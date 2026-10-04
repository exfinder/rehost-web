#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace System.Web.Util;

using System.Runtime.Remoting.Messaging;

[Serializable]
[System.Runtime.InteropServices.ComVisible(true)]
internal sealed class LegacyCallContext
{
    private static readonly AsyncLocal<LogicalState?> LogicalStateSlot = new();
    private static readonly AsyncLocal<IllogicalState?> IllogicalStateSlot =
        new(OnIllogicalContextChanged);

    // Weak on purpose: a suspended state can only ever be resumed by a captured ExecutionContext
    // that still references it, so once nothing does, the entry is dead and is pruned on the next
    // transition. Strong entries never left the stack (most flows resume elsewhere) and grew by
    // roughly one per request per pool thread for the life of the process.
    [ThreadStatic]
    private static Stack<WeakReference<IllogicalState>>? _suspendedIllogicalStates;

    // ExecutionContext.Run wipes the illogical state, and modern .NET has no
    // HostExecutionContextManager through which a host could re-establish it inside the run the
    // way Framework did. This hook is that seam: on each wipe it receives the flowed HostContext
    // and returns the value to re-establish on the current thread, or null to leave the wipe.
    private static Func<object?, object?>? _hostContextRestorer;

    internal static void RegisterHostContextRestorer(Func<object?, object?> restorer)
    {
        if (Interlocked.CompareExchange(ref _hostContextRestorer, restorer, null) is not null)
            throw new InvalidOperationException(
                "A host context restorer is already registered; the restorer is fixed for the process lifetime.");
    }

    private LegacyCallContext()
    {
    }

    public static object? GetData(string name) =>
        LogicalStateSlot.Value?.GetData(name) ?? GetIllogicalData(name);

    public static void SetData(string name, object? data)
    {
        if (data is ILogicalThreadAffinative)
        {
            LogicalSetData(name, data);
            return;
        }

        SetLogicalData(name, null, remove: true);
        GetOrCreateIllogicalState().Data[name] = data;
    }

    public static void LogicalSetData(string name, object? data)
    {
        IllogicalStateSlot.Value?.Data.Remove(name);
        SetLogicalData(name, data, remove: false);
    }

    public static object? LogicalGetData(string name) => LogicalStateSlot.Value?.GetData(name);

    public static void FreeNamedDataSlot(string name)
    {
        SetLogicalData(name, null, remove: true);
        IllogicalStateSlot.Value?.Data.Remove(name);
    }

    public static object? HostContext
    {
        get => IllogicalStateSlot.Value?.HostContext ?? LogicalStateSlot.Value?.HostContext;
        set
        {
            if (value is ILogicalThreadAffinative)
            {
                ClearIllogicalHostContext();
                SetLogicalHostContext(value);
            }
            else
            {
                SetIllogicalHostContext(value);
                SetLogicalHostContext(null);
            }
        }
    }

    private static object? GetIllogicalData(string name) =>
        IllogicalStateSlot.Value?.Data.TryGetValue(name, out var value) == true ? value : null;

    private static void SetLogicalData(string name, object? value, bool remove)
    {
        ArgumentNullException.ThrowIfNull(name);
        LogicalStateSlot.Value = (LogicalStateSlot.Value ?? LogicalState.Empty).WithData(name, value, remove);
    }

    private static void SetLogicalHostContext(object? value) =>
        LogicalStateSlot.Value = (LogicalStateSlot.Value ?? LogicalState.Empty).WithHostContext(value);

    // Captured ExecutionContexts keep references to the published state, so HostContext changes
    // must replace the state object rather than mutate it; an in-place write would rewrite what
    // an in-flight await captured (and the restore seam keys off the captured value).
    private static void SetIllogicalHostContext(object? value)
    {
        var current = IllogicalStateSlot.Value;
        if (current is null)
        {
            if (value is null)
                return;
            IllogicalStateSlot.Value = new IllogicalState(value);
            return;
        }

        if (!ReferenceEquals(current.HostContext, value))
            IllogicalStateSlot.Value = current.WithHostContext(value);
    }

    private static void ClearIllogicalHostContext() => SetIllogicalHostContext(null);

    private static IllogicalState GetOrCreateIllogicalState() =>
        IllogicalStateSlot.Value ??= new IllogicalState();

    private static void OnIllogicalContextChanged(AsyncLocalValueChangedArgs<IllogicalState?> args)
    {
        if (!args.ThreadContextChanged)
            return;

        var suspended = PruneDeadSuspendedStates();
        if (args.CurrentValue is not null && ReferenceEquals(suspended, args.CurrentValue))
        {
            _suspendedIllogicalStates!.Pop();
            return;
        }

        if (args.PreviousValue is not null)
            (_suspendedIllogicalStates ??= new Stack<WeakReference<IllogicalState>>())
                .Push(new WeakReference<IllogicalState>(args.PreviousValue));

        if (args.CurrentValue is not null)
        {
            var restored = _hostContextRestorer?.Invoke(args.CurrentValue.HostContext);
            IllogicalStateSlot.Value = restored is null ? null : new IllogicalState(restored);
        }
    }

    private static IllogicalState? PruneDeadSuspendedStates()
    {
        var stack = _suspendedIllogicalStates;
        if (stack is null)
            return null;

        while (stack.TryPeek(out var top))
        {
            if (top.TryGetTarget(out var state))
                return state;
            stack.Pop();
        }

        return null;
    }

    private sealed class IllogicalState
    {
        internal IllogicalState()
        {
            Data = new Dictionary<string, object?>();
        }

        internal IllogicalState(object? hostContext)
            : this()
        {
            HostContext = hostContext;
        }

        private IllogicalState(Dictionary<string, object?> data, object? hostContext)
        {
            Data = data;
            HostContext = hostContext;
        }

        // Shared between generations on purpose: illogical data never survives a flow (the wipe
        // clears it), so only the owning thread observes it and in-place mutation stays invisible.
        internal Dictionary<string, object?> Data { get; }

        internal object? HostContext { get; }

        internal IllogicalState WithHostContext(object? value) => new(Data, value);
    }

    private sealed class LogicalState
    {
        internal static readonly LogicalState Empty = new(new Dictionary<string, object?>(), null);

        private readonly Dictionary<string, object?> _data;

        private LogicalState(Dictionary<string, object?> data, object? hostContext)
        {
            _data = data;
            HostContext = hostContext;
        }

        internal object? HostContext { get; }

        internal object? GetData(string name) => _data.TryGetValue(name, out var value) ? value : null;

        internal LogicalState WithData(string name, object? value, bool remove)
        {
            var data = new Dictionary<string, object?>(_data);
            if (remove)
                data.Remove(name);
            else
                data[name] = value;
            return new LogicalState(data, HostContext);
        }

        internal LogicalState WithHostContext(object? value) => new(_data, value);
    }
}

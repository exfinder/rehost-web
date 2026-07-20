#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace System.Runtime.Remoting.Messaging;

[System.Runtime.InteropServices.ComVisible(true)]
internal interface ILogicalThreadAffinative
{
}

[Serializable]
[System.Runtime.InteropServices.ComVisible(true)]
internal sealed class CallContext
{
    private static readonly AsyncLocal<LogicalState?> LogicalStateSlot = new();
    private static readonly AsyncLocal<IllogicalState?> IllogicalStateSlot =
        new(OnIllogicalContextChanged);

    [ThreadStatic]
    private static Stack<IllogicalState>? _suspendedIllogicalStates;

    private CallContext()
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

    private static void SetIllogicalHostContext(object? value) =>
        GetOrCreateIllogicalState().HostContext = value;

    private static void ClearIllogicalHostContext()
    {
        if (IllogicalStateSlot.Value is not null)
            IllogicalStateSlot.Value.HostContext = null;
    }

    private static IllogicalState GetOrCreateIllogicalState() =>
        IllogicalStateSlot.Value ??= new IllogicalState();

    private static void OnIllogicalContextChanged(AsyncLocalValueChangedArgs<IllogicalState?> args)
    {
        if (!args.ThreadContextChanged)
            return;

        if (args.CurrentValue is not null &&
            _suspendedIllogicalStates?.TryPeek(out var suspended) == true &&
            ReferenceEquals(suspended, args.CurrentValue))
        {
            _suspendedIllogicalStates.Pop();
            return;
        }

        if (args.PreviousValue is not null)
            (_suspendedIllogicalStates ??= new Stack<IllogicalState>()).Push(args.PreviousValue);

        if (args.CurrentValue is not null)
            IllogicalStateSlot.Value = null;
    }

    private sealed class IllogicalState
    {
        internal Dictionary<string, object?> Data { get; } = new();

        internal object? HostContext { get; set; }
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

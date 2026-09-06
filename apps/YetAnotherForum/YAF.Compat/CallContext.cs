using System.Collections.Concurrent;
using System.Threading;

namespace System.Runtime.Remoting.Messaging;

public static class CallContext
{
    private static readonly ConcurrentDictionary<string, AsyncLocal<object?>> Slots = new();

    public static void LogicalSetData(string name, object? data) => Slot(name).Value = data;

    public static object? LogicalGetData(string name) => Slot(name).Value;

    public static void SetData(string name, object? data) => LogicalSetData(name, data);

    public static object? GetData(string name) => LogicalGetData(name);

    public static void FreeNamedDataSlot(string name) => Slot(name).Value = null;

    private static AsyncLocal<object?> Slot(string name) => Slots.GetOrAdd(name, static _ => new AsyncLocal<object?>());
}

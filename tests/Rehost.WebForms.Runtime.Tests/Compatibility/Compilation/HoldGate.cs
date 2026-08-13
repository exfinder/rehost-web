using Shouldly;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

// A held mutex, rather than a sleep of a guessed length: the child stays alive exactly while
// the overlap under test lasts, and neither machine speed nor load can shorten it.
internal sealed class HoldGate : IDisposable
{
    private readonly Mutex _mutex;
    private bool _held;

    private HoldGate(Mutex mutex, string name)
    {
        _mutex = mutex;
        _held = true;
        Name = name;
    }

    internal string Name { get; }

    internal static HoldGate Take()
    {
        var name = @"Local\rehost-scenario-" + Guid.NewGuid().ToString("n");
        var mutex = new Mutex(false, name);
        mutex.WaitOne();

        return new HoldGate(mutex, name);
    }

    internal void Release()
    {
        if (_held)
        {
            _mutex.ReleaseMutex();
            _held = false;
        }
    }

    public void Dispose()
    {
        Release();
        _mutex.Dispose();
    }
}

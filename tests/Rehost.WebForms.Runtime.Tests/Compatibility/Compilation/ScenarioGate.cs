using Shouldly;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

// A held mutex, rather than a sleep of a guessed length: the child stays alive exactly while
// the overlap under test lasts, and neither machine speed nor load can shorten it.
internal sealed class ScenarioGate : IDisposable
{
    private readonly Mutex _mutex;
    private bool _held;

    private ScenarioGate(Mutex mutex, string name)
    {
        _mutex = mutex;
        _held = true;
        Name = name;
    }

    internal string Name { get; }

    internal static ScenarioGate Take()
    {
        var name = @"Local\rehost-scenario-" + Guid.NewGuid().ToString("n");
        var mutex = new Mutex(false, name);
        mutex.WaitOne();

        return new ScenarioGate(mutex, name);
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

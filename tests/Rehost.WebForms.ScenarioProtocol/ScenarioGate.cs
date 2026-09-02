using System.Diagnostics;
using System.IO.Pipes;

namespace Rehost.WebForms.ScenarioProtocol;

// One arrival and one release across processes: application code parks inside the gate while the
// test observes that it is parked. A named pipe carries it because named event handles are
// Windows-only; the Unix path is /tmp/CoreFxPipe_<name>, which caps how long a name may be.
public sealed class ScenarioGate : IDisposable
{
    public const string GateVariable = "REHOST_SCENARIO_GATE";

    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(30);

    internal const byte Arrived = 1;
    internal const byte Released = 2;

    private const string ReleaseOrder =
        "Wait for the arrival before releasing it, and check that the application process received";

    private readonly NamedPipeServerStream _pipe;

    private int _waited;
    private int _arrived;
    private int _released;
    private int _connected;
    private int _peerLeft;

    public ScenarioGate()
    {
        // macOS caps a domain-socket path at 104 characters and TMPDIR there is already ~50, so
        // the name stays short rather than descriptive.
        Name = $"rhg-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}";
        _pipe = new NamedPipeServerStream(
            Name,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    public string Name { get; }

    public static ScenarioGateWaiter Open() => Open(Environment.GetEnvironmentVariable(GateVariable));

    public static ScenarioGateWaiter Open(string? name) => new(name);

    public Task WaitForArrivalAsync(CancellationToken cancellationToken = default) =>
        WaitForArrivalAsync(DefaultWait, cancellationToken);

    public async Task WaitForArrivalAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!await TryWaitForArrivalAsync(timeout, cancellationToken))
        {
            throw new TimeoutException(
                $"Nothing arrived at gate '{Name}' within {timeout}: {Phase()}.");
        }
    }

    public Task<bool> TryWaitForArrivalAsync(CancellationToken cancellationToken = default) =>
        TryWaitForArrivalAsync(DefaultWait, cancellationToken);

    public async Task<bool> TryWaitForArrivalAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (Interlocked.Exchange(ref _waited, 1) == 1)
        {
            throw new InvalidOperationException(
                $"Gate '{Name}' takes one arrival and has already been waited on.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        var arrival = new byte[1];
        try
        {
            await _pipe.WaitForConnectionAsync(deadline.Token);
            Volatile.Write(ref _connected, 1);

            var read = await _pipe.ReadAsync(arrival, deadline.Token);
            Volatile.Write(ref _peerLeft, read == 0 ? 1 : 0);

            var arrived = read == 1 && arrival[0] == Arrived;
            Volatile.Write(ref _arrived, arrived ? 1 : 0);
            return arrived;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public void Release()
    {
        if (Volatile.Read(ref _arrived) == 0)
        {
            throw new InvalidOperationException(
                $"Nothing has arrived at gate '{Name}'. {ReleaseOrder} {GateVariable}={Name}.");
        }

        if (Interlocked.Exchange(ref _released, 1) == 1)
        {
            throw new InvalidOperationException($"Gate '{Name}' is already released.");
        }

        _pipe.WriteByte(Released);
        _pipe.Flush();
    }

    // A test that throws between arrival and release would otherwise leave the application parked
    // for its own timeout, holding whatever the gate stands in front of.
    public void Dispose()
    {
        if (Volatile.Read(ref _arrived) == 1 && Interlocked.Exchange(ref _released, 1) == 0)
        {
            try
            {
                _pipe.WriteByte(Released);
                _pipe.Flush();
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _pipe.Dispose();
    }

    private string Phase() =>
        Volatile.Read(ref _connected) == 0
            ? "the application never connected"
            : Volatile.Read(ref _peerLeft) == 1
                ? "the application connected, then left without arriving"
                : "the application connected but never arrived";
}

// The application side of a gate. An unarmed waiter is the normal case: a scenario that never
// names a gate runs the same probe code and passes straight through.
public sealed class ScenarioGateWaiter : IDisposable
{
    private readonly NamedPipeClientStream? _pipe;

    private int _arrived;

    internal ScenarioGateWaiter(string? name)
    {
        Name = name;
        _pipe = string.IsNullOrEmpty(name)
            ? null
            : new NamedPipeClientStream(".", name, PipeDirection.InOut);
    }

    public string? Name { get; }

    public bool Armed => _pipe != null;

    public void ArriveAndWait() => ArriveAndWait(ScenarioGate.DefaultWait);

    public void ArriveAndWait(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (_pipe == null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _arrived, 1) == 1)
        {
            throw new InvalidOperationException(
                $"Gate '{Name}' takes one arrival and has already been arrived at.");
        }

        var elapsed = Stopwatch.StartNew();
        try
        {
            _pipe.Connect(Remaining(timeout, elapsed));
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Gate '{Name}' was not listening within {timeout}: the test never created it.");
        }

        _pipe.WriteByte(ScenarioGate.Arrived);
        _pipe.Flush();

        // The caller is a request thread inside application code and a blocking pipe read cannot
        // be cancelled, so the read runs off-thread where the timeout can win the race.
        var released = Task.Run(_pipe.ReadByte);
        if (!released.Wait(Remaining(timeout, elapsed)))
        {
            // Disposing the pipe faults the read this call has abandoned.
            released.ContinueWith(
                static abandoned => _ = abandoned.Exception,
                TaskContinuationOptions.OnlyOnFaulted);

            throw new TimeoutException($"Gate '{Name}' was never released within {timeout}.");
        }

        if (released.Result == -1)
        {
            throw new InvalidOperationException(
                $"Gate '{Name}' closed before it was released: the test left without doing so.");
        }

        if (released.Result != ScenarioGate.Released)
        {
            throw new InvalidOperationException(
                $"Gate '{Name}' sent {released.Result} where a release was expected.");
        }
    }

    public void Dispose() => _pipe?.Dispose();

    private static int Remaining(TimeSpan timeout, Stopwatch elapsed)
    {
        var left = timeout - elapsed.Elapsed;
        return left > TimeSpan.Zero ? (int)Math.Min(left.TotalMilliseconds, int.MaxValue) : 0;
    }
}

namespace Rehost.WebForms.Hosting;

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;

internal sealed class RequestBodyCoordinator
{
    private readonly PipeReader _reader;
    private readonly CancellationToken _requestAborted;
    private int _readPending;
    private int _stopped;
    private TerminalState? _terminal;

    internal RequestBodyCoordinator(PipeReader reader, CancellationToken requestAborted)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
        _requestAborted = requestAborted;
    }

    internal int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (count == 0 || Volatile.Read(ref _stopped) != 0)
        {
            return 0;
        }

        var terminal = Volatile.Read(ref _terminal);
        if (terminal != null)
        {
            return terminal.ReadSynchronously();
        }

        AcquireRead();
        try
        {
            return ReadCoreAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            return LatchFailure(exception).ReadSynchronously();
        }
        finally
        {
            ReleaseRead();
        }
    }

    internal IAsyncResult BeginRead(
        byte[] buffer,
        int offset,
        int count,
        AsyncCallback? callback,
        object? state)
    {
        ValidateBufferArguments(buffer, offset, count);

        var result = new RequestBodyAsyncResult(this, callback, state);
        if (count == 0)
        {
            result.Complete(0, null, completedSynchronously: true);
            return result;
        }

        var terminal = Volatile.Read(ref _terminal);
        if (terminal != null)
        {
            return terminal.BeginRead(result);
        }

        AcquireRead();
        var read = ReadCoreAsync(buffer.AsMemory(offset, count));
        if (read.IsCompleted)
        {
            var bytesRead = 0;
            try
            {
                bytesRead = read.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                ReleaseRead();
                throw LatchFailure(exception).CreateException();
            }

            ReleaseRead();
            result.Complete(bytesRead, null, completedSynchronously: true);
            return result;
        }

        _ = CompleteReadAsync(read, result);
        return result;
    }

    internal int EndRead(IAsyncResult asyncResult)
    {
        ArgumentNullException.ThrowIfNull(asyncResult);
        if (asyncResult is not RequestBodyAsyncResult result || result.Owner != this)
        {
            throw new ArgumentException(null, nameof(asyncResult));
        }

        return result.End();
    }

    // RequestAborted is raised after the read that observed the reset, and System.Web decides
    // between end of body and HttpException by asking IsClientConnected at exactly that moment
    // (HttpBufferlessInputStream.Read). Reporting the latched terminal keeps that decision off
    // the token's timing.
    internal bool ClientDisconnected =>
        Volatile.Read(ref _terminal) is { } terminal && terminal.IsClientDisconnect;

    internal void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            _reader.CancelPendingRead();
        }
    }

    private static void ValidateBufferArguments(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (buffer.Length - offset < count)
        {
            throw new ArgumentException("Offset and count exceed the buffer length.");
        }
    }

    private void AcquireRead()
    {
        if (Interlocked.CompareExchange(ref _readPending, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "An asynchronous request-body read is already pending.");
        }
    }

    private void ReleaseRead()
    {
        Volatile.Write(ref _readPending, 0);
    }

    private async Task CompleteReadAsync(
        ValueTask<int> read,
        RequestBodyAsyncResult result)
    {
        var bytesRead = 0;
        Exception? error = null;

        try
        {
            bytesRead = await read.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = LatchFailure(exception).CreateException();
        }

        ReleaseRead();
        result.Complete(bytesRead, error, completedSynchronously: false);
    }

    private async ValueTask<int> ReadCoreAsync(Memory<byte> destination)
    {
        while (true)
        {
            if (Volatile.Read(ref _stopped) != 0)
            {
                throw new OperationCanceledException("Request processing has completed.");
            }

            var read = await _reader.ReadAsync(_requestAborted).ConfigureAwait(false);
            var buffer = read.Buffer;

            if (Volatile.Read(ref _stopped) != 0)
            {
                _reader.AdvanceTo(buffer.Start, buffer.End);
                throw new OperationCanceledException("Request processing has completed.");
            }

            var bytesRead = (int)Math.Min(buffer.Length, destination.Length);

            if (bytesRead > 0)
            {
                buffer.Slice(0, bytesRead).CopyTo(destination.Span);
            }

            var consumed = buffer.GetPosition(bytesRead);
            _reader.AdvanceTo(consumed);

            if (read.IsCanceled && bytesRead == 0)
            {
                throw new OperationCanceledException("The request-body read was canceled.");
            }

            if (read.IsCompleted && bytesRead == buffer.Length)
            {
                LatchTerminal(TerminalState.EndOfBody);
            }

            if (bytesRead > 0 || read.IsCompleted)
            {
                return bytesRead;
            }

        }
    }

    // Typed, not RequestAborted: a reset races the token and would classify nondeterministically.
    private TerminalState LatchFailure(Exception exception)
    {
        var terminal = exception switch
        {
            BadHttpRequestException rejection => TerminalState.HostRejected(rejection),
            OperationCanceledException or ConnectionAbortedException or ConnectionResetException =>
                TerminalState.Disconnected(exception),
            _ when Volatile.Read(ref _stopped) != 0 => TerminalState.Disconnected(exception),
            _ => TerminalState.Failed(exception),
        };
        return LatchTerminal(terminal);
    }

    private TerminalState LatchTerminal(TerminalState terminal)
    {
        return Interlocked.CompareExchange(ref _terminal, terminal, null) ?? terminal;
    }

    private sealed class RequestBodyAsyncResult : IAsyncResult
    {
        private readonly AsyncCallback? _callback;
        private readonly object? _state;
        private readonly ManualResetEventSlim _waitHandle = new();
        private ExceptionDispatchInfo? _error;
        private int _bytesRead;
        private int _completed;
        private int _ended;

        internal RequestBodyAsyncResult(
            RequestBodyCoordinator owner,
            AsyncCallback? callback,
            object? state)
        {
            Owner = owner;
            _callback = callback;
            _state = state;
        }

        internal RequestBodyCoordinator Owner { get; }

        public object? AsyncState => _state;

        public WaitHandle AsyncWaitHandle => _waitHandle.WaitHandle;

        public bool CompletedSynchronously { get; private set; }

        public bool IsCompleted => Volatile.Read(ref _completed) != 0;

        internal void Complete(int bytesRead, Exception? error, bool completedSynchronously)
        {
            _bytesRead = bytesRead;
            if (error != null)
            {
                _error = ExceptionDispatchInfo.Capture(error);
            }

            CompletedSynchronously = completedSynchronously;
            Volatile.Write(ref _completed, 1);
            _waitHandle.Set();
            _callback?.Invoke(this);
        }

        internal int End()
        {
            if (Interlocked.Exchange(ref _ended, 1) != 0)
            {
                throw new InvalidOperationException("EndRead was called more than once.");
            }

            _waitHandle.Wait();
            _waitHandle.Dispose();
            _error?.Throw();
            return _bytesRead;
        }
    }

    private sealed class TerminalState
    {
        private readonly ExceptionDispatchInfo? _error;
        private readonly TerminalKind _kind;

        private TerminalState(
            TerminalKind kind,
            Exception? error = null,
            int? statusCode = null)
        {
            _kind = kind;
            _error = error == null ? null : ExceptionDispatchInfo.Capture(error);
            StatusCode = statusCode;
        }

        internal static TerminalState EndOfBody { get; } = new(TerminalKind.EndOfBody);

        internal int? StatusCode { get; }

        internal bool IsClientDisconnect => _kind == TerminalKind.Disconnected;

        internal static TerminalState Disconnected(Exception error) =>
            new(TerminalKind.Disconnected, error);

        internal static TerminalState Failed(Exception error) =>
            new(TerminalKind.Failed, error);

        internal static TerminalState HostRejected(BadHttpRequestException error) =>
            new(TerminalKind.HostRejected, error, error.StatusCode);

        internal IAsyncResult BeginRead(RequestBodyAsyncResult result)
        {
            if (_kind == TerminalKind.EndOfBody)
            {
                result.Complete(0, null, completedSynchronously: true);
                return result;
            }

            throw CreateException();
        }

        internal HttpException CreateException()
        {
            var source = _error?.SourceException;
            if (source is HttpException already)
            {
                return already;
            }

            return _kind == TerminalKind.HostRejected
                ? new HttpException(StatusCode!.Value, source!.Message, source)
                : new HttpException(
                    "The client disconnected while the request body was being read.",
                    source);
        }

        internal int ReadSynchronously()
        {
            switch (_kind)
            {
                case TerminalKind.EndOfBody:
                case TerminalKind.Disconnected:
                    return 0;

                case TerminalKind.HostRejected:
                    throw CreateException();

                default:
                    _error!.Throw();
                    throw new InvalidOperationException();
            }
        }
    }

    private enum TerminalKind
    {
        EndOfBody,
        Disconnected,
        Failed,
        HostRejected,
    }
}

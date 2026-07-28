using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

// P33: the recursive write lock decides re-entrancy by comparing a thread id against one it
// recorded earlier, so that identity has to be stable per thread and distinct across threads.
public sealed class HttpApplicationStateLockTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(5);

    [Fact]
    public void A_write_lock_is_recursive_on_the_owning_thread()
    {
        var stateLock = new HttpApplicationStateLock();

        stateLock.AcquireWrite();
        stateLock.AcquireWrite();
        stateLock.ReleaseWrite();

        var contender = Contend(stateLock);
        contender.Join(Settle).ShouldBeFalse("one recursive hold remains");

        stateLock.ReleaseWrite();
        contender.Join(Generous).ShouldBeTrue();
    }

    [Fact]
    public void Ensure_release_write_drops_every_recursive_hold()
    {
        var stateLock = new HttpApplicationStateLock();

        stateLock.AcquireWrite();
        stateLock.AcquireWrite();
        stateLock.EnsureReleaseWrite();

        Contend(stateLock).Join(Generous).ShouldBeTrue();
    }

    [Fact]
    public void Reads_taken_by_the_writing_thread_do_not_deadlock()
    {
        var stateLock = new HttpApplicationStateLock();

        stateLock.AcquireWrite();
        stateLock.AcquireRead();
        stateLock.ReleaseRead();
        stateLock.ReleaseWrite();

        Contend(stateLock).Join(Generous).ShouldBeTrue();
    }

    [Fact]
    public void Thread_identity_is_stable_within_a_thread_and_distinct_across_threads()
    {
        var current = SafeNativeMethods.GetCurrentThreadId();

        SafeNativeMethods.GetCurrentThreadId().ShouldBe(current);

        var other = 0;
        var thread = new Thread(() => other = SafeNativeMethods.GetCurrentThreadId());
        thread.Start();
        thread.Join(Generous).ShouldBeTrue();

        other.ShouldNotBe(0);
        other.ShouldNotBe(current);
    }

    private static Thread Contend(HttpApplicationStateLock stateLock)
    {
        var started = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            started.Set();
            stateLock.AcquireWrite();
            stateLock.ReleaseWrite();
        })
        {
            IsBackground = true
        };

        thread.Start();
        started.Wait(Generous).ShouldBeTrue();
        return thread;
    }
}

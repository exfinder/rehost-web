using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests;

// P33: the recursive write lock decides re-entrancy by comparing a thread id against one it
// recorded earlier, so that identity has to be stable per thread and distinct across threads.
public sealed class HttpApplicationStateLockTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(5);

    [Fact]
    public void A_Write_Lock_Is_Recursive_On_The_Owning_Thread()
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
    public void Ensure_Release_Write_Drops_Every_Recursive_Hold()
    {
        var stateLock = new HttpApplicationStateLock();

        stateLock.AcquireWrite();
        stateLock.AcquireWrite();
        stateLock.EnsureReleaseWrite();

        Contend(stateLock).Join(Generous).ShouldBeTrue();
    }

    [Fact]
    public void Reads_Taken_By_The_Writing_Thread_Do_Not_Deadlock()
    {
        var stateLock = new HttpApplicationStateLock();

        stateLock.AcquireWrite();
        stateLock.AcquireRead();
        stateLock.ReleaseRead();
        stateLock.ReleaseWrite();

        Contend(stateLock).Join(Generous).ShouldBeTrue();
    }

    [Fact]
    public void Thread_Identity_Is_Stable_Within_A_Thread_And_Distinct_Across_Threads()
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

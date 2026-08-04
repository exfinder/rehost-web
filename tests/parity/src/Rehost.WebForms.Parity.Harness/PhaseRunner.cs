using System;

namespace Rehost.WebForms.Parity.Harness;

public static class PhaseRunner
{
    public static void InPhase(string phase, Action action)
    {
        try
        {
            action();
        }
        catch (PhaseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PhaseException(phase, exception);
        }
    }

    public static T InPhase<T>(string phase, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (PhaseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PhaseException(phase, exception);
        }
    }
}

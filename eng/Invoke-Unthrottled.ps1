<#
.SYNOPSIS
Runs a command with its whole process tree pinned at Windows High QoS.

.DESCRIPTION
Processes launched over SSH have no foreground presence, so Windows assigns them
EcoQoS: confined to E-cores and frequency-clamped. Pinning a process at High QoS
is a per-process attribute that is NOT inherited by children, so a build or test
run has to have every descendant pinned individually as it appears.

See docs/windows-host-cpu-throttling.md for the measurements and for the
machine-wide registry setting this pairs with.

.EXAMPLE
pwsh -NoProfile -File eng/Invoke-Unthrottled.ps1 -Command 'dotnet test Rehost.Web.slnx --no-build'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Command,
    [string]$WorkingDirectory = (Get-Location).Path,
    [int]$PollMs = 50,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw "Invoke-Unthrottled.ps1 is Windows-only: QoS levels have no equivalent on this platform. Run the command directly."
}

if (-not ('Rehost.Unthrottle' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rehost
{
    public static class Unthrottle
    {
        [DllImport("psapi.dll", SetLastError = true)] static extern bool EnumProcesses([Out] uint[] pids, uint cb, out uint needed);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr h, int cls, IntPtr info, uint len, out uint ret);

        [StructLayout(LayoutKind.Sequential)]
        struct PowerThrottlingState { public uint Version; public uint ControlMask; public uint StateMask; }

        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetProcessInformation(IntPtr h, int cls, ref PowerThrottlingState s, uint size);

        const uint QueryLimited = 0x1000;
        const uint SetInformation = 0x0200;
        const int ProcessPowerThrottling = 4;
        const int ProcessBasicInformation = 0;
        const uint ExecutionSpeed = 0x1;

        static volatile bool _run;
        static Thread _thread;

        public static int Applied;
        public static List<string> Names = new List<string>();

        static uint ParentOf(uint pid)
        {
            IntPtr h = OpenProcess(QueryLimited, false, pid);
            if (h == IntPtr.Zero) return 0xFFFFFFFF;
            IntPtr buf = Marshal.AllocHGlobal(48);
            try
            {
                uint ret;
                if (NtQueryInformationProcess(h, ProcessBasicInformation, buf, 48, out ret) != 0) return 0xFFFFFFFF;
                return (uint)(long)Marshal.ReadIntPtr(buf, 40);
            }
            finally { Marshal.FreeHGlobal(buf); CloseHandle(h); }
        }

        // ControlMask=EXECUTION_SPEED with StateMask=0 pins the process at High
        // QoS. ControlMask=0 would hand the decision back to the scheduler,
        // which is the throttled default.
        static bool OptOut(uint pid)
        {
            IntPtr h = OpenProcess(SetInformation | QueryLimited, false, pid);
            if (h == IntPtr.Zero) return false;
            try
            {
                PowerThrottlingState s = new PowerThrottlingState();
                s.Version = 1;
                s.ControlMask = ExecutionSpeed;
                s.StateMask = 0;
                return SetProcessInformation(h, ProcessPowerThrottling, ref s, (uint)Marshal.SizeOf(typeof(PowerThrottlingState)));
            }
            finally { CloseHandle(h); }
        }

        static string NameOf(uint pid)
        {
            try { return Process.GetProcessById((int)pid).ProcessName; } catch { return "pid" + pid; }
        }

        static DateTime StartTimeOf(uint pid)
        {
            try { return Process.GetProcessById((int)pid).StartTime; } catch { return DateTime.MaxValue; }
        }

        public static void Start(uint rootPid, int intervalMs)
        {
            Applied = 0;
            Names = new List<string>();
            var parentOf = new Dictionary<uint, uint>();
            var tracked = new HashSet<uint>();
            var excluded = new HashSet<uint>();
            tracked.Add(rootPid);
            if (OptOut(rootPid)) { Applied++; Names.Add(NameOf(rootPid)); }
            DateTime rootStart = StartTimeOf(rootPid);

            _run = true;
            _thread = new Thread(delegate ()
            {
                uint[] pids = new uint[8192];
                while (_run)
                {
                    uint needed;
                    if (EnumProcesses(pids, (uint)(pids.Length * 4), out needed))
                    {
                        int n = (int)(needed / 4);
                        for (int i = 0; i < n; i++)
                        {
                            uint p = pids[i];
                            if (p != 0 && !parentOf.ContainsKey(p)) parentOf[p] = ParentOf(p);
                        }

                        // A grandchild can be enumerated in the same pass as its
                        // parent, so keep resolving until the tree stops growing.
                        bool changed = true;
                        while (changed)
                        {
                            changed = false;
                            foreach (var kv in new List<KeyValuePair<uint, uint>>(parentOf))
                            {
                                if (tracked.Contains(kv.Key) || excluded.Contains(kv.Key)) continue;
                                if (!tracked.Contains(kv.Value)) continue;
                                // Pids are recycled, so an unrelated long-lived
                                // process can inherit a parent pid that now
                                // belongs to this tree. Nothing in the tree can
                                // predate its root. Such a process must not enter
                                // `tracked`, or its own children get adopted too.
                                if (StartTimeOf(kv.Key) < rootStart) { excluded.Add(kv.Key); continue; }
                                tracked.Add(kv.Key);
                                if (OptOut(kv.Key)) { Applied++; Names.Add(NameOf(kv.Key)); }
                                changed = true;
                            }
                        }
                    }
                    Thread.Sleep(intervalMs);
                }
            });
            _thread.IsBackground = true;
            _thread.Start();
        }

        public static void Stop()
        {
            _run = false;
            if (_thread != null) _thread.Join(3000);
        }
    }
}
'@ -ErrorAction Stop
}

$parts = $Command -split '\s+', 2
$exe = $parts[0]
$rest = if ($parts.Count -gt 1) { $parts[1] } else { '' }

$startArgs = @{
    FilePath         = $exe
    WorkingDirectory = $WorkingDirectory
    NoNewWindow      = $true
    PassThru         = $true
}
if ($rest) { $startArgs.ArgumentList = $rest }

$sw = [Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process @startArgs
[Rehost.Unthrottle]::Start([uint]$proc.Id, $PollMs)
try {
    $proc.WaitForExit()
}
finally {
    [Rehost.Unthrottle]::Stop()
    $sw.Stop()
}

if (-not $Quiet) {
    $grouped = ([Rehost.Unthrottle]::Names | Group-Object | Sort-Object Count -Descending |
        ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ', '
    # Straight to stderr, not Write-Host: PowerShell's information stream comes
    # back wrapped in CLIXML when a caller pipes this over ssh. The wrapped
    # command inherits the console rather than being redirected, so its output
    # streams live and never passes through here.
    # The inner parentheses are load-bearing: without them the commas bind to
    # WriteLine's argument list instead of the -f operand array.
    [Console]::Error.WriteLine(("[High QoS] {0:N1}s, exit {1}, {2} processes pinned: {3}" -f `
                $sw.Elapsed.TotalSeconds, $proc.ExitCode, [Rehost.Unthrottle]::Applied, $grouped))
}

exit $proc.ExitCode

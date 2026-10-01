# WebForms Identity application findings

Evidence from bringing up the frozen Visual Studio Individual User Accounts
template. Current application status lives in
[its development notes](../../apps/WebFormsIdentityApplication/DEVELOPMENT.md).

## Result

The application now matches its IIS/Framework journey on Windows x64, Linux,
and macOS arm64. Katana's System.Web host was recompiled under Rehost identity;
OWIN, ASP.NET Identity 2.2, and EF 6.4 run unchanged. LocalDb is the only
platform-bound dependency; the sidecar supplies reachable SQL Server instead.

## Baseline journey

Captured 2026-08-15 on IIS Express / Framework 4.8.1:

| Step | Result |
| --- | --- |
| Anonymous `/Account/Manage` | 302 to absolute login URL with `ReturnUrl` |
| Register | EF creates schema; 302 `/`; application cookie set; external/two-factor cookies cleared |
| Authenticated manage/home | User name and logged-in `LoginView` render |
| Log out | 302 `/`; both `.ASPXAUTH` and OWIN application cookie expire |
| Wrong/right login | 200 with error / 302 with cookie |

`apps/WebFormsIdentityApplication/smoke.sh` matches every row on all supported
platforms.

## Gaps exposed and closed

| Gap | Resolution |
| --- | --- |
| Missing `ListView`/`DataPager` closure | Controls compiled and registered |
| Bare BCL type failed `BuildManager.GetType` | `mscorlib` facade entry restored |
| Mandatory Dynamic Data load blocked model binding | Absent assembly now leaves the optional hook inactive (ledger P69) |
| Lowercase subfolder config path missed `Web.config` | Configuration path case-folding added (ledger P70) |
| EF configuration placement was uncertain | Existing `HttpConfigurationSystem` already supplies site configuration |

## Remaining boundaries

- Auto-generated machine keys are process-scoped; restart invalidates OWIN
  cookies unless the application supplies persistent keys.
- External providers, mail/SMS, confirmation/reset, and two-factor flows are
  present but unassessed because the template leaves them disabled.
- LocalDb remains Windows-only and outside the portable contract.
- Integrated-only Katana stage markers, WebSockets, disconnect, and shutdown
  detection remain unassessed for this host.

## Sources

- Frozen application: `apps/WebFormsIdentityApplication/`
- Katana provenance: [aspnet-katana](../provenance/aspnet-katana.md)
- OWIN support boundary: [compatibility map](../compatibility.md)

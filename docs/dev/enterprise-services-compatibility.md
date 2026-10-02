# Enterprise Services compatibility

System.Web preserves its internal Framework transaction enum/method shapes but
does not support COM+ page transaction execution on any platform.

`TransactionOption`, `TransactionVote`, and `ContextUtil` remain internal under
the original `System.EnterpriseServices` namespace. Transaction entry points
throw actionable `PlatformNotSupportedException`; fallback probes remain
false. Silent no-ops and mapping to `System.Transactions` were rejected because
both misrepresent COM+ semantics.

No public `System.EnterpriseServices` assembly identity, COM+, IIS native
callback, CAS demand, performance counter, or ambient transaction behavior is
claimed.

Implementation:
`src/Rehost.Web/Compatibility/EnterpriseServices`.
Remaining scope:
[Enterprise Services](follow-ups/enterprise-services.md).

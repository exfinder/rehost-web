# ASP.NET Core host adapter gaps

The implemented boundary is recorded in
[the host ADR](../adr/0003-host-boundary.md) and
[compatibility map](../compatibility.md). This file owns only residual work.

## Remaining work

- HTTP/3: no transport gate.
- Compression is IIS's module and its configuration; on the
  [IIS-role follow-up](iis-role-behaviors.md).
- Client certificates behind a proxy: forwarded-certificate header into `CERT_*`
  when a consumer needs it.
- Integrated-mode server variables (`UNENCODED_URL`, `HTTP_URL`, native-module
  values) and `ServerVariables.Set`: `HttpServerVarsCollection` reaches only an
  `IIS7WorkerRequest` for them; unassessed.

## Done when

Every adapter member reached by a milestone application either preserves its
transport contract or fails explicitly, and response spill/cleanup cannot pass
without exercising disk.

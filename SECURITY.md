# Security

Report a vulnerability privately through GitHub: open the repository's
**Security** tab and choose **Report a vulnerability**. Do not open a public
issue and do not include the details in a pull request.

- Supported: the latest published release only. A fix ships as a new package set;
  there are no patches to an older version.
- Response: an acknowledgement within 7 days, then a decision on scope and a
  fix or a documented boundary.
- Scope: the portable runtime and the packages under `src/`. Legacy
  serialization and resource payloads are trusted-input compatibility, not a
  security boundary ([PROJECT.md](PROJECT.md)); a report that an application's
  own `web.config` allows an unsafe setting is a documentation question, not
  a vulnerability, unless the runtime accepts a setting it claims to refuse.

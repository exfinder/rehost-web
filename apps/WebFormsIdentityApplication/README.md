# WebFormsIdentityApplication

The Visual Studio 2022 Web Forms template application with **Individual User
Accounts** authentication (register, login, external logins, two-factor,
password reset), frozen as generated.

| Folder | Role |
| --- | --- |
| `WebFormsIdentityApplication/` | The frozen .NET Framework 4.8.1 WAP. Never modified; stays buildable in Visual Studio on Windows. `bin/`, `obj/`, `packages/`, the `.sln`, and the LocalDb `App_Data/*.mdf|ldf` are not imported. |

No `.App`/`.Host` pair yet: the app pulls in OWIN, ASP.NET Identity 2.x, and
Entity Framework 6, none of which the ported packages cover. What it exercises
beyond [`WebFormsApplication`](../WebFormsApplication/README.md), and how each
piece could be closed, is mapped in
[`docs/research/webforms-identity-application-gaps.md`](../../docs/research/webforms-identity-application-gaps.md).

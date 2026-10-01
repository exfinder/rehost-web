# Web Forms with user accounts

The Visual Studio Web Forms template with registration, sign-in, and sign-out,
running on .NET 10. Accounts are stored in a local SQLite database;
no database server needed.

## Run it

Requires the .NET 10 SDK (10.0.302 or later) and a clone of this repository.
From the repository root:

```text
dotnet run --project apps/WebFormsIdentityApplication/WebFormsIdentityApplication.Host
```

Open [http://127.0.0.1:5082/](http://127.0.0.1:5082/).
The host creates the account database on first startup, under
`apps/WebFormsIdentityApplication/WebFormsIdentityApplication.Host/rehost_root/App_Data/Identity.db`.
Later runs keep the accounts in that file.

## Try it

- Open `/Account/Manage` while signed out: it redirects to the login page.
- Choose **Register**, enter an email and a password such as `Passw0rd!`,
  and create an account.
- Log off, then sign in with the same credentials.

## Limitations

- External login providers are disabled in the template.
- Email and SMS services are stubs. Password-reset and two-factor delivery
  need an implementation and have not been tested here.
- SQLite replaces the template's Windows-only LocalDB database.
  This example validates registration and sign-in against SQLite.

[Development notes](DEVELOPMENT.md) cover the database setup, package replacements,
configuration, and automated checks. For build or startup errors, see
[Troubleshooting](../../docs/troubleshooting.md).

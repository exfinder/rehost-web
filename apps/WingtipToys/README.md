# Wingtip Toys store

Microsoft's Web Forms tutorial store running on .NET 10 with SQL Server.
It demonstrates registration, sign-in, a shopping cart, administrator tools,
and checkout with simulated PayPal responses.

## Run it

Requires the .NET 10 SDK (10.0.302 or later), Docker running Linux containers,
and a clone of this repository. Run these commands from the repository root.

### 1. Start SQL Server

```text
docker run -d --name rehost-wingtip-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Rehost!Dev2026' -p 127.0.0.1:14333:1433 mcr.microsoft.com/mssql/server:2022-latest
```

The application already uses these database settings. Wait for SQL Server to
finish starting; `docker logs rehost-wingtip-sql` shows its progress.
On Apple silicon, the x64 image runs through emulation.
If you created this container earlier, use `docker start rehost-wingtip-sql`
instead.

### 2. Start the store

```text
dotnet run --project apps/WingtipToys/WingtipToys.Host
```

Open [http://127.0.0.1:5085/](http://127.0.0.1:5085/).
The first request creates and seeds the product and account databases,
so it may take longer than later visits.

## Try it

- Browse a product category and add products to the cart.
- Register an account, then change a quantity in the cart and click **Update**.
- Choose **Check out**. When the browser redirects to PayPal, return to
  `http://127.0.0.1:5085/Checkout/CheckoutReview` to continue the simulated
  checkout. Choose **Complete Order**, then check that the cart is empty.
- Log off and sign in as `canEditUser@wingtiptoys.com` with password `Pa$$word1`
  to see the administrator page.

Stop the host with **Ctrl+C** and the database with `docker stop rehost-wingtip-sql`.
Starting the same container again keeps the accounts and orders.

## Limitations

- PayPal responses come from a local simulator; no payment is made.
  The browser handoff is bypassed as described above. Real PayPal integration
  and checkout cancellation are untested.
- Google login uses placeholder credentials and fails at Google.
  Two-factor authentication and account-management flows are untested.
- The demo seeds an administrator account and uses a sample database password.
  A deployment needs its own credentials.
- Prices follow the host's culture. Admin product creation assumes decimal
  points and can fail under other cultures. Admin product removal is untested.

[Development notes](DEVELOPMENT.md) cover configuration, package replacements,
the PayPal simulator, and automated checks. For build or startup errors, see
[Troubleshooting](../../docs/troubleshooting.md).

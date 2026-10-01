# eShop catalog

Microsoft's eShopLegacyWebForms catalog manager running on .NET 10.
It demonstrates routed pages, data binding, dependency injection, and script
bundles. This example uses mock catalog data; no database needed.

## Run it

Requires the .NET 10 SDK (10.0.302 or later) and a clone of this repository.
From the repository root:

```text
dotnet run --project apps/eShopLegacyWebForms/eShopLegacyWebForms.Host
```

Open [http://127.0.0.1:5083/](http://127.0.0.1:5083/).

## Try it

- Browse the catalog and open a product's details.
- Open `/Catalog/Create`, `/Catalog/Edit/1`, and `/Catalog/Delete/1`
  to inspect the forms.
- Visit About and Contact from the navigation menu.

## Limitations

- Mock data replaces database access. The real EF6 database path is untested;
  the original LocalDB connection is Windows-only.
- Validation covers page rendering. Submitting create, edit, or delete forms
  and their validators is untested.
- Application Insights is removed. Log output, customization, and mobile views
  are untested.

[Development notes](DEVELOPMENT.md) cover configuration, dependency replacements,
and automated checks. For build or startup errors, see
[Troubleshooting](../../docs/troubleshooting.md).

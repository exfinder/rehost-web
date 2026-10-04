# eShop MVC catalog

Microsoft's eShopLegacyMVC catalog manager running on .NET 10.
It demonstrates MVC controllers and Razor views, a Web API controller,
dependency injection, and style bundles. This example uses mock catalog data;
no database needed.

## Run it

Requires the .NET 10 SDK (10.0.302 or later) and a clone of this repository.
From the repository root:

```text
dotnet run --project apps/eShopLegacyMVC/eShopLegacyMVC.Host
```

Open [http://127.0.0.1:5088/](http://127.0.0.1:5088/).

## Try it

- Browse the catalog and open a product's details.
- Edit a product and save it; the change shows in the catalog until restart.
- Open `/Catalog/Create` and `/Catalog/Delete/1` to inspect the forms.
- Request [`/api/brands`](http://127.0.0.1:5088/api/brands) for the brands as JSON.

## Limitations

- Mock data replaces database access. The real EF6 database path is untested;
  the original LocalDB connection is Windows-only.
- Validation covers browsing, editing and the brands API. Creating and deleting
  products, client-side validation and `/api/files` are untested.
- Application Insights is removed. Log output and customization data are untested.

[Development notes](DEVELOPMENT.md) cover configuration, dependency replacements,
and automated checks. For build or startup errors, see
[Troubleshooting](../../docs/troubleshooting.md).

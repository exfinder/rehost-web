# Web Forms template

The familiar Visual Studio Web Forms application, running on .NET 10.
It demonstrates master pages, postbacks, friendly URLs, and script and style
bundles. No database needed.

## Run it

Requires the .NET 10 SDK (10.0.302 or later) and a clone of this repository.
From the repository root:

```text
dotnet run --project apps/WebFormsApplication/WebFormsApplication.Host
```

Open [http://127.0.0.1:5081/](http://127.0.0.1:5081/).
The command builds the runtime and application before starting the site.

## Try it

- Browse Home, About, and Contact using the navigation links.
- Open `/Default.aspx` and watch it redirect to `/Default`.
- Narrow the browser window and try the collapsible navigation menu.

The [automated checks](DEVELOPMENT.md#commands) also exercise form postbacks,
script and style bundles, and mobile views.

## Where to go next

This is the plain template without user accounts or a database.
To run your own application, follow [Getting started](../../docs/getting-started.md).

[Development notes](DEVELOPMENT.md) cover the project layout, configuration,
publishing, and validation. For build or startup errors, see
[Troubleshooting](../../docs/troubleshooting.md).

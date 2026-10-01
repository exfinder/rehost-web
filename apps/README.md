# Example applications

Start with the Web Forms template for the quickest setup. All examples need
the .NET 10 SDK (10.0.302 or later) and a clone of this repository.
Each guide has run commands, things to try in the browser, and known limits.

| Example | What it demonstrates | Extra setup |
| --- | --- | --- |
| [Web Forms template](WebFormsApplication/README.md) | Master pages, postbacks, friendly URLs, script and style bundles | None |
| [Web Forms with user accounts](WebFormsIdentityApplication/README.md) | Registration and sign-in | SQLite database created automatically |
| [eShop catalog](eShopLegacyWebForms/README.md) | Routed catalog pages with mock data | None |
| [AJAX Control Toolkit](AjaxControlToolkitSampleSite/README.md) | Interactive controls and runtime page compilation | None |
| [Wingtip Toys store](WingtipToys/README.md) | Shopping cart, administrator tools, simulated checkout | Docker for SQL Server |
| [YAF forum](YAF/README.md) | Registration, topics, replies, moderation, search | Docker for PostgreSQL |

To run your own application, follow [Getting started](../docs/getting-started.md).

Each guide links to separate development notes for configuration, dependency
decisions, and validation evidence. See
[Will my app work?](../docs/what-works.md) for runtime compatibility.

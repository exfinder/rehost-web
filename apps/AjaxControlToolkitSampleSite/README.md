# AJAX Control Toolkit sample site

Interactive Web Forms controls running on .NET 10: calendars, accordions,
popups, autocomplete, and partial page updates.
This example uses the archived AJAX Control Toolkit v20.1 sample site.
Its page code compiles at runtime, demonstrating the ASP.NET Web Site model.

## Run it

Requires the .NET 10 SDK (10.0.302 or later) and a clone of this repository.
No database needed. From the repository root:

```text
dotnet run --project apps/AjaxControlToolkitSampleSite/AjaxControlToolkitSampleSite.Host
```

Open [http://127.0.0.1:5084/](http://127.0.0.1:5084/).
The first visit compiles the site's code; opening a new demo may also compile
its page.

## Try it

Use the site's menu to open these demos:

- **Accordion**: expand and collapse its sections.
- **Calendar**: focus a date field and choose a date.
- **AutoComplete**: type at least two characters to request suggestions.
- **ConfirmButton**: click **Click Me** and accept the confirmation;
  the result updates inside the page.

## Limitations

- Two unused bundle URLs return 404 in the browser console. The controls load
  their scripts and styles through other URLs.
- File uploads, Rating callbacks, and HTML sanitization are untested.
  The Twitter demo uses a defunct external API.
- Validation covers page rendering, autocomplete's service, and a ConfirmButton
  partial postback. Other demo postbacks are untested.
- This example includes extra build steps for Web Site projects and two
  application path fixes. It is not a ready-made migration template for that
  project type. Publishing and production bundle optimization remain unvalidated.

[Development notes](DEVELOPMENT.md) explain the configuration, fixes, and
validation results. For build or startup errors, see
[Troubleshooting](../../docs/troubleshooting.md).

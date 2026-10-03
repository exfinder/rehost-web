# BlogEngine.NET blog

A blog running on .NET 10 with SQLite storage: posts, pages, comments, feeds,
sign-in, and the Web API behind the admin panel. This example uses
[BlogEngine.NET](https://github.com/BlogEngine/BlogEngine.NET) 3.3.8.0, rebuilt
with compatibility changes. Its `BlogEngine.Core` class library is built as a
second project beside the web project.

## Run it

Requires the .NET 10 SDK (10.0.302 or later) and a clone of this repository.
From the repository root:

```text
dotnet run --project apps/BlogEngine/BlogEngine.Host
```

Open [http://127.0.0.1:5086/](http://127.0.0.1:5086/).
The build copies BlogEngine's sample database to
`apps/BlogEngine/BlogEngine.Host/rehost_root/App_Data/BlogEngine.s3db` when that
file is missing. Later builds and runs keep it; delete it to start over.

## Try it

- Open the welcome post and add a comment. The sample settings do not
  moderate comments, so it appears at once.
- Choose **Log in** and sign in as `Admin` with password `admin`,
  BlogEngine's documented defaults.
- In the admin panel, write a post or a page, then read it on the blog.
- Search the blog, or subscribe to the RSS feed at `/syndication.axd`.
- Sign out with the power icon in the admin sidebar.

Stop the host with **Ctrl+C**.

## Limitations

- This local demo uses BlogEngine's default administrator password and the
  fixed machine key from its `Web.Config`. A deployment needs its own
  credentials and [keys](../../docs/migration.md#machine-keys).
- `image.axd` resizes images with System.Drawing, which .NET supports only on
  Windows.
- The admin panel's **About** page (`admin/about.cshtml`) fails: it calls
  `WindowsIdentity.GetCurrent()`, which is Windows-only.
- Comment notifications go to the sample settings' placeholder mail server
  and fail in the background. Mail delivery is untested.
- Not tried: SQL Server and MySQL storage, the extension gallery,
  BlogML import and export, and BlogEngine's `BinaryFormatter` call in
  extension settings. The SQLite provider row is reached through
  `DbProviderFactories.GetFactory`; BlogEngine has no `SqlDataSource`, so a
  `ProviderName` lookup from one is not tried here.

[Development notes](DEVELOPMENT.md) cover the project layout, the changes
BlogEngine needed, configuration, and automated checks. The
[source table](../../docs/dev/sources.md) records the upstream revision and
license. For build or startup errors, see
[Troubleshooting](../../docs/troubleshooting.md).

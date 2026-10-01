# YAF forum

A forum running on .NET 10 with PostgreSQL: user registration,
sign-in, topics, replies, moderation, and Lucene search.
This example uses [YAF.NET](https://github.com/YAFNET/YAFNET) 3.2.16,
rebuilt with compatibility changes.

## Run it

Requires the .NET 10 SDK (10.0.302 or later), Docker running, and a clone of this repository.
Run these commands from the repository root.

### 1. Start PostgreSQL

```text
docker run -d --name rehost-yaf-pg -e POSTGRES_USER=yaf -e POSTGRES_PASSWORD='Rehost!Dev2026' -e POSTGRES_DB=yafnet -p 127.0.0.1:15432:5432 postgres:17-alpine
```

The application already uses these database settings.
If you created this container earlier, start it with `docker start rehost-yaf-pg`
instead.

### 2. Start the forum

```text
dotnet run --project apps/YAF/YAF.Host
```

Open [http://127.0.0.1:5087/](http://127.0.0.1:5087/).
The command builds the runtime and application before starting the site.

### 3. Create the board

On the first run, YAF opens its install wizard. Continue through the permissions
and connection checks, choose the `yafnet` connection, and initialize the database.
Use these values when creating the board:

| Field | Value |
| --- | --- |
| Board name | `Rehost Test Forum` |
| Forum email | `forum@rehost.test` |
| Base URL | `http://127.0.0.1:5087/` |
| Administrator username | `hostadmin` |
| Administrator email | `hostadmin@rehost.test` |
| Password | `Rehost!Dev2026` |

Finish the wizard to open the forum. Later runs use the existing board.

## Try it

- Sign in as `hostadmin` and create a topic.
- Register a member in a separate browser session.
  Verification emails are saved under
  `apps/YAF/YAF.Host/rehost_root/App_Data/mail/`; open the newest `.eml` in a
  mail viewer and follow its approval link.
- Sign in as the member and reply to the topic.
- As the administrator, delete the reply. Sign out to see the forum as a guest.

Stop the host with **Ctrl+C** and the database with `docker stop rehost-yaf-pg`.
Starting the same container again keeps the board and posts.

## Limitations

- This local demo uses sample passwords and a fixed test machine key.
  A deployment needs its own credentials and [keys](../../docs/migration.md#machine-keys).
- Registration mail goes to files; network mail delivery is untested.
- Image resizing and avatars are not portable off Windows. Attachments,
  private messages, multi-board creation, virtual-directory hosting, and upgrades
  are untested. Search is checked through its API; the browser search page is untested.

[Development notes](DEVELOPMENT.md) cover dependencies, configuration, SQL Server
setup, and automated checks. The [porting record](../../docs/dev/provenance/yafnet.md)
lists the source changes. For build or startup errors, see
[Troubleshooting](../../docs/troubleshooting.md).

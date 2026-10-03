# Web Forms template development

[Running guide](README.md) · [Package mapping](../../docs/migration.md#package-mapping)

The legacy WAP supplies source and content. `.App` compiles its application DLL;
`.Host` owns the Kestrel process. Shared staging and publish behavior is described
in [migration reference](../../docs/dev/migration-reference.md#app-and-host-layout).

## Commands

`apps/LocalFeed.props` and `apps/Directory.*` pack changed runtime projects before
restore; builds that skip required restore fail. Packages use Release regardless
of the application build configuration. Consumers need none of this local-feed
machinery.

```text
eng/app-linux-smoke.sh WebFormsApplication 5081
eng/external-consumer.sh artifacts/candidate/feed
```

The external-consumer runner copies the application outside the checkout and
restores only from its supplied feed and nuget.org.

## Configuration

The package XDT removes `runtime`, `system.codedom` and `system.serviceModel`;
staging then retargets the Optimization controls assembly. A Host-local `Web.Rehost.config` replaces that default. Publish applies
`Web.$(Configuration).config` first; development builds apply no configuration
transform. Edit staged content through its source and rebuild before refreshing.

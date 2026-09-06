# `<system.net><mailSettings>`

`system.net` is a BCL section group, not an IIS one. It fails here twice, and the
second failure is not ours to fix.

## Activation

The shipped `machine.config` does not declare the group, so an application that
merely contains `<system.net>` dies at `HttpRuntime.HostingInit` with
"Unrecognized configuration section system.net". YAF.NET carries `mailSettings`
and its host XDT deletes the group to activate at all.

## Mail, which stays dead either way

On Framework the parameterless constructor is the whole mechanism.
`SmtpClient()` calls `Initialize()`, which reads the section
(`System.dll` 4.8.9340.0, decompiled on the Windows validation host):

```csharp
if (MailConfiguration.Smtp != null)
{
    if (MailConfiguration.Smtp.Network != null)
    {
        if (host == null || host.Length == 0)
            host = MailConfiguration.Smtp.Network.Host;
        if (port == 0)
            port = MailConfiguration.Smtp.Network.Port;
        transport.Credentials = MailConfiguration.Smtp.Network.Credential;
        transport.EnableSsl   = MailConfiguration.Smtp.Network.EnableSsl;
        ...
    }
    deliveryFormat = MailConfiguration.Smtp.DeliveryFormat;
    deliveryMethod = MailConfiguration.Smtp.DeliveryMethod;
    if (MailConfiguration.Smtp.SpecifiedPickupDirectory != null)
        pickupDirectoryLocation = MailConfiguration.Smtp.SpecifiedPickupDirectory.PickupDirectoryLocation;
}
```

`MailSettingsSectionGroup` is `Sections["smtp"]` on a `ConfigurationSectionGroup`,
and `RuntimeConfig` reads the same path, `"system.net/mailSettings/smtp"`, as a
`SmtpSection`. Framework's own `LoginUtil` relies on it: `new SmtpClient();
smtp.Send(message);`, host never set.

Measured on .NET 10: `System.Net.Configuration.MailSettingsSectionGroup` does not
exist, and `new SmtpClient()` returns `Host` empty, `Port` 25. The block above is
gone, so declaring the section would fix activation and configure nothing. An
application whose mail is configured in `web.config` has no configured mail here,
including the network-free `SpecifiedPickupDirectory` mode.

`MailConfiguration` is internal and `ilspycmd -t` will not emit internal types, so
its body is unread; the evidence is its consumer above plus the public section group.

## Reached by

YAF.NET calls `new SmtpClient()` in `YAF.Core/Services/MailService.cs`, and every
user-creation path sends a verification mail, so no second user can be created
without an answer here ([app notes](../../apps/YAF/README.md)).

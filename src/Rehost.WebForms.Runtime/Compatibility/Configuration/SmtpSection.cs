// Derived from Microsoft Reference Source:
// System/net/System/Net/Configuration/{SmtpSection,SmtpNetworkElement,
// SmtpSpecifiedPickupDirectoryElement}.cs. MIT licensed by repository policy.

using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Net.Mail;

#nullable disable

namespace System.Net.Configuration;

public sealed class SmtpSection : ConfigurationSection
{
    private static readonly ConfigurationProperty s_deliveryMethod = new(
        "deliveryMethod", typeof(SmtpDeliveryMethod), SmtpDeliveryMethod.Network,
        new SmtpDeliveryMethodConverter(), null, ConfigurationPropertyOptions.None);
    private static readonly ConfigurationProperty s_deliveryFormat = new(
        "deliveryFormat", typeof(SmtpDeliveryFormat), SmtpDeliveryFormat.SevenBit,
        new SmtpDeliveryFormatConverter(), null, ConfigurationPropertyOptions.None);
    private static readonly ConfigurationProperty s_from = new("from", typeof(string));
    private static readonly ConfigurationProperty s_network = new("network", typeof(SmtpNetworkElement));
    private static readonly ConfigurationProperty s_pickupDirectory = new(
        "specifiedPickupDirectory", typeof(SmtpSpecifiedPickupDirectoryElement));
    private static readonly ConfigurationPropertyCollection s_properties =
        new() { s_deliveryMethod, s_deliveryFormat, s_from, s_network, s_pickupDirectory };

    [ConfigurationProperty("deliveryMethod", DefaultValue = SmtpDeliveryMethod.Network)]
    public SmtpDeliveryMethod DeliveryMethod
    {
        get => (SmtpDeliveryMethod)this[s_deliveryMethod];
        set => this[s_deliveryMethod] = value;
    }

    [ConfigurationProperty("deliveryFormat", DefaultValue = SmtpDeliveryFormat.SevenBit)]
    public SmtpDeliveryFormat DeliveryFormat
    {
        get => (SmtpDeliveryFormat)this[s_deliveryFormat];
        set => this[s_deliveryFormat] = value;
    }

    [ConfigurationProperty("from")]
    public string From
    {
        get => (string)this[s_from];
        set => this[s_from] = value;
    }

    [ConfigurationProperty("network")]
    public SmtpNetworkElement Network => (SmtpNetworkElement)this[s_network];

    [ConfigurationProperty("specifiedPickupDirectory")]
    public SmtpSpecifiedPickupDirectoryElement SpecifiedPickupDirectory =>
        (SmtpSpecifiedPickupDirectoryElement)this[s_pickupDirectory];

    protected override ConfigurationPropertyCollection Properties => s_properties;

    private sealed class SmtpDeliveryMethodConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) =>
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) =>
            value is string text
                ? text.ToLowerInvariant() switch
                {
                    "network" => SmtpDeliveryMethod.Network,
                    "specifiedpickupdirectory" => SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    "pickupdirectoryfromiis" => SmtpDeliveryMethod.PickupDirectoryFromIis,
                    _ => base.ConvertFrom(context, culture, value)
                }
                : base.ConvertFrom(context, culture, value);
    }

    private sealed class SmtpDeliveryFormatConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) =>
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) =>
            value is string text
                ? text.ToLowerInvariant() switch
                {
                    "sevenbit" => SmtpDeliveryFormat.SevenBit,
                    "international" => SmtpDeliveryFormat.International,
                    _ => base.ConvertFrom(context, culture, value)
                }
                : base.ConvertFrom(context, culture, value);
    }
}

public sealed class SmtpNetworkElement : ConfigurationElement
{
    private static readonly ConfigurationProperty s_defaultCredentials = new("defaultCredentials", typeof(bool), false);
    private static readonly ConfigurationProperty s_host = new("host", typeof(string));
    private static readonly ConfigurationProperty s_clientDomain = new("clientDomain", typeof(string));
    private static readonly ConfigurationProperty s_password = new("password", typeof(string));
    private static readonly ConfigurationProperty s_port = new(
        "port", typeof(int), 25, null, new IntegerValidator(IPEndPoint.MinPort + 1, IPEndPoint.MaxPort),
        ConfigurationPropertyOptions.None);
    private static readonly ConfigurationProperty s_userName = new("userName", typeof(string));
    private static readonly ConfigurationProperty s_targetName = new("targetName", typeof(string));
    private static readonly ConfigurationProperty s_enableSsl = new("enableSsl", typeof(bool), false);
    private static readonly ConfigurationPropertyCollection s_properties = new()
    {
        s_defaultCredentials, s_host, s_clientDomain, s_password, s_port, s_userName, s_targetName, s_enableSsl
    };

    [ConfigurationProperty("defaultCredentials", DefaultValue = false)]
    public bool DefaultCredentials { get => (bool)this[s_defaultCredentials]; set => this[s_defaultCredentials] = value; }

    [ConfigurationProperty("host")]
    public string Host { get => (string)this[s_host]; set => this[s_host] = value; }

    [ConfigurationProperty("targetName")]
    public string TargetName { get => (string)this[s_targetName]; set => this[s_targetName] = value; }

    [ConfigurationProperty("clientDomain")]
    public string ClientDomain { get => (string)this[s_clientDomain]; set => this[s_clientDomain] = value; }

    [ConfigurationProperty("password")]
    public string Password { get => (string)this[s_password]; set => this[s_password] = value; }

    [ConfigurationProperty("port", DefaultValue = 25)]
    public int Port { get => (int)this[s_port]; set => this[s_port] = value; }

    [ConfigurationProperty("userName")]
    public string UserName { get => (string)this[s_userName]; set => this[s_userName] = value; }

    [ConfigurationProperty("enableSsl", DefaultValue = false)]
    public bool EnableSsl { get => (bool)this[s_enableSsl]; set => this[s_enableSsl] = value; }

    protected override ConfigurationPropertyCollection Properties => s_properties;
}

public sealed class SmtpSpecifiedPickupDirectoryElement : ConfigurationElement
{
    private static readonly ConfigurationProperty s_location = new("pickupDirectoryLocation", typeof(string));
    private static readonly ConfigurationPropertyCollection s_properties = new() { s_location };

    [ConfigurationProperty("pickupDirectoryLocation")]
    public string PickupDirectoryLocation { get => (string)this[s_location]; set => this[s_location] = value; }

    protected override ConfigurationPropertyCollection Properties => s_properties;
}

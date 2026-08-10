namespace System.Web.Services {
    using System.Globalization;

    internal static class Res {
        internal const string ConfigKeyNotFoundInElementCollection = nameof(ConfigKeyNotFoundInElementCollection);
        internal const string ConfigKeysDoNotMatch = nameof(ConfigKeysDoNotMatch);

        internal static string GetString(string name, params object[] args) {
            string value = name switch {
                ConfigKeyNotFoundInElementCollection => "No elements matching the key {0} were found in the ConfigurationElementCollection.",
                ConfigKeysDoNotMatch => "The key does not match the indexer key. Key on element (expected value): {0}. Key provided to indexer: {1}.",
                _ => name,
            };
            return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
        }
    }
}

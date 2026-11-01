#pragma warning disable 0169, 0414, 0649
namespace HephaestusClient
{
    /// <summary>
    /// Read by BepInEx ConfigurationManager (F12) through reflection; only the field names matter.
    /// Category / DispName / Description are rewritten when the language changes.
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? ShowRangeAsPercent;
        public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
        public bool? Browsable;
        public string Category;
        public object DefaultValue;
        public bool? HideDefaultButton;
        public bool? HideSettingName;
        public string Description;
        public string DispName;
        public int? Order;
        public bool? ReadOnly;
        public bool? IsAdvanced;
    }
}

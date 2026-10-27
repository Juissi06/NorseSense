namespace NorseSense
{
    // Read by ConfigurationManager (by name, so no reference to it is needed): the order settings are listed in,
    // and which are "advanced" and hidden until its Advanced settings box is ticked.
#pragma warning disable 0649, 0414
    internal sealed class ConfigurationManagerAttributes
    {
        public int? Order;
        public bool? IsAdvanced;
        public bool? Browsable;
    }
#pragma warning restore 0649, 0414
}

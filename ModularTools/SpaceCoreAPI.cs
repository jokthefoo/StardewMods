using Microsoft.Xna.Framework;

namespace ModularTools
{
    public interface ISpaceCoreApi
    { 
        // Must have [XmlType("Mods_SOMETHINGHERE")] attribute (required to start with "Mods_")
        void RegisterSerializerType(Type type);
    }
    
    public class ObjectExtensionData
    {
        public string CategoryTextOverride { get; set; } = null;
        public Color CategoryColorOverride { get; set; } = new Color( 0, 0, 0, 0);

        public bool CanBeTrashed { get; set; } = true;
        public bool CanBeShipped { get; set; } = true;

        public int? EatenHealthRestoredOverride { get; set; } = null;
        public int? EatenStaminaRestoredOverride { get; set; } = null;

        public int? MaxStackSizeOverride { get; set; } = null;

        public class TotemWarpData
        {
            public string Location { get; set; }
            public Vector2 Position { get; set; }
            public Color Color { get; set; }
            public bool ConsumedOnUse { get; set; } = true;
        }
        public TotemWarpData TotemWarp { get; set; }

        public bool UseForTriggerAction { get; set; } = false;
        public bool ConsumeForTriggerAction { get; set; } = false;

        public string GiftedToNotOnAllowListMessage { get; set; }
        public Dictionary<string, bool> GiftableToNpcAllowList { get; set; }
        public Dictionary<string, string> GiftableToNpcDisallowList { get; set; }
    }
}

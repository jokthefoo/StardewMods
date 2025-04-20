using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Constants;
using StardewValley.GameData.Tools;
using StardewValley.Menus;
using StardewValley.Tools;

namespace FishMod
{
    [XmlType("Mods_Jok_DeluxeFishingRod")]
    public class PirateFishingRodTool : FishingRod
    {
        public const string ToolSpritesPseudoPath = "Mods/Willy/FishMod/ToolSprites";
        public const string DeluxeRodId = "Willy.FishMod.DeluxeFishingRod";
        public const string DeluxeRodQiid = ItemRegistry.type_tool + DeluxeRodId;
        
        public PirateFishingRodTool()
        {
            Name = "Pirate Fishing Rod";
            displayName = "Pirate Fishing Rod";
        }

        internal static void EditToolAssets(IDictionary<string, ToolData> data)
        {
            // Pirate rod has built in 15% treasure boost,
            // also enables the ability to get up to 3 treasure chests
            data[DeluxeRodId] = new ToolData
            {
                ClassName = "FishingRod",
                Name = "Pirate Fishing Rod",
                AttachmentSlots = 3,
                SalePrice = 25000,
                DisplayName = "Pirate Fishing Rod",
                Description = "Somehow this rod seems to find a lot more treasure.",
                Texture = ToolSpritesPseudoPath,
                SpriteIndex = 0,
                UpgradeLevel = 0
            };
        }
    }
}
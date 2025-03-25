using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Serialization;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Tools;
using StardewValley.Internal;
using StardewValley.Menus;
using StardewValley.Tools;

namespace FishMod
{
    [XmlType("Mods_spacechase0_DeluxeFishingRod")]
    public class DeluxeFishingRodTool : FishingRod
    {
        public const string ToolSpritesPseudoPath = "Mods/Willy/FishMod/ToolSprites";

        public const string DeluxeRodId = "Willy.FishMod.DeluxeFishingRod";
        public const string DeluxeRodQiid = ItemRegistry.type_tool + DeluxeRodId;

        public static List<int> randomTreasureNumbers = new();
        public DeluxeFishingRodTool()
        {
            Name = "Bop's Rod";
            displayName = "Bop's Rod";
        }

        public static int getAddedDistance(Farmer who)
        {
            if (who.FishingLevel >= 15)
                return 4;
            if (who.FishingLevel >= 8)
                return 3;
            if (who.FishingLevel >= 4)
                return 2;
            return who.FishingLevel >= 1 ? 1 : 0;
        }

        internal static void EditToolAssets(IDictionary<string, ToolData> data)
        {
            data[DeluxeRodId] = new ToolData
            {
                ClassName = "FishingRod",
                Name = "Bop's Rod",
                AttachmentSlots = 5,
                SalePrice = 0,
                DisplayName = "Bop's Rod",
                Description = "Bina boo is a big silly",
                Texture = ToolSpritesPseudoPath,
                SpriteIndex = 0,
                MenuSpriteIndex = -1,
                UpgradeLevel = 0,
                ConventionalUpgradeFrom = null,
                UpgradeFrom = null,
                CanBeLostOnDeath = false,
                SetProperties = null,
            };
        }

        private static IMonitor Monitor;

        internal static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }
        
        public static bool CheckIfValidBobberBar(IClickableMenu menu)
        {
            if (menu is BobberBar || menu is AdvBobberBar)
            {
                return true;
            }

            return false;
        }

        public static IEnumerable<CodeInstruction> bobberBar_Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            CodeMatcher matcher = new(instructions, generator);
            MethodInfo getActiveMenu = AccessTools.PropertyGetter(typeof(Game1), nameof(Game1.activeClickableMenu));
            MethodInfo bobberBarInstChecker =
                AccessTools.Method(typeof(DeluxeFishingRodTool), nameof(CheckIfValidBobberBar));

            matcher.MatchStartForward(
                    new CodeMatch(OpCodes.Call, getActiveMenu),
                    new CodeMatch(OpCodes.Brfalse_S),
                    new CodeMatch(OpCodes.Call, getActiveMenu),
                    new CodeMatch(OpCodes.Isinst),
                    new CodeMatch(OpCodes.Brtrue_S)
                )
                .ThrowIfNotMatch($"Could not find entry point for {nameof(bobberBar_Transpiler)}")
                .CreateLabelWithOffsets(4, out var labelToJumpTo)
                .Advance(3)
                .RemoveInstruction()
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Call, bobberBarInstChecker)
                );

            return matcher.InstructionEnumeration();
        }
        
        internal static void Post_openTreasureMenuEndFunction(FishingRod __instance, int remainingFish)
        {
            if (Game1.activeClickableMenu is not ItemGrabMenu menu)
            {
                Monitor.Log("Not item grab menu.", LogLevel.Warn);
                return;
            }

            if (menu.source != ItemGrabMenu.source_fishingChest)
                return;

            foreach (int i in randomTreasureNumbers)
            {
                switch (i)
                {
                    case 0: // blu chest
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create("(O)852", Game1.random.Next(1, 6))); // dragon tooth
                        break;
                    case 1: // red chest
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create("(O)260", Game1.random.Next(1, 30))); // hot pepper
                        break;
                    case 2: // green chest
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create("(O)60", Game1.random.Next(1, 2))); // emerald
                        break;
                }
            }
        }
    }
}
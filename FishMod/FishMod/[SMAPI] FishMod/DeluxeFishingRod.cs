using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Tools;
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
        public static TimeSpan minigameTimeToClick;
        
        public static float baseFishFrenzyChance = 0.02f;
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

            // TODO: different treasure chest rewards
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

        public static void PlayExclamationMark(Farmer who)
        {
            who.PlayFishBiteChime();
            Rumble.rumble(0.75f, 250f);

            minigameTimeToClick = Game1.currentGameTime.TotalGameTime + TimeSpan.FromMilliseconds(800);

            Point standingPixel = who.StandingPixel;
            Game1.screenOverlayTempSprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors",
                new Rectangle(395, 497, 3, 8),
                new Vector2(standingPixel.X - Game1.viewport.X, standingPixel.Y - 128 - 8 - Game1.viewport.Y), false,
                0.02f, Color.White)
            {
                scale = 5f,
                scaleChange = -0.01f,
                motion = new Vector2(0.0f, -0.5f),
                shakeIntensityChange = -0.005f,
                shakeIntensity = 1f
            });
        }

        public static void PlayHitEffectForRandomEncounter(Farmer who, IClickableMenu menu)
        {
            Game1.screenOverlayTempSprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(612, 1913, 74, 30), 1500f, 1, 0, Game1.GlobalToLocal(Game1.viewport, who.getStandingPosition() + new Vector2(-140f, -160f)), false, false, 1f, 0.005f, Color.White, 4f, 0.075f, 0.0f, 0.0f, true)
            {
                scaleChangeChange = -0.005f,
                motion = new Vector2(0.0f, -0.1f),
                endFunction = (TemporaryAnimatedSprite.endBehavior) (_ =>
                {
                    Game1.activeClickableMenu = menu;
                }),
                id = 987654321
            });
            Game1.player.playNearbySoundLocal("FishHit");
        }
    }
}
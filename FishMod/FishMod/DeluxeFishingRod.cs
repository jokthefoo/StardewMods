using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Tools;
using StardewValley.Menus;
using StardewValley.Tools;

namespace FishMod
{
    [XmlType("Mods_spacechase0_DeluxeFishingRod")]
    public class DeluxeFishingRodTool : FishingRod
    {
        public static Texture2D fishingTextures;
        
        public const string FishSpritesPath = "Assets/FishSprites.png";
        public const string ToolSpritesPseudoPath = "Mods/Willy/FishMod/ToolSprites";

        public const string DeluxeRodId = "Willy.FishMod.DeluxeFishingRod";
        public const string DeluxeRodQiid = ItemRegistry.type_tool + DeluxeRodId;

        public static int treasureCaughtCount;
        public static TimeSpan minigameTimeToClick;   
        public new static double baseChanceForTreasure = 0.15;
        
        public static float baseFishFrenzyChance = 0.03f;
        public DeluxeFishingRodTool()
        {
            Name = "Pirate Fishing Rod";
            displayName = "Pirate Fishing Rod";
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
        
        public static bool CheckIfValidBobberBar(IClickableMenu menu)
        {
            if (menu is BobberBar || menu is AdvBobberBar || menu is FishFrenzyBobberBar || menu is BossBobberBar || menu is SplitBobberBar || menu is DoubleFishBobberBar || menu is ShrinkingBobberBar)
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
                return;
            }

            if (menu.source != ItemGrabMenu.source_fishingChest)
                return;


            for(int i = 0; i < treasureCaughtCount; i++)
            {
                switch (i)
                {
                    case 1: // blue chest
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create("(O)MysteryBox",
                            Game1.random.Next(1, 3)));

                        if (Game1.random.NextDouble() < .7) // tackles
                        {
                            string[] tackles = { "(O)691", "(O)692", "(O)693", "(O)694", "(O)695", "(O)687"};
                            double[] probs = { 1/28f, 1/7f, 1/7f, 1/7f, 1/7f, 1/7f, 1/7f, 1/28f };
                            var tackle = MiningFishing.WeightedChoice(tackles, probs);
                            menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create(tackle));
                        }
                        
                        // aquamarine // ancient fruit // blue slime egg // rainbow shell // quality bobber // battery // seafoam pudding // solar panel
                        string[] objects = { "(O)62", "(O)454", "(O)413", "(O)394", "(O)877", "(O)787", "(O)265", "(BC)231" };
                        double[] probabilities = { 1/28f, 1/7f, 1/7f, 1/7f, 1/7f, 1/7f, 1/7f, 1/28f };
                        var winner = MiningFishing.WeightedChoice(objects, probabilities);
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create(winner));
                        
                        if (Game1.random.NextDouble() < .05)
                        {
                            menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create("(O)167", 100)); // joja cola
                        }

                        break;
                    case 2: // red chest
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create("(O)791")); // golden coconut
                        
                        // curiosity lure // dragon tooth // cinder shard // island warp totem // pina colada // tiger slime egg // treasure chest // deluxe fish tank
                        string[] redObjects = { "(O)856", "(O)852", "(O)848", "(O)886", "(O)873", "(O)857", "(O)166", "(F)2312" };
                        double[] redProbs = { 1/7f, 1/7f, 1/7f, 1/7f, 1/7f, 1/7f, 1/28f, 1/28f };
                        var redWinner = MiningFishing.WeightedChoice(redObjects, redProbs);
                        int amount = 1;
                        if (redWinner == redObjects[2]) // cinder shard
                        {
                            amount = 10;
                        }
                        if (redWinner == redObjects[4]) // warp totem
                        {
                            amount = 3;
                        }
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create(redWinner, amount));
                        break;
                    case 3: // green chest
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create("(O)60", Game1.random.Next(1, 5))); // emerald
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create("(O)909", Game1.random.Next(1, 5))); // radioactive ore
                        
                        // pearl // radioactive carp // galaxy soul // purple shorts // tea set // magic rock candy // prismatic shard // auto petter // coffee maker
                        string[] greenObjects = { "(O)797", "(O)901", "(O)896", "(O)789", "(O)341", "(O)279", "(O)74", "(BC)272", "(BC)246" };
                        double[] greenProb = { 1/9f, 1/9f, 1/9f, 1/18f, 1/28f, 1/9f, 1/9f, 1/18f, 1/9f };
                        var greenWinner = MiningFishing.WeightedChoice(greenObjects, greenProb);
                        menu.ItemsToGrabMenu.actualInventory.Add(ItemRegistry.Create(greenWinner));
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
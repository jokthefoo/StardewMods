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
    public class DeluxeFishingRodTool
    {
        public static Texture2D fishingTextures;
        
        public const string FishSpritesPath = "Assets/FishSprites.png";

        public static int treasureCaughtCount;
        public static TimeSpan minigameTimeToClick;   
        public new static double baseChanceForTreasure = 0.15;
        
        public static float baseFishFrenzyChance = 0.03f;
        
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

        public static void StartFishingMinigame(BobberBar bobberBar, MenuChangedEventArgs e)
        {

            string? baitid = "";
            List<string> tackles = new List<string>();
            if (Game1.player.CurrentTool is FishingRod fishingRod)
            {
                baitid = fishingRod.GetBait()?.QualifiedItemId;
                tackles = fishingRod.GetTackleQualifiedItemIDs();
            }
            else
            {
                return;
            }

            e.NewMenu.exitThisMenu(false);
            if (!bobberBar.bossFish && Game1.random.NextDouble() < DeluxeFishingRodTool.baseFishFrenzyChance)
            {
                Game1.activeClickableMenu = new FishFrenzyBobberBar(bobberBar.whichFish, bobberBar.fishSize,
                    bobberBar.bobbers, bobberBar.setFlagOnCatch, baitid);
                return;
            }

            double tackleBoost = Utility.getStringCountInList(tackles, "(O)693") *
                DeluxeFishingRodTool.baseChanceForTreasure / 3.0;
            double pirateTackleBoost = Utility.getStringCountInList(tackles, "(O){{ModId}}.PirateTreasureHunter") *
                                       DeluxeFishingRodTool.baseChanceForTreasure;

            double pirateRodBoost = 0;
            if (Game1.player.CurrentTool?.QualifiedItemId == PirateFishingRodTool.DeluxeRodQiid)
            {
                pirateRodBoost = DeluxeFishingRodTool.baseChanceForTreasure;
            }

            double baitBoost = baitid == "(O)703" ? DeluxeFishingRodTool.baseChanceForTreasure : 0.0;
            double pirateBoost =
                Game1.player.professions.Contains(9) ? DeluxeFishingRodTool.baseChanceForTreasure : 0.0;

            double treasureOdds = DeluxeFishingRodTool.baseChanceForTreasure + Game1.player.LuckLevel * 0.005 +
                                  baitBoost + tackleBoost + Game1.player.DailyLuck / 2.0 + pirateBoost +
                                  pirateTackleBoost + pirateRodBoost;

            bool blueTreasure = Game1.random.NextDouble() < treasureOdds;
            bool redTreasure = Game1.random.NextDouble() < treasureOdds;
            bool greenTreasure = false;
            if (pirateRodBoost > 0)
            {
                greenTreasure = Game1.random.NextDouble() < treasureOdds;
            }

            int treasureCount = (bobberBar.treasure ? 1 : 0) + (blueTreasure ? 1 : 0) + (redTreasure ? 1 : 0) +
                                (greenTreasure ? 1 : 0);

            if (treasureCount < 4 && Game1.random.NextDouble() < pirateRodBoost)
            {
                treasureCount++;
            }

            if (bobberBar.treasure && Game1.player.stats.Get(StatKeys.Mastery(1)) > 0U && Game1.random.NextDouble() <
                0.25 + Game1.player.team.AverageDailyLuck() + pirateTackleBoost)
                bobberBar.goldenTreasure = true;

            if (bobberBar.whichFish == "Jok.Fishdew.CP.Susebron")
            {
                Game1.activeClickableMenu = new BossBobberBar(bobberBar.whichFish, bobberBar.fishSize, treasureCount,
                    bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid,
                    bobberBar.goldenTreasure);
                return;
            }

            if (bobberBar.whichFish == "Jok.Fishdew.CP.BlueEel")
            {
                Game1.activeClickableMenu = new SplitBobberBar(bobberBar.whichFish, bobberBar.fishSize, treasureCount,
                    bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid,
                    bobberBar.goldenTreasure);
                return;
            }

            if (bobberBar.whichFish == "Jok.Fishdew.CP.RedDiscus")
            {
                Game1.activeClickableMenu = new DoubleFishBobberBar(bobberBar.whichFish, bobberBar.fishSize,
                    treasureCount,
                    bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid,
                    bobberBar.goldenTreasure);
                return;
            }

            var fishCaught = Game1.player.fishCaught;
            if (fishCaught != null && fishCaught.ContainsKey("Jok.Fishdew.CP.Susebron") &&
                bobberBar.whichFish == "Jok.Fishdew.CP.BlackDorado")
            {
                Game1.activeClickableMenu = new BossBobberBar(bobberBar.whichFish, bobberBar.fishSize, treasureCount,
                    bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid,
                    bobberBar.goldenTreasure);
                return;
            }

            if (bobberBar.whichFish == "Jok.Fishdew.CP.MidnightPufferfish")
            {
                Game1.activeClickableMenu = new ShrinkingBobberBar(bobberBar.whichFish, bobberBar.fishSize,
                    treasureCount,
                    bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid,
                    bobberBar.goldenTreasure);
                return;
            }
            Game1.activeClickableMenu = new AdvBobberBar(bobberBar.whichFish, bobberBar.fishSize, treasureCount,
                bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid, bobberBar.goldenTreasure);
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
            //Game1.player.UsingTool = true;
            Game1.player.CanMove = false;
            Game1.screenOverlayTempSprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(612, 1913, 74, 30), 500f, 1, 0, Game1.GlobalToLocal(Game1.viewport, who.getStandingPosition() + new Vector2(-140f, -160f)), false, false, 1f, 0.005f, Color.White, 4f, 0.075f, 0.0f, 0.0f, true)
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
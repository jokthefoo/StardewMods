using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Constants;
using StardewValley.GameData.Tools;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace FishMod
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.DayStarted += OnDayStarted;

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Display.MenuChanged += OnMenuChanged;

            DeluxeFishingRodTool.fishingTextures = helper.ModContent.Load<Texture2D>(DeluxeFishingRodTool.FishSpritesPath);
            Config = helper.ReadConfig<ModConfig>();
            HarmonyPatches();
        }

        private void HarmonyPatches()
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(Axe), nameof(Axe.DoFunction)),
                prefix: new HarmonyMethod(typeof(AxeFishing), nameof(AxeFishing.TreeChopping_prefix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(FishingRod), nameof(FishingRod.tickUpdate)),
                transpiler: new HarmonyMethod(typeof(DeluxeFishingRodTool),
                    nameof(DeluxeFishingRodTool.bobberBar_Transpiler))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(FishingRod), nameof(FishingRod.openTreasureMenuEndFunction)),
                postfix: new HarmonyMethod(typeof(DeluxeFishingRodTool),
                    nameof(DeluxeFishingRodTool.Post_openTreasureMenuEndFunction))
            );

            Type[] types = { typeof(Vector2), typeof(int), typeof(Farmer) };
            var originalToolsMethod = typeof(Tool).GetMethod("tilesAffected",
                BindingFlags.Instance | BindingFlags.NonPublic, null, types, null);
            harmony.Patch(
                original: originalToolsMethod,
                postfix: new HarmonyMethod(typeof(WateringCanFishing), nameof(WateringCanFishing.Post_tilesAffected)));


            harmony.Patch(
                original: AccessTools.Method(typeof(WateringCan), nameof(WateringCan.DoFunction)),
                postfix: new HarmonyMethod(typeof(WateringCanFishing),
                    nameof(WateringCanFishing.Post_wateringCanReleased))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.draw)),
                prefix: new HarmonyMethod(typeof(WateringCanFishing), nameof(WateringCanFishing.Pre_toolDraw))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.toolPowerIncrease)), // 600ms 
                postfix: new HarmonyMethod(typeof(WateringCanFishing), nameof(WateringCanFishing.Post_toolCharging))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Pickaxe), nameof(Pickaxe.DoFunction)),
                postfix: new HarmonyMethod(typeof(MiningFishing), nameof(MiningFishing.Post_PickaxeSwing))
            );
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            foreach (var mapping in WateringCanFishing.tilesToWaterNextDay)
            {
                foreach (Vector2 tile in mapping.Value)
                {
                    if (mapping.Key.terrainFeatures.ContainsKey(tile) && mapping.Key.terrainFeatures[tile] is HoeDirt)
                    {
                        (mapping.Key.terrainFeatures[tile] as HoeDirt).state.Value = 1;
                    }
                }
            }
            WateringCanFishing.tilesToWaterNextDay.Clear();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Config.WateringMiniGameEnabled)
            {
                return;
            }
            if (WateringCanFishing.chargingUpTime.CompareTo(TimeSpan.Zero) != 0 &&
                Game1.currentGameTime.TotalGameTime > WateringCanFishing.chargingUpTime &&
                Game1.player.CurrentTool is WateringCan)
            {
                WateringCanFishing.chargingUpTime = TimeSpan.Zero;

                WateringCanFishing.PlayToolIncreaseAnimation(Game1.player);
                WateringCanFishing.playerDidChargeUp = true;
            }
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(DeluxeFishingRodTool.ToolSpritesPseudoPath))
            {
                e.LoadFromModFile<Texture2D>("assets/ToolSprites.png", AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Tools"))
            {
                e.Edit(editor =>
                {
                    DeluxeFishingRodTool.EditToolAssets(editor.AsDictionary<string, ToolData>().Data);
                });
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var spacecore = Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
            
            if (spacecore is null)
            {
                Monitor.Log("No SpaceCore API found! Mod will not work!", LogLevel.Error);
            }
            else
            {
                spacecore.RegisterSerializerType(typeof(DeluxeFishingRodTool));
                Monitor.Log("Registered subclasses with SpaceCore!");
            }
            
            SetupModConfigs();
        }

        private void SetupModConfigs()
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            // add config options
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Axe MiniGame Enabled",
                tooltip: () => "Enables Axe MiniGame on trees and large stumps/logs.",
                getValue: () => Config.AxeMiniGameEnabled,
                setValue: value => Config.AxeMiniGameEnabled = value
            );
            
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Watering MiniGame Enabled",
                tooltip: () => "Enables Watering MiniGame after charging up watering can.",
                getValue: () => Config.WateringMiniGameEnabled,
                setValue: value => Config.WateringMiniGameEnabled = value
            );
            
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Mining MiniGame Enabled",
                tooltip: () => "Enables Mining MiniGame on special rock (still will spawn but doesn't do anything special).",
                getValue: () => Config.MiningMiniGameEnabled,
                setValue: value => Config.MiningMiniGameEnabled = value
            );
            
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Fishing Changes Enabled",
                tooltip: () => "Enables Extra fishing changes.",
                getValue: () => Config.FishingChangesEnabled,
                setValue: value => Config.FishingChangesEnabled = value
            );
        }
        public static ModConfig Config { get; set; }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is BobberBar bobberBar)
            {
                if (!Config.FishingChangesEnabled)
                {
                    return;
                }

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

                double tackleBoost = Utility.getStringCountInList(tackles, "(O)693") * DeluxeFishingRodTool.baseChanceForTreasure / 3.0;
                double pirateTackleBoost = Utility.getStringCountInList(tackles, "(O){{ModId}}.PirateTreasureHunter") * DeluxeFishingRodTool.baseChanceForTreasure;

                double pirateRodBoost = 0;
                if (Game1.player.CurrentTool?.QualifiedItemId == DeluxeFishingRodTool.DeluxeRodQiid)
                {
                    pirateRodBoost = DeluxeFishingRodTool.baseChanceForTreasure;
                }

                double baitBoost = baitid == "(O)703" ? DeluxeFishingRodTool.baseChanceForTreasure : 0.0;
                double pirateBoost = Game1.player.professions.Contains(9) ? DeluxeFishingRodTool.baseChanceForTreasure : 0.0;
                
                double treasureOdds = DeluxeFishingRodTool.baseChanceForTreasure + Game1.player.LuckLevel * 0.005 + baitBoost + tackleBoost + Game1.player.DailyLuck / 2.0 + pirateBoost + pirateTackleBoost + pirateRodBoost;

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
                
                if (bobberBar.treasure && Game1.player.stats.Get(StatKeys.Mastery(1)) > 0U && Game1.random.NextDouble() < 0.25 + Game1.player.team.AverageDailyLuck() + pirateTackleBoost)
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
                    Game1.activeClickableMenu = new DoubleFishBobberBar(bobberBar.whichFish, bobberBar.fishSize, treasureCount,
                        bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid,
                        bobberBar.goldenTreasure);
                    return;
                }
                //TODO: Jok.Fishdew.CP.BlackDorado -- rarer
                //TODO: Jok.Fishdew.CP.MidnightPufferfish
                    
                // TODO: dyeable fish ponds?
                // TODO: maybe animals? they would be so cute on the bar
                // TODO: after catching susebron maybe you can keep catching less valuable version? still do boss fight

                Game1.activeClickableMenu = new AdvBobberBar(bobberBar.whichFish, bobberBar.fishSize, treasureCount,
                    bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid, bobberBar.goldenTreasure);
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

            if (e.Button == SButton.MouseLeft && DeluxeFishingRodTool.minigameTimeToClick > Game1.currentGameTime.TotalGameTime)
            {
                DeluxeFishingRodTool.minigameTimeToClick = TimeSpan.Zero;
                IClickableMenu menu = new AdvBobberBar("734", 100, 3, new List<string>(), "nobait", false, "", true, 3);
                DeluxeFishingRodTool.PlayHitEffectForRandomEncounter(Game1.player, menu);
            }

            if (e.Button == SButton.N)
            {
                DeluxeFishingRodTool.PlayExclamationMark(Game1.player);
            }
            
            if (e.Button == SButton.B)
            {
                FishingRod.minFishingBiteTime = 100;
                FishingRod.maxFishingBiteTime = 100;
                Game1.player.gainExperience(Farmer.fishingSkill, 5000);
                Game1.player.gainExperience(Farmer.farmingSkill, 5000);
                Game1.player.gainExperience(Farmer.combatSkill, 5000);
                Game1.player.gainExperience(Farmer.foragingSkill, 5000);
                Game1.player.gainExperience(Farmer.miningSkill, 5000);
                
                var boprod = ItemRegistry.Create(DeluxeFishingRodTool.DeluxeRodQiid);
                boprod.specialItem = true;
                Game1.player.addItemByMenuIfNecessary(boprod);
                DeluxeFishingRodTool.baseChanceForTreasure = 2;
            }
            
            if (e.Button == SButton.F)
            {

                if (Game1.player.CurrentTool?.QualifiedItemId == DeluxeFishingRodTool.DeluxeRodQiid)
                {
                    if (Game1.player.CurrentTool is FishingRod fishingRod)
                    {
                        fishingRod.castingPower = 1f;
                        var num0 = Math.Max(128f, (float)((double)fishingRod.castingPower * (DeluxeFishingRodTool.getAddedDistance(Game1.player) + 4) * 64.0)) - 8f;
                        fishingRod.bobber.Set(new Vector2(
                            Game1.player.StandingPixel.X + (Game1.player.FacingDirection == 3 ? -1f : 1f) * num0,
                            Game1.player.StandingPixel.Y));
                    }
                }

                List<string> bobbers = new List<string>();

                var num1 = 1f * (1 / 5f);
                var num2 = 1 + Game1.player.FishingLevel / 2;
                var num3 = num1 * (Game1.random.Next(num2, Math.Max(6, num2)) / 5f);
                var fishSize = Math.Max(0.0f, Math.Min(1f, num3 * (float)(1.0 + Game1.random.Next(-10, 11) / 100.0)));
                
                //Game1.activeClickableMenu = new FishFrenzyBobberBar("136", fishSize, bobbers, "","nobait");
                //Game1.activeClickableMenu = new SplitBobberBar("136", fishSize, 3, bobbers, "");
                Game1.activeClickableMenu = new DoubleFishBobberBar("Jok.Fishdew.CP.RedDiscus", fishSize, 3, bobbers, "");
            }
            
            if (e.Button == SButton.G)
            {
                if (Game1.player.CurrentTool?.QualifiedItemId == DeluxeFishingRodTool.DeluxeRodQiid)
                {
                    if (Game1.player.CurrentTool is FishingRod fishingRod)
                    {
                        fishingRod.castingPower = 1f;
                        var num0 = Math.Max(128f, (float)((double)fishingRod.castingPower * (DeluxeFishingRodTool.getAddedDistance(Game1.player) + 4) * 64.0)) - 8f;
                        fishingRod.bobber.Set(new Vector2(
                            Game1.player.StandingPixel.X + (Game1.player.FacingDirection == 3 ? -1f : 1f) * num0,
                            Game1.player.StandingPixel.Y));
                        
                        Game1.activeClickableMenu = new BossBobberBar("Jok.Fishdew.CP.Susebron", 69, 1,
                            fishingRod.GetTackleQualifiedItemIDs(), "", true);
                    }
                }
                //Game1.activeClickableMenu = new MiningBobberBar(Game1.player.currentLocation, Game1.player.CurrentTool, new Vector2(0,0));
            }
        }
    }
}
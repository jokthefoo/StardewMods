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
            
            // Axe fishing
            harmony.Patch(
                original: AccessTools.Method(typeof(Axe), nameof(Axe.DoFunction)),
                prefix: new HarmonyMethod(typeof(AxeFishing), nameof(AxeFishing.TreeChopping_prefix))
            );
            
            // Normal Fishing
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

            // Watering can fishing
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

            // Mining Fishing
            harmony.Patch(
                original: AccessTools.Method(typeof(Pickaxe), nameof(Pickaxe.DoFunction)),
                postfix: new HarmonyMethod(typeof(MiningFishing), nameof(MiningFishing.Post_PickaxeSwing))
            );
            
            // Animal Fishing
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.checkForAction)),
                postfix: new HarmonyMethod(typeof(AnimalFishing), nameof(AnimalFishing.Post_CheckForAction))
            );
            
            Type[] drawTypes = { typeof(SpriteBatch),typeof(int), typeof(int), typeof(float) };
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.draw), drawTypes),
                prefix: new HarmonyMethod(typeof(AnimalFishing), nameof(AnimalFishing.Pre_draw))
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

                DeluxeFishingRodTool.StartFishingMinigame(bobberBar, e);
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

            if (Game1.activeClickableMenu != null)
            {
                return;
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
                
                Game1.activeClickableMenu = new FishFrenzyBobberBar("136", fishSize, bobbers, "","nobait");
                //Game1.activeClickableMenu = new SplitBobberBar("136", fishSize, 3, bobbers, "");
                //Game1.activeClickableMenu = new DoubleFishBobberBar("Jok.Fishdew.CP.RedDiscus", fishSize, 3, bobbers, "");
                //Game1.activeClickableMenu = new ShrinkingBobberBar("Jok.Fishdew.CP.MidnightPufferfish", fishSize, 0, bobbers, "");
            }
            
            if (e.Button == SButton.H)
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
                Game1.activeClickableMenu = new SplitBobberBar("136", fishSize, 3, bobbers, "");
                //Game1.activeClickableMenu = new DoubleFishBobberBar("Jok.Fishdew.CP.RedDiscus", fishSize, 3, bobbers, "");
                //Game1.activeClickableMenu = new ShrinkingBobberBar("Jok.Fishdew.CP.MidnightPufferfish", fishSize, 0, bobbers, "");
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
            
            if (e.Button == SButton.J)
            {
                Game1.activeClickableMenu = new MiningBobberBar(Game1.player.currentLocation, Game1.player.CurrentTool, new Vector2(0,0));
            }
        }
    }
}
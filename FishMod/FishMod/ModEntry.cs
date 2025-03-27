using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Tools;
using StardewValley.Menus;
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
            
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Display.MenuChanged += OnMenuChanged;
            
            ObjectIds.fishingTextures = helper.ModContent.Load<Texture2D>(ObjectIds.FishSpritesPath);
            
            var harmony = new Harmony(ModManifest.UniqueID);

            DeluxeFishingRodTool.Initialize(Monitor);
            harmony.Patch(
                original: AccessTools.Method(typeof(Axe), nameof(Axe.DoFunction)),
                prefix: new HarmonyMethod(typeof(AxeFishing), nameof(AxeFishing.TreeChopping_prefix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(FishingRod), nameof(FishingRod.tickUpdate)),
                transpiler: new HarmonyMethod(typeof(DeluxeFishingRodTool), nameof(DeluxeFishingRodTool.bobberBar_Transpiler))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(FishingRod), nameof(FishingRod.openTreasureMenuEndFunction)),
                postfix: new HarmonyMethod(typeof(DeluxeFishingRodTool), nameof(DeluxeFishingRodTool.Post_openTreasureMenuEndFunction))
            );
            
            Type[] types = { typeof ( Vector2 ), typeof ( int ), typeof ( Farmer ) };
            MethodInfo originalToolsMethod = typeof ( Tool ).GetMethod ( "tilesAffected", BindingFlags.Instance | BindingFlags.NonPublic, null, types, null );
            harmony.Patch(
                original: originalToolsMethod,
                postfix: new HarmonyMethod(typeof(DeluxeFishingRodTool), nameof(DeluxeFishingRodTool.Post_tilesAffected)) );
            
            
            harmony.Patch(
                original: AccessTools.Method(typeof(WateringCan), nameof(WateringCan.DoFunction)),
                postfix: new HarmonyMethod(typeof(DeluxeFishingRodTool), nameof(DeluxeFishingRodTool.Post_wateringCanReleased))
            );
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.toolPowerIncrease)),
                postfix: new HarmonyMethod(typeof(DeluxeFishingRodTool), nameof(DeluxeFishingRodTool.Post_toolCharging))
            );
            // 600ms
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (e.IsMultipleOf(60)) // run every second
            {
            }
            if (DeluxeFishingRodTool.chargingUpTime > Game1.currentGameTime.TotalGameTime && Game1.player.CurrentTool is FishingRod )
            {
                DeluxeFishingRodTool.chargingUpTime = TimeSpan.Zero;
                Game1.playSound("toolCharge",
                    Utility.CreateRandom(Game1.dayOfMonth, Game1.player.Position.X * 1000.0, Game1.player.Position.Y)
                        .Next(12, 16) * 100 + (Game1.player.CurrentTool?.UpgradeLevel+1) * 100);
                DeluxeFishingRodTool.playerDidChargeUp = true;
            }
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(ObjectIds.SpritesPseudoPath))
            {
                e.LoadFromModFile<Texture2D>("assets/Sprites.png", AssetLoadPriority.Exclusive);
            }  
            else if (e.NameWithoutLocale.IsEquivalentTo(DeluxeFishingRodTool.ToolSpritesPseudoPath))
            {
                e.LoadFromModFile<Texture2D>("assets/ToolSprites.png", AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(editor =>
                {
                    ObjectIds.EditAssets(editor.AsDictionary<string, ObjectData>().Data);
                });
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
                Monitor.Log("Registered subclasses with SpaceCore!", LogLevel.Trace);
            }
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is BobberBar bobberBar)
            {
                if (Game1.player.CurrentTool?.QualifiedItemId == DeluxeFishingRodTool.DeluxeRodQiid)
                {
                    string baitid = "";
                    if (Game1.player.CurrentTool is FishingRod fishingRod)
                    {
                        baitid = fishingRod.GetBait()?.QualifiedItemId;
                    }
                    e.NewMenu.exitThisMenu(false);
                    Game1.activeClickableMenu = new AdvBobberBar(bobberBar.whichFish, bobberBar.fishSize, 3, bobberBar.bobbers, bobberBar.setFlagOnCatch, bobberBar.bossFish, baitid, bobberBar.goldenTreasure);
                }
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
                DeluxeFishingRodTool.PlayHitEffectForRandomEncounter(Game1.player);
            }

            if (e.Button == SButton.N)
            {
                DeluxeFishingRodTool.PlayExclamationMark(Game1.player);
            }

            if (e.Button == SButton.H)
            {
                Game1.playSound("toolCharge",
                    Utility.CreateRandom(Game1.dayOfMonth, Game1.player.Position.X * 1000.0, Game1.player.Position.Y)
                        .Next(12, 16) * 100 + (Game1.player.CurrentTool?.UpgradeLevel+1) * 100); 
            }
            
            if (e.Button == SButton.B)
            {
                FishingRod.minFishingBiteTime = 100;
                FishingRod.maxFishingBiteTime = 100;
                Game1.player.gainExperience(Farmer.fishingSkill, 5000);
                
                var boprod = ItemRegistry.Create(DeluxeFishingRodTool.DeluxeRodQiid);
                boprod.specialItem = true;
                Game1.player.addItemByMenuIfNecessary(boprod);
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
                
                Game1.activeClickableMenu = new BasicBobberBar("136", fishSize, 3, bobbers, "nobait");
            }
        }
    }
}
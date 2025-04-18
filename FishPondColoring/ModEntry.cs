using System.ComponentModel;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewUI.Framework;

namespace FishPondColoring
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private static IViewEngine viewEngine;
        public static IModHelper Helper;
        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Helper = helper;
            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            //Helper.Events.Input.ButtonPressed += OnButtonPressed;
            HarmonyPatches();
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button == SButton.C)
            {
                FishPond pond = new FishPond();
                ShowColorPicker(pond);
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            viewEngine = Helper.ModRegistry.GetApi<IViewEngine>("focustense.StardewUI");
            viewEngine.RegisterViews("Mods/Jok.ColorfulPonds/Views", "assets/views");
            viewEngine.RegisterSprites("Mods/Jok.ColorfulPonds/Sprites", "assets/sprites");
            //viewEngine.EnableHotReloadingWithSourceSync();
        }

        private void HarmonyPatches()
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(PondQueryMenu), nameof(PondQueryMenu.draw), new Type[] { typeof(SpriteBatch) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Pond_draw_postfix))
            );
            
            harmony.Patch(
                original: AccessTools.Method(typeof(PondQueryMenu), nameof(PondQueryMenu.receiveLeftClick), new Type[] { typeof(int),typeof(int),typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Pond_receiveLeftClick_postfix))
            );

            harmony.Patch(
                original:  AccessTools.Method(typeof(PondQueryMenu), nameof(PondQueryMenu.performHoverAction)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Pond_performHoverAction_postfix))
            );
            
            harmony.Patch(
                original:  AccessTools.Method(typeof(FishPond), "doFishSpecificWaterColoring"),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Pond_doFishSpecificWaterColoring_postfix))
            );
            
            var parameters = new Type[] { typeof(FishPond) };
            harmony.Patch(
                original: AccessTools.Constructor(typeof(PondQueryMenu), parameters),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Pond_constructor_postfix))
            );
        }
        
        public static void ShowColorPicker(FishPond pond)
        {
            var model = new PondColorModel(pond);
            Game1.activeClickableMenu = viewEngine.CreateMenuFromAsset(
                $"Mods/Jok.ColorfulPonds/Views/ColorPicker",model);
        }

        public static ClickableTextureComponent changeColorButton;
        internal static void Pond_draw_postfix(PondQueryMenu __instance, SpriteBatch b)
        {
            if (changeColorButton == null)
                return;
            
            changeColorButton.draw(b);
            __instance.drawMouse(b);
        }
        
        internal static void Pond_receiveLeftClick_postfix(PondQueryMenu __instance, int x, int y, bool playSound)
        {
            if (changeColorButton == null)
                return;
            
            if (changeColorButton.containsPoint(x, y))
            {
                Game1.playSound("smallSelect", null);
                Color fishPondColor = new Color(Game1.random.Next(256), Game1.random.Next(256), Game1.random.Next(256));
                
                FishPond? pond = Traverse.Create(__instance).Field("_pond").GetValue() as FishPond;
                if (pond != null)
                {
                    ShowColorPicker(pond);
                }
            }
        }

        internal static void Pond_performHoverAction_postfix(PondQueryMenu __instance, int x, int y)
        {
            if (changeColorButton == null)
                return;

            if (changeColorButton.containsPoint(x, y))
            {
                changeColorButton.scale = Math.Min(4.1f, changeColorButton.scale + 0.05f);
                Traverse.Create(__instance).Field("hoverText").SetValue("Change Pond Color");
            }
            else
            {
                changeColorButton.scale = Math.Max(4f, __instance.emptyButton.scale - 0.05f);
            }
        }

        internal static void Pond_constructor_postfix(PondQueryMenu __instance, FishPond fish_pond)
        {
            changeColorButton = new ClickableTextureComponent(new Rectangle(__instance.xPositionOnScreen + PondQueryMenu.width + 4 + +4 + 64, __instance.yPositionOnScreen + PondQueryMenu.height - 192 + 4 - IClickableMenu.borderWidth, 64, 64), Game1.mouseCursors, new Rectangle(119, 469, 16, 16), 4f)
            {
                myID = 107,
                downNeighborID = -99998,
                upNeighborID = -99998
            };
        }

        public static void Pond_doFishSpecificWaterColoring_postfix(FishPond __instance)
        {
            string value;
            if (__instance.modData.TryGetValue("Jok.FishPondColor", out value))
            {
                uint result = uint.Parse(value);
                if (result != 0)
                {
                    __instance.overrideWaterColor.Value = new Color(result);
                }
            }
        }
    }
}
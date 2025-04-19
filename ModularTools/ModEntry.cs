using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace ModularTools
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        public static IModHelper Helper;
        
        public override void Entry(IModHelper helper)
        {
            Helper = helper;
            //Helper.Events.Input.ButtonPressed += OnButtonPressed;
            HarmonyPatches();
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button == SButton.C)
            {
                FishPond pond = new FishPond();
            }
        }

        private void HarmonyPatches()
        {
            var harmony = new Harmony(ModManifest.UniqueID);
        }
        
    }
}
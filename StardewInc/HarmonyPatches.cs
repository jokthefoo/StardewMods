using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using Object = StardewValley.Object;

namespace Jok.StardewInc;

internal static class HarmonyPatches
{
    public static void Patch(string modId)
    {
        var harmony = new Harmony(modId);
        
        /*
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.pressActionButton)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Game1_pressActionButton_postfix)));*/
        /*harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.performToolAction)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_performToolAction_postfix)));*/
    }
}
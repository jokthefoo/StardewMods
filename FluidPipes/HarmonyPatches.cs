using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Machines;
using Object = StardewValley.Object;

namespace FluidPipes;

internal static class HarmonyPatches
{
    public static void Patch(string modId)
    {
        var harmony = new Harmony(modId);
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), "CheckForActionOnMachine"),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_CheckForActionOnMachine_prefix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.performToolAction)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_performToolAction_prefix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.maximumStackSize)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_maximumStackSize_postfix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.canGrabSomethingFromHere)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Utility_canGrabSomethingFromHere_postfix)));
    }
    public static void Utility_canGrabSomethingFromHere_postfix(ref bool __result, int x, int y, Farmer who)
    {
        Vector2 tileLocation = new Vector2(x / 64, y / 64);
        if (Game1.currentLocation.Objects.TryGetValue(tileLocation, out var obj))
        {
            if (obj.readyForHarvest.Value && obj.heldObject.Value != null && obj.heldObject.Value.modData.TryGetValue(FluidData.LiquidKey, out var liquid))
            {
                Game1.mouseCursor = Game1.cursor_default;
                Game1.mouseCursorTransparency = 1f;
                __result = false;
            }
        }
    }

    public static void Object_maximumStackSize_postfix(Object __instance, ref int __result)
    {
        if (__instance.modData.TryGetValue(FluidData.LiquidKey, out var liquid))
        {
            __result = int.MaxValue;
        }
    }

    public static bool Object_CheckForActionOnMachine_prefix(Object __instance, Farmer who, bool justCheckingForActivity = false)
    {
        if (!__instance.readyForHarvest.Value)
        {
            return true;
        }

        if (__instance.heldObject.Value.modData.TryGetValue(FluidData.LiquidKey, out var liquid))
        {
            return false;
        }
        return true;
    }
    
    public static bool Object_performToolAction_prefix(Object __instance, ref bool __result, Tool t)
    {
        if (__instance != null && __instance.heldObject.Value != null && __instance.heldObject.Value.modData.TryGetValue(FluidData.LiquidKey, out var liquid))
        {
            __result = true;
            return false;
        }
        return true;
    }
}
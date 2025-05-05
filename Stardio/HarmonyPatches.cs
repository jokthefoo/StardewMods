using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using Object = StardewValley.Object;

namespace Stardio;

internal static class HarmonyPatches
{
    public static void Patch(string modId)
    {
        var harmony = new Harmony(modId);
        
        /*
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.pressActionButton)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Game1_pressActionButton_postfix)));*/
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), "CheckForActionOnMachine"),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_CheckForActionOnMachine_prefix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), "CheckForActionOnMachine"),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_CheckForActionOnMachine_postfix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Farmer), nameof(Farmer.MovePosition)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Farmer_MovePosition_postfix)));
        /*
        harmony.Patch(
            AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isCollidingPosition),
                new[] { typeof(Rectangle), typeof(xTile.Dimensions.Rectangle), typeof(bool), typeof(int), typeof(bool), typeof(Character), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GameLocation_isCollidingPosition_postfix)));
            */
    }

    public static void Farmer_MovePosition_postfix(Farmer __instance, GameTime time, xTile.Dimensions.Rectangle viewport, GameLocation currentLocation)
    {
        if (currentLocation == null)
        {
            return;
        }
        
        if (currentLocation.objects.TryGetValue(__instance.Tile, out Object obj) && obj is BeltItem belt)
        {
            float speedBuff = 0f;
            const float speedBuffValue = 2f; // TODO config these
            const float pushVelocity = 1.5f;
            switch (belt.currentRotation.Value)
            {
                case 0:
                    __instance.yVelocity = pushVelocity;
                    if (__instance.movementDirections.Contains(0))
                    {
                        speedBuff += speedBuffValue;
                    }
                    else if (__instance.movementDirections.Contains(2))
                    {
                        speedBuff -= speedBuffValue;
                    }
                    break;
                case 1:
                    __instance.xVelocity = pushVelocity;
                    if(__instance.movementDirections.Contains(1))
                    {
                        speedBuff += speedBuffValue;
                    } 
                    else if (__instance.movementDirections.Contains(3))
                    {
                        speedBuff -= speedBuffValue;
                    }
                    break;
                case 2:
                    __instance.yVelocity = -pushVelocity;
                    if(__instance.movementDirections.Contains(2))
                    {
                        speedBuff += speedBuffValue;
                    }
                    else if (__instance.movementDirections.Contains(0))
                    {
                        speedBuff -= speedBuffValue;
                    }
                    break;
                case 3:
                    __instance.xVelocity = -pushVelocity;
                    if(__instance.movementDirections.Contains(3))
                    {
                        speedBuff += speedBuffValue;
                    }
                    else if (__instance.movementDirections.Contains(1))
                    {
                        speedBuff -= speedBuffValue;
                    }
                    break;
            }
            __instance.temporarySpeedBuff = speedBuff;
        }
    }
    
    /*
    public static void GameLocation_isCollidingPosition_postfix(GameLocation __instance, ref bool __result, Rectangle position, xTile.Dimensions.Rectangle viewport, bool isFarmer, int damagesFarmer,
        bool glider, Character character, bool pathfinding, bool projectile = false, bool ignoreCharacterRequirement = false, bool skipCollisionEffects = false)
    {
        Vector2 nextTopRight = new Vector2(position.Right / 64, position.Top / 64);
        Vector2 nextTopLeft = new Vector2(position.Left / 64, position.Top / 64);
        Vector2 nextBottomRight = new Vector2(position.Right / 64, position.Bottom / 64);
        Vector2 nextBottomLeft = new Vector2(position.Left / 64, position.Bottom / 64);
        bool nextLargerThanTile = position.Width > 64;
        Vector2 nextBottomMid = new Vector2(position.Center.X / 64, position.Bottom / 64);
        Vector2 nextTopMid = new Vector2(position.Center.X / 64, position.Top / 64);
        
        string forecast = ModEntry.Helper.Reflection
            .GetMethod(new GameLocation(), "_TestCornersTiles")
            .Invoke<string>();
        
        __instance._TestCornersTiles(nextTopRight, nextTopLeft, nextBottomRight, nextBottomLeft, nextTopMid, nextBottomMid, null, null, null, null, null, null, nextLargerThanTile, delegate(Vector2 corner)
        {
            if (__instance.objects.TryGetValue(corner, out var obj) && !pathfinding && character != null && !skipCollisionEffects)
            {
                //obj.doCollisionAction(position, (int)((float)character.speed + character.addedSpeed), corner, character);
            }
            return false;
        });
    }*/
    
    public static void Object_CheckForActionOnMachine_prefix(Object __instance, out bool __state, Farmer who, bool justCheckingForActivity = false)
    {
        __state = __instance is BeltItem && __instance.heldObject.Value != null;
    }
    
    public static void Object_CheckForActionOnMachine_postfix(Object __instance, ref bool __result, bool __state, Farmer who, bool justCheckingForActivity = false)
    {
        if (__state && __instance is BeltItem belt && __instance.heldObject.Value == null)
        {
            belt.HeldItemPosition = 0;
        }
    }
}
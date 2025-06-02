using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace Jok.Stardio;

internal static class HarmonyPatches
{
    public static void Patch(string modId)
    {
        var harmony = new Harmony(modId);
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), "CheckForActionOnMachine"),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_CheckForActionOnMachine_prefix)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_CheckForActionOnMachine_postfix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Farmer), nameof(Farmer.MovePosition)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Farmer_MovePosition_postfix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.performToolAction)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_performToolAction_postfix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.minutesElapsed)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_minutesElapsed_prefix)));
    }
    
    public static bool Object_minutesElapsed_prefix(Object __instance, int minutes)
    {
        if (ModEntry.IsObjectDroneHub(__instance) || __instance.QualifiedItemId == ModEntry.FILTER_QID)
        {
            __instance.readyForHarvest.Value = false;
            return false;
        }
        return true;
    }

    public static void Object_performToolAction_postfix(Object __instance, ref bool __result, Tool t)
    {
        if (!Context.IsMainPlayer)
        {
            return;
        }
        
        var tileLoc = __instance.TileLocation;
        var location = __instance.Location;
        var state = MachineStateManager.GetState(location, tileLoc);
        if (__result && ModMachineState.IsValid(state))
        {
            foreach (var item in state.currentInventory)
            {
                location.debris.Add(new Debris(item, tileLoc * 64f + new Vector2(32f, 32f)));
            }
        }
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
            float speedBuffValue = ModEntry.Config.BeltPlayerSpeedBoost;
            float pushVelocity = ModEntry.Config.BeltPushPlayerSpeed;
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

        if (ModEntry.IsObjectDroneHub(__instance))
        {
            if (justCheckingForActivity)
            {
                __result = true;
                return;
            }
            if (__instance.heldObject.Value is Chest chest)
            {
                Game1.playSound("openChest");
                who.Halt();
                who.freezePause = 1000;
                Game1.activeClickableMenu = new ItemGrabMenu(chest.Items, reverseGrab: false, showReceivingMenu: true, InventoryMenu.highlightAllItems, chest.grabItemFromInventory, null, chest.grabItemFromChest, snapToBottom: false, canBeExitedWithKey: true, playRightClickSound: true, allowRightClick: true, showOrganizeButton: true, 1, null, -1, __instance);
                __result = true;
            }
        }
        
        if (__instance.QualifiedItemId == ModEntry.FILTER_QID)
        {
            if (__instance.heldObject.Value != null && justCheckingForActivity)
            {
                __result = true;
                return;
            }
            
            if (__instance.heldObject.Value != null && who.ActiveItem == null)
            {
                if (who.IsLocalPlayer)
                {
                    var outputObj = __instance.heldObject.Value;
                    __instance.heldObject.Value = null;
                    if (!who.addItemToInventoryBool(outputObj))
                    {
                        __instance.heldObject.Value = outputObj;
                        Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Crop.cs.588"));
                        __result = true;
                        return;
                    }
                    __result = true;
                    Game1.playSound("coin", null);
                }
            }
        }
    }
}
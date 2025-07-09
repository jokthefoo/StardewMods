using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Objects;
using StardewValley.Tools;
using xTile.Dimensions;
using Object = StardewValley.Object;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace BiggerMachines;

internal static class HarmonyPatches
{
    public static void Patch(string modId)
    {
        var harmony = new Harmony(modId);

        // Character collision
        harmony.Patch(
            AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isCollidingPosition),
                new[] { typeof(Rectangle), typeof(xTile.Dimensions.Rectangle), typeof(bool), typeof(int), typeof(bool), typeof(Character), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GameLocation_isCollidingPosition_postfix)));

        // Drawing
        harmony.Patch(AccessTools.Method(typeof(Object), nameof(Object.draw), new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_draw_prefix)));

        harmony.Patch(AccessTools.Method(typeof(Object), nameof(Object.drawWhenHeld)), 
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_drawWhenHeld_prefix)));

        harmony.Patch(
            AccessTools.Method(typeof(Object), nameof(Object.drawInMenu),
                new[] { typeof(SpriteBatch), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(StackDrawType), typeof(Color), typeof(bool) }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_drawInMenu_prefix)));

        // Placement bound checking
        harmony.Patch(AccessTools.Method(typeof(Object), nameof(Object.drawPlacementBounds)), 
            new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_drawPlacementBounds_prefix)));

        harmony.Patch(AccessTools.Method(typeof(Utility), nameof(Utility.playerCanPlaceItemHere)), 
            new HarmonyMethod(typeof(HarmonyPatches), nameof(Utility_playerCanPlaceItemHere_prefix)));

        harmony.Patch(AccessTools.Method(typeof(GameLocation), nameof(GameLocation.IsTileOccupiedBy)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GameLocation_IsTileOccupiedBy_postfix)));

        // Break object
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Tool), nameof(Tool.DoFunction)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Tool_DoFunction_postfix)));

        // Grab finished product
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.canGrabSomethingFromHere)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Utility_canGrabSomethingFromHere_postfix)));

        // Check for machine action
        harmony.Patch(AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.checkAction)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GameLocation_checkAction_postfix)));

        // Block furniture from placing inside bounds
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.canBePlacedHere)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Furniture_canBePlacedHere_postfix)));

        // Building alpha
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.updateWhenCurrentLocation)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_updateWhenCurrentLocation_postfix)));

        // Don't cull Bigger Machines when top left is out of frame
        // Skip bigger machine drawing
        harmony.Patch(AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.draw)), 
            transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(GameLocation_draw_transpiler)));
        // Draw bigger machines if any part is in frame
        harmony.Patch(AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.draw)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GameLocation_draw_postfix)));

        // Chest
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.placementAction)), 
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_placementAction_postfix)));

        harmony.Patch(AccessTools.DeclaredMethod(typeof(Chest), nameof(Chest.draw), new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
            new HarmonyMethod(typeof(HarmonyPatches), nameof(Chest_draw_prefix)));
    }

    public static bool Chest_draw_prefix(Chest __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
    {
        if (!ModEntry.BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData) || !bigMachineData.IsChest)
        {
            return true;
        }

        var animFrame = 0;
        var machineEffects = new MachineEffects();
        Object_draw_prefix(__instance, ref animFrame, ref machineEffects, spriteBatch, x, y, alpha);

        return false;
    }

    public static void Object_placementAction_postfix(Object __instance, ref bool __result, GameLocation location, int x, int y, Farmer who)
    {
        if (!__result || !__instance.bigCraftable.Value || !ModEntry.BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData) || !bigMachineData.IsChest)
        {
            return;
        }

        if (location is MineShaft || location is VolcanoDungeon)
        {
            __result = false;
            location.Objects[__instance.TileLocation] = null;
            Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.13053"));
            return;
        }

        var chest = new Chest(true, __instance.TileLocation, __instance.ItemId) { name = __instance.name, shakeTimer = 50 };
        location.objects[__instance.TileLocation] = chest;

        var bmChest = new BiggerMachine(chest, bigMachineData);

        if (!ModEntry.LocationBigMachines[location.Name].TryAdd(chest.TileLocation, bmChest))
        {
            ModEntry.MonitorInst.Log($"Jok.BiggerMachines tried to add chest at: {chest.TileLocation.ToString()}, but machine already at position", LogLevel.Error);
        }
    }

    public static IEnumerable<CodeInstruction> GameLocation_draw_transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        CodeMatcher matcher = new(instructions, generator);
        var tryGetValue = AccessTools.Method(typeof(OverlaidDictionary), nameof(OverlaidDictionary.TryGetValue), new[] { typeof(Vector2), typeof(Object) });
        var objectDrawTranspile = AccessTools.Method(typeof(HarmonyPatches), nameof(ObjectDrawTranspile));

        return matcher.MatchStartForward(new CodeMatch(OpCodes.Ldarg_0), // this
                new CodeMatch(OpCodes.Ldfld), // grab location.Objects
                new CodeMatch(OpCodes.Ldloc_S), // tileLocation
                new CodeMatch(OpCodes.Ldloca_S), // object
                new CodeMatch(OpCodes.Callvirt, tryGetValue), new CodeMatch(OpCodes.Brfalse_S)).ThrowIfNotMatch($"Could not find entry point for {nameof(GameLocation_draw_transpiler)}")
            .Advance(1) // keep this
            .RemoveInstruction() // remove location.Objects
            .Advance(2) // keep tileLocation and object
            .RemoveInstruction() // remove tryGetValue
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, objectDrawTranspile)).InstructionEnumeration();
    }

    public static void GameLocation_draw_postfix(GameLocation __instance, SpriteBatch b)
    {
        if (!ModEntry.LocationBigMachines.TryGetValue(__instance.Name, out var biggerMachines))
        {
            return;
        }

        foreach (var (pos, bm) in biggerMachines) bm.Object.draw(b, (int)pos.X, (int)pos.Y);
    }

    public static void Object_updateWhenCurrentLocation_postfix(Object __instance, GameTime time)
    {
        if (!__instance.bigCraftable.Value || !ModEntry.BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
        {
            return;
        }

        var currentAlpha = GetBiggerMachineAlpha(__instance);

        if (bigMachineData.Fade)
        {
            var itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
            var sourceRect = itemData.GetTexture().Bounds;
            var boundingBox = new Rectangle((int)__instance.TileLocation.X * 64, ((int)__instance.TileLocation.Y + -(sourceRect.Height / 16) + bigMachineData.Height) * 64, bigMachineData.Width * 64,
                (sourceRect.Height / 16 - bigMachineData.Height) * 64 + 32);

            if (Game1.player.GetBoundingBox().Intersects(boundingBox))
            {
                if (currentAlpha > 0.4f)
                {
                    currentAlpha = Math.Max(0.4f, currentAlpha - 0.04f);
                }

                __instance.modData[ModEntry.ModDataAlphaKey] = currentAlpha.ToString();
                return;
            }
        }

        if (currentAlpha < 1f)
        {
            currentAlpha = Math.Min(1f, currentAlpha + 0.05f);
            __instance.modData[ModEntry.ModDataAlphaKey] = currentAlpha.ToString();
        }
    }

    private static float GetBiggerMachineAlpha(Object obj)
    {
        var alpha = 1.0f;

        if (obj.modData.TryGetValue(ModEntry.ModDataAlphaKey, out var value))
        {
            if (float.TryParse(value, out var behindAlpha))
            {
                alpha = behindAlpha;
            }
        }
        else
        {
            obj.modData[ModEntry.ModDataAlphaKey] = "1.0";
        }

        return alpha;
    }

    public static void Furniture_canBePlacedHere_postfix(Furniture __instance, ref bool __result, GameLocation l, Vector2 tile, CollisionMask collisionMask = CollisionMask.All, bool showError = false)
    {
        if (!ModEntry.LocationBigMachines.ContainsKey(l.Name) || !__result)
        {
            return;
        }

        var bm = GetBiggerMachineAt(l.Name, tile);

        if (bm != null && (!bm.Object.isPassable() || !__instance.isPassable()))
        {
            __result = false;
        }
    }

    private static void GameLocation_checkAction_postfix(GameLocation __instance, ref bool __result, Location tileLocation, xTile.Dimensions.Rectangle viewport, Farmer who)
    {
        if (!ModEntry.LocationBigMachines.ContainsKey(__instance.Name) || __result)
        {
            return;
        }

        var bm = GetBiggerMachineAt(__instance.Name, new Vector2(tileLocation.X, tileLocation.Y));

        if (bm == null)
        {
            return;
        }

        if (who.ActiveObject == null && bm.Object.checkForAction(who))
        {
            __result = true;
            return;
        }

        if (who.CurrentItem != null)
        {
            var old_held_object = bm.Object.heldObject.Value;
            bm.Object.heldObject.Value = null;
            var probe_true = bm.Object.performObjectDropInAction(who.CurrentItem, true, who);
            bm.Object.heldObject.Value = old_held_object;
            var dropin_true = bm.Object.performObjectDropInAction(who.CurrentItem, false, who, true);

            if ((probe_true || dropin_true) && who.isMoving())
            {
                Game1.haltAfterCheck = false;
            }

            if (who.ignoreItemConsumptionThisFrame)
            {
                __result = true;
                return;
            }

            if (dropin_true)
            {
                who.reduceActiveItemByOne();
                __result = true;
                return;
            }

            __result = bm.Object.checkForAction(who) || probe_true;
            return;
        }

        __result = bm.Object.checkForAction(who);
    }

    private static void Utility_canGrabSomethingFromHere_postfix(ref bool __result, int x, int y, Farmer who)
    {
        if (Game1.currentLocation == null || !who.IsLocalPlayer || __result)
        {
            return;
        }

        if (!ModEntry.LocationBigMachines.ContainsKey(Game1.currentLocation.Name))
        {
            return;
        }

        var tilePosition = new Vector2(x / 64, y / 64);
        var bm = GetBiggerMachineAt(Game1.currentLocation.Name, tilePosition);

        if (bm != null)
        {
            if (bm.Object.readyForHarvest.Value)
            {
                Game1.mouseCursor = Game1.cursor_harvest;

                if (!Utility.withinRadiusOfPlayer(x, y, 1, who))
                {
                    Game1.mouseCursorTransparency = 0.5f;
                    return;
                }

                __result = true;
            }
        }
    }

    private static void Tool_DoFunction_postfix(Tool __instance, GameLocation location, int x, int y, int power, Farmer who)
    {
        if (!ModEntry.LocationBigMachines.ContainsKey(location.Name) || !__instance.isHeavyHitter() || __instance is MeleeWeapon)
        {
            return;
        }

        var tilePosition = new Vector2(x / 64, y / 64);
        var bm = GetBiggerMachineAt(location.Name, tilePosition);

        if (bm != null && bm.Object.performToolAction(__instance))
        {
            if (bm.Object.Type == "Crafting" && bm.Object.Fragility != 2)
            {
                location.debris.Add(new Debris(bm.Object.QualifiedItemId, who.GetToolLocation(), Utility.PointToVector2(who.StandingPixel)));
            }

            bm.Object.performRemoveAction();
            location.Objects.Remove(bm.Object.TileLocation);
        }
    }

    public static void GameLocation_IsTileOccupiedBy_postfix(GameLocation __instance, ref bool __result, Vector2 tile, CollisionMask collisionMask = CollisionMask.All,
        CollisionMask ignorePassables = CollisionMask.None, bool useFarmerTile = false)
    {
        if (!ModEntry.LocationBigMachines.ContainsKey(__instance.Name) || __result || !collisionMask.HasFlag(CollisionMask.Objects))
        {
            return;
        }

        var bm = GetBiggerMachineAt(__instance.Name, tile);

        if (bm != null && (!ignorePassables.HasFlag(CollisionMask.Objects) || !bm.Object.isPassable()))
        {
            __result = true;
        }
    }

    private static bool ObjectDrawTranspile(GameLocation location, Vector2 tile, out Object value)
    {
        value = null;

        if (location.objects.TryGetValue(tile, out var o))
        {
            if (o.bigCraftable.Value && ModEntry.BigMachinesList.TryGetValue(o.ItemId, out var bigMachineData))
            {
                return false;
            }

            value = o;
            return true;
        }

        return false;
    }

    private static BiggerMachine? GetBiggerMachineAt(string locationName, Vector2 tile)
    {
        var position = default(Point);
        position.X = (int)((int)tile.X + 0.5f) * 64;
        position.Y = (int)((int)tile.Y + 0.5f) * 64;

        foreach (var (pos, bm) in ModEntry.LocationBigMachines[locationName])
            if (bm.GetBoundingBox().Contains(position))
            {
                return bm;
            }

        return null;
    }

    public static bool Utility_playerCanPlaceItemHere_prefix(GameLocation location, Item item, int x, int y, Farmer f, ref bool __result)
    {
        if (item is null || !ModEntry.BigMachinesList.TryGetValue(item.ItemId, out var bigMachineData))
        {
            return true;
        }

        if (Utility.isPlacementForbiddenHere(location) || Game1.eventUp || f.bathingClothes.Value || f.onBridge.Value)
        {
            __result = false;
            return false;
        }

        var rect = new Rectangle(x, y, bigMachineData.Width * 64, bigMachineData.Height * 64);
        rect.Inflate(96, 96);

        if (!rect.Contains(f.StandingPixel))
        {
            __result = false;
            return false;
        }

        var tileLocation = new Vector2(x / 64, y / 64);

        for (var ix = (int)tileLocation.X; ix < (int)tileLocation.X + bigMachineData.Width; ++ix)
        {
            for (var iy = (int)tileLocation.Y; iy < (int)tileLocation.Y + bigMachineData.Height; ++iy)
            {
                if (!item.canBePlacedHere(location, new Vector2(ix, iy)))
                {
                    __result = false;
                    return false;
                }
            }
        }

        __result = item.isPlaceable();
        return false;
    }

    // Collision
    public static void GameLocation_isCollidingPosition_postfix(GameLocation __instance, ref bool __result, Rectangle position, xTile.Dimensions.Rectangle viewport, bool isFarmer, int damagesFarmer,
        bool glider, Character character, bool pathfinding, bool projectile = false, bool ignoreCharacterRequirement = false, bool skipCollisionEffects = false)
    {
        if (!ModEntry.LocationBigMachines.ContainsKey(__instance.Name) || __result)
        {
            return;
        }

        var farmer = character as Farmer;
        Rectangle? currentBounds;

        if (farmer != null)
        {
            currentBounds = farmer.GetBoundingBox();
        }
        else
        {
            currentBounds = null;
        }

        foreach (var (pos, bm) in ModEntry.LocationBigMachines[__instance.Name])
            if (bm.IntersectsForCollision(position) && (!currentBounds.HasValue || !bm.IntersectsForCollision(currentBounds.Value)))
            {
                __result = true;
                return;
            }
    }

    // The real draw
    private static bool Object_draw_prefix(Object __instance, ref int ____machineAnimationFrame, ref MachineEffects ____machineAnimation, SpriteBatch spriteBatch, int x, int y, float alpha)
    {
        if (__instance.isTemporarilyInvisible || !__instance.bigCraftable.Value || !ModEntry.BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
        {
            return true;
        }

        var scaleFactor = __instance.getScale() * 4f;
        var position = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64));
        var itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
        var drawHeightOffset = itemData.GetTexture().Height * 4 - bigMachineData.Height * 64;
        var dest = position - new Vector2(0, drawHeightOffset) + scaleFactor / 2f + (__instance.shakeTimer > 0 ? new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2)) : Vector2.Zero);

        var draw_layer = Math.Max(0f, ((y + 1) * 64 - 24) / 10000f) + x * 1E-05f;
        var offset = 0;

        if (__instance.showNextIndex.Value)
        {
            offset = 1;
        }

        if (____machineAnimationFrame >= 0 && ____machineAnimation != null)
        {
            offset = ____machineAnimationFrame;
        }

        alpha *= GetBiggerMachineAlpha(__instance);

        if (bigMachineData.DrawShadow)
        {
            drawShadow(__instance, bigMachineData, alpha, spriteBatch);
        }

        var sourceRect = new Rectangle(bigMachineData.Width * 16 * offset, 0, bigMachineData.Width * 16, itemData.GetTexture().Height);
        spriteBatch.Draw(itemData.GetTexture(), dest, sourceRect, Color.White * alpha, 0, Vector2.Zero, 4, SpriteEffects.None, draw_layer);

        if (__instance.isLamp.Value && Game1.isDarkOut(__instance.Location))
        {
            spriteBatch.Draw(Game1.mouseCursors, position + new Vector2(-32f, -32f), new Rectangle(88, 1779, 32, 32), Color.White * 0.75f, 0f, Vector2.Zero, 4f, SpriteEffects.None,
                Math.Max(0f, ((y + 1) * 64 - 20) / 10000f) + x / 1000000f);
        }

        // Draw code for machine bubble:
        if (!__instance.readyForHarvest.Value)
        {
            return false;
        }

        var bmOffset = new Vector2(16 * bigMachineData.Width, 0);

        var base_sort = (y + 1) * 64 / 10000f + __instance.TileLocation.X / 50000f;

        if (__instance.IsTapper() || __instance.QualifiedItemId.Equals("(BC)MushroomLog"))
        {
            base_sort += 0.02f;
        }

        var yOffset = 4f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2);
        // White bubble
        spriteBatch.Draw(Game1.mouseCursors, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 - 8, y * 64 - 96 - 16 + yOffset) + bmOffset), new Rectangle(141, 465, 20, 24), Color.White * 0.75f,
            0f, Vector2.Zero, 4f, SpriteEffects.None, base_sort + 1E-06f);

        if (__instance.heldObject.Value == null)
        {
            return false;
        }

        if (__instance.heldObject.Value is ColoredObject coloredObj)
        {
            coloredObj.drawInMenu(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 - 96f - 8f + yOffset) + bmOffset), 1f, 0.75f, base_sort + 1.1E-05f);
            return false;
        }

        var heldItemData = ItemRegistry.GetDataOrErrorItem(__instance.heldObject.Value.QualifiedItemId);
        spriteBatch.Draw(heldItemData.GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 - 64 - 8 + yOffset) + bmOffset), heldItemData.GetSourceRect(),
            Color.White * 0.75f, 0f, new Vector2(8f, 8f), 4f, SpriteEffects.None, base_sort + 1E-05f);

        var drawType = StackDrawType.Hide;

        if (__instance.heldObject.Value.Stack > 1)
        {
            drawType = StackDrawType.Draw;
        }
        else if (__instance.heldObject.Value.Quality > 0)
        {
            drawType = StackDrawType.HideButShowQuality;
        }

        __instance.heldObject.Value.DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 - 64 - 32 + yOffset - 4f) + bmOffset), 1f, 1f, base_sort + 1.2E-05f,
            drawType, Color.White);
        return false;
    }

    private static bool Object_drawWhenHeld_prefix(Object __instance, SpriteBatch spriteBatch, Vector2 objectPosition, Farmer f)
    {
        if (!__instance.bigCraftable.Value || !ModEntry.BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
        {
            return true;
        }

        var itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
        var sourceRect = new Rectangle(0, 0, bigMachineData.Width * 16, itemData.GetTexture().Height);
        spriteBatch.Draw(itemData.GetTexture(), objectPosition - new Vector2(0, itemData.GetTexture().Height * 4f) + new Vector2(0, 124), sourceRect, Color.White, 0,
            new Vector2(bigMachineData.Width / 2f * 16 - 8, 0), 4f, SpriteEffects.None, Math.Max(0.0f, (f.getStandingPosition().Y + 3) / 10000f));
        return false;
    }

    private static bool Object_drawInMenu_prefix(Object __instance, SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency, float layerDepth, StackDrawType drawStackNumber,
        Color color, bool drawShadow)
    {
        if (!__instance.bigCraftable.Value || !ModEntry.BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
        {
            return true;
        }

        if (__instance.IsRecipe)
        {
            transparency = 0.5f;
            scaleSize *= 0.75f;
        }

        var itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
        var scale = Math.Min(4f * 16 / itemData.GetTexture().Height, 4f * bigMachineData.Width) * scaleSize;
        var sourceRect = new Rectangle(0, 0, bigMachineData.Width * 16, itemData.GetTexture().Height);
        spriteBatch.Draw(itemData.GetTexture(), location + new Vector2(32f, 32f), sourceRect, color * transparency, 0f, new Vector2(sourceRect.Width / 2, sourceRect.Height / 2), scale,
            SpriteEffects.None, layerDepth);
        __instance.DrawMenuIcons(spriteBatch, location, scaleSize, transparency, layerDepth, drawStackNumber, color);
        return false;
    }

    private static bool Object_drawPlacementBounds_prefix(Object __instance, SpriteBatch spriteBatch, GameLocation location)
    {
        if (!__instance.bigCraftable.Value || !ModEntry.BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
        {
            return true;
        }

        Game1.isCheckingNonMousePlacement = !Game1.IsPerformingMousePlacement();
        var x = (int)Game1.GetPlacementGrabTile().X * 64;
        var y = (int)Game1.GetPlacementGrabTile().Y * 64;

        if (Game1.isCheckingNonMousePlacement)
        {
            var nearbyValidPlacementPosition = Utility.GetNearbyValidPlacementPosition(Game1.player, location, __instance, x, y);
            x = (int)nearbyValidPlacementPosition.X;
            y = (int)nearbyValidPlacementPosition.Y;
        }

        var tile = new Vector2(x / 64, y / 64);

        if (__instance.Equals(Game1.player.ActiveObject))
        {
            __instance.TileLocation = tile;
        }

        var canPlaceHere = Utility.playerCanPlaceItemHere(location, __instance, x, y, Game1.player) ||
                           (Utility.isThereAnObjectHereWhichAcceptsThisItem(location, __instance, x, y) && Utility.withinRadiusOfPlayer(x, y, 1, Game1.player));
        Game1.isCheckingNonMousePlacement = false;

        for (var x_offset = 0; x_offset < bigMachineData.Width; ++x_offset)
        {
            for (var y_offset = 0; y_offset < bigMachineData.Height; ++y_offset)
            {
                spriteBatch.Draw(Game1.mouseCursors, new Vector2((tile.X + x_offset) * 64f - Game1.viewport.X, (tile.Y + y_offset) * 64f - Game1.viewport.Y),
                    new Rectangle(canPlaceHere ? 194 : 210, 388, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.01f);
            }
        }

        __instance.draw(spriteBatch, (int)tile.X, (int)tile.Y, 0.5f);
        return false;
    }

    public static void drawShadow(Object obj, BiggerMachineData bigMachineData, float alpha, SpriteBatch b)
    {
        var basePosition = Game1.GlobalToLocal(new Vector2(obj.TileLocation.X * 64, (obj.TileLocation.Y + bigMachineData.Height) * 64));
        b.Draw(Game1.mouseCursors, basePosition, Building.leftShadow, Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1E-05f);

        for (var x = 1; x < bigMachineData.Width - 1; x++)
        {
            b.Draw(Game1.mouseCursors, basePosition + new Vector2(x * 64, 0f), Building.middleShadow, Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1E-05f);
        }

        b.Draw(Game1.mouseCursors, basePosition + new Vector2((bigMachineData.Width - 1) * 64, 0f), Building.rightShadow, Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1E-05f);
    }
}
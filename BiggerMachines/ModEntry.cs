using BiggerMachines;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Machines;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.Tools;
using xTile.Dimensions;
using Object = StardewValley.Object;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace BiggerMachines
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        public static Mod Instance;
        public static IMonitor MonitorInst;
        //  debug logging:  MonitorInst.Log($"X value: {x}", LogLevel.Info);
        public static IModHelper Helper;
        
        public static Dictionary<string, List<BiggerMachine>> LocationBigMachines = new();
        public static Dictionary<string, BiggerMachineData> BigMachinesList = new();

        public static string ModDataDimensionsKey = "Jok.BiggerMachines.Dimensions";
        public static string ModDataAlphaKey = "Jok.BiggerMachines.Alpha";
        public static string ModDataFadeBehindKey = "Jok.BiggerMachines.EnableTransparency";
        public static string ModDataShadowKey = "Jok.BiggerMachines.DrawShadow";
        
        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Instance = this;
            MonitorInst = Monitor;
            Helper = helper;
            I18n.Init(Helper.Translation);
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Content.AssetRequested += OnAssetRequested;
            Helper.Events.World.ObjectListChanged += OnObjectListChanged;
            
            HarmonyPatches();
        }

        private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
        {
            var location = e.Location;
            
            if (!LocationBigMachines.ContainsKey(location.Name))
            {
                LocationBigMachines.TryAdd(location.Name, new List<BiggerMachine>());
            }

            foreach (var (pos, obj) in e.Added)
            {
                if (!obj.bigCraftable.Value || !BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData))
                {
                    continue;
                }
                
                BiggerMachine bm = new BiggerMachine(obj, bigMachineData);
                LocationBigMachines[location.Name].Add(bm);
            }
            
            foreach (var (pos, obj) in e.Removed)
            {
                if (!obj.bigCraftable.Value || !BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData))
                {
                    continue;
                }
                
                foreach (var bm in LocationBigMachines[location.Name])
                {
                    if (bm.IntersectsForCollision(obj.boundingBox.Value))
                    {
                        LocationBigMachines[location.Name].Remove(bm);
                        return;
                    }
                }
            }
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, BigCraftableData>().Data;
                    foreach (var (itemid, bc) in data)
                    {
                        if (bc.CustomFields == null)
                        {
                            continue;
                        }
                        
                        if (bc.CustomFields.TryGetValue(ModDataDimensionsKey, out string? value))
                        {
                            string[] dims = value.Split(",");
                            if(Int32.TryParse(dims[0], out var width) && Int32.TryParse(dims[1], out var height))
                            {
                                bool hasFading = false;
                                if (bc.CustomFields.TryGetValue(ModDataFadeBehindKey, out string? fadeBehind))
                                {
                                    if (fadeBehind.ToLower() == "true")
                                    {
                                        hasFading = true;
                                    }
                                }
                                
                                bool drawShadow = false;
                                if (bc.CustomFields.TryGetValue(ModDataShadowKey, out string? shadow))
                                {
                                    if (shadow.ToLower() == "true")
                                    {
                                        drawShadow = true;
                                    }
                                }
                                BigMachinesList.Add(itemid, new BiggerMachineData(width, height, hasFading, drawShadow));
                            }
                        }
                        
                    }
                }, AssetEditPriority.Late);
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button == SButton.C)
            {
            }
        }
        // TODO maybe: Chest.CheckAutoLoad  --- (hopper)
        // TODO maybe: workbench -- objects.TryGetValue(  --- grabs nearby chests
        // TODO maybe: Game1 -- pressusetoolbutton  --- can pop machines without tool
        // TODO maybe: right click placing only works to the right
        private void HarmonyPatches()
        {
            var harmony = new Harmony(ModManifest.UniqueID);

            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isCollidingPosition),
                    new Type[] { typeof(Rectangle), typeof(xTile.Dimensions.Rectangle), typeof(bool), typeof(int), typeof(bool), typeof(Character), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameLocation_isCollidingPosition_postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Object), nameof(Object.draw),
                    new Type[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Object_draw2_prefix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Object), nameof(Object.drawWhenHeld)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Object_drawWhenHeld_prefix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Object), nameof(Object.drawInMenu),
                    new Type[] { typeof(SpriteBatch), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(StackDrawType), typeof(Color), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Object_drawInMenu_prefix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Object), nameof(Object.drawPlacementBounds)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Object_drawPlacementBounds_prefix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Utility), nameof(Utility.playerCanPlaceItemHere)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Utility_playerCanPlaceItemHere_prefix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.IsTileOccupiedBy)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameLocation_IsTileOccupiedBy_postfix)));
            
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Tool), nameof(Tool.DoFunction)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Tool_DoFunction_postfix)));
            
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.canGrabSomethingFromHere)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Utility_canGrabSomethingFromHere_postfix)));
            
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.checkAction)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameLocation_checkAction_postfix)));
            
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.canBePlacedHere)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Furniture_canBePlacedHere_postfix)));
            
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Object), nameof(Object.updateWhenCurrentLocation)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Object_updateWhenCurrentLocation_postfix)));
        }
        
        public static void Object_updateWhenCurrentLocation_postfix(Object __instance, GameTime time)
        {
            if (!__instance.bigCraftable.Value || !BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
            {
                return;
            }
            
            float currentAlpha = GetBiggerMachineAlpha(__instance);
            if (bigMachineData.Fade)
            {
                ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
                Rectangle sourceRect = itemData.GetTexture().Bounds;
                Rectangle boundingBox = new Rectangle((int)__instance.TileLocation.X * 64, ((int)__instance.TileLocation.Y + (-(sourceRect.Height / 16) + bigMachineData.Height)) * 64, bigMachineData.Width * 64, (sourceRect.Height / 16 - bigMachineData.Height) * 64 + 32);
                if (Game1.player.GetBoundingBox().Intersects(boundingBox))
                {
                    if (currentAlpha > 0.4f)
                    {
                        currentAlpha = Math.Max(0.4f, currentAlpha - 0.04f);
                    }

                    __instance.modData[ModDataAlphaKey] = currentAlpha.ToString();
                    return;
                }
            }
            if (currentAlpha < 1f)
            {
                currentAlpha = Math.Min(1f, currentAlpha + 0.05f);
                __instance.modData[ModDataAlphaKey] = currentAlpha.ToString();
            }
        }
        
        private static float GetBiggerMachineAlpha(Object obj)
        {
            float alpha = 1.0f;
            if (obj.modData.TryGetValue(ModDataAlphaKey, out string? value))
            {
                if(float.TryParse(value, out var behindAlpha))
                {
                    alpha = behindAlpha;
                }
            }
            else
            {
                obj.modData[ModDataAlphaKey] = "1.0";
            }
            return alpha;
        }
        
        public static void Furniture_canBePlacedHere_postfix(Furniture __instance, ref bool __result, GameLocation l, Vector2 tile, CollisionMask collisionMask = CollisionMask.All, bool showError = false)
        {
            if (!LocationBigMachines.ContainsKey(l.Name) || !__result)
            {
                return;
            }

            var bm = GetMachineAt(l.Name, tile);
            if (bm != null && (!bm.Object.isPassable() || !__instance.isPassable()))
            {
                __result = false;
            }
        }
        
        static void GameLocation_checkAction_postfix(GameLocation __instance, ref bool __result, Location tileLocation,
            xTile.Dimensions.Rectangle viewport, Farmer who)
        {
            if (!LocationBigMachines.ContainsKey(__instance.Name) || __result)
            {
                return;
            }

            BiggerMachine bm = GetMachineAt(__instance.Name, new Vector2(tileLocation.X, tileLocation.Y));
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
                Object old_held_object = bm.Object.heldObject.Value;
                bm.Object.heldObject.Value = null;
                bool probe_true = bm.Object.performObjectDropInAction(who.CurrentItem, probe: true, who);
                bm.Object.heldObject.Value = old_held_object;
                bool dropin_true = bm.Object.performObjectDropInAction(who.CurrentItem, probe: false, who, returnFalseIfItemConsumed: true);
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

        static void Utility_canGrabSomethingFromHere_postfix(ref bool __result, int x, int y, Farmer who)
        {
            if (Game1.currentLocation == null || !who.IsLocalPlayer || __result)
            {
                return;
            }
            
            if (!LocationBigMachines.ContainsKey(Game1.currentLocation.Name))
            {
                return;
            }

            Vector2 tilePosition = new Vector2(x / 64, y / 64);
            var bm = GetMachineAt(Game1.currentLocation.Name, tilePosition);
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
        
        static void Tool_DoFunction_postfix(Tool __instance, GameLocation location, int x, int y, int power, Farmer who)
        {
            if (!LocationBigMachines.ContainsKey(location.Name) || !__instance.isHeavyHitter() ||
                __instance is MeleeWeapon)
            {
                return;
            }

            Vector2 tilePosition = new Vector2(x / 64, y / 64);
            var bm = GetMachineAt(location.Name, tilePosition);
            if (bm != null && bm.Object.performToolAction(__instance))
            {
                if (bm.Object.Type == "Crafting" && bm.Object.Fragility != 2)
                {
                    location.debris.Add(new Debris(bm.Object.QualifiedItemId, who.GetToolLocation(),
                        Utility.PointToVector2(who.StandingPixel)));
                }
                bm.Object.performRemoveAction();
                location.Objects.Remove(bm.Object.TileLocation);
            }
        }

        public static void GameLocation_IsTileOccupiedBy_postfix(GameLocation __instance, ref bool __result, Vector2 tile, CollisionMask collisionMask = CollisionMask.All, CollisionMask ignorePassables = CollisionMask.None, bool useFarmerTile = false)
        {
            if (!LocationBigMachines.ContainsKey(__instance.Name) || __result || !collisionMask.HasFlag(CollisionMask.Objects))
            {
                return;
            }
            
            BiggerMachine bm = GetMachineAt(__instance.Name, tile);
            if (bm != null && (!ignorePassables.HasFlag(CollisionMask.Objects) || !bm.Object.isPassable()))
            {
                __result = true;
            }
        }
        
        private static BiggerMachine? GetMachineAt(string locationName, Vector2 tile)
        {
            Point position = default(Point);
            position.X = (int)((int)tile.X + 0.5f) * 64;
            position.Y = (int)((int)tile.Y + 0.5f) * 64;
            foreach (var bm in LocationBigMachines[locationName])
            {
                if (bm.GetBoundingBox().Contains(position))
                {
                    return bm;
                }
            }
            return null;
        } 
        
        public static bool Utility_playerCanPlaceItemHere_prefix(GameLocation location, Item item, int x, int y, Farmer f, ref bool __result)
        {
            if (item is null || !BigMachinesList.TryGetValue(item.ItemId, out var bigMachineData))
            {
                return true;
            }
            
            if (Utility.isPlacementForbiddenHere(location) || Game1.eventUp || f.bathingClothes.Value || f.onBridge.Value)
            {
                __result = false;
                return false;
            }
            
            Rectangle rect = new Rectangle(x, y, bigMachineData.Width * 64, bigMachineData.Height * 64);
            rect.Inflate(96, 96);
            if (!rect.Contains(f.StandingPixel))
            {
                __result = false;
                return false;
            }
            
            Vector2 tileLocation = new Vector2(x / 64, y / 64);
            for (int ix = (int)tileLocation.X; ix < (int)tileLocation.X + bigMachineData.Width; ++ix)
            {
                for (int iy = (int)tileLocation.Y; iy < (int)tileLocation.Y + bigMachineData.Height; ++iy)
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
        public static void GameLocation_isCollidingPosition_postfix(GameLocation __instance, ref bool __result, Rectangle position, xTile.Dimensions.Rectangle viewport, bool isFarmer,
            int damagesFarmer, bool glider, Character character, bool pathfinding, bool projectile = false,
            bool ignoreCharacterRequirement = false, bool skipCollisionEffects = false)
        {
            if (!LocationBigMachines.ContainsKey(__instance.Name) || __result)
            {
                return;
            }
            
            Farmer farmer = character as Farmer;
            Rectangle? currentBounds;
            if (farmer != null)
            {
                currentBounds = farmer.GetBoundingBox();
            }
            else
            {
                currentBounds = null;
            }
            
            foreach (var bm in LocationBigMachines[__instance.Name])
            {
                if (bm.IntersectsForCollision(position) && (!currentBounds.HasValue || !bm.IntersectsForCollision(currentBounds.Value)))
                {
                    __result = true;
                    return;
                }
            }
        }
        
        // The real draw
        private static bool Object_draw2_prefix(Object __instance, ref int ____machineAnimationFrame, ref MachineEffects ____machineAnimation, SpriteBatch spriteBatch, int x, int y, float alpha)
        {
            if (__instance.isTemporarilyInvisible || !__instance.bigCraftable.Value || !BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
            {
                return true;
            }
            var scaleFactor = __instance.getScale() * 4f;
            var position = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64));
            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
            int drawHeightOffset = itemData.GetTexture().Height * 4 - bigMachineData.Height * 64;
            var dest = position - new Vector2(0, drawHeightOffset) + scaleFactor / 2f + (__instance.shakeTimer > 0
                ? new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2))
                : Vector2.Zero);
            
            var draw_layer = Math.Max(0f, ((y + 1) * 64 - 24) / 10000f) + x * 1E-05f;
            var offset = 0;
            if (__instance.showNextIndex.Value) 
                offset = 1;
            
            if (____machineAnimationFrame >= 0 && ____machineAnimation != null)
                offset = ____machineAnimationFrame;

            alpha *= GetBiggerMachineAlpha(__instance);

            if (bigMachineData.DrawShadow)
            {
                drawShadow(__instance, bigMachineData, alpha, spriteBatch);
            }
            
            Rectangle sourceRect = new Rectangle(bigMachineData.Width * 16 * offset, 0,bigMachineData.Width * 16, itemData.GetTexture().Height);
            spriteBatch.Draw(itemData.GetTexture(), dest, sourceRect, Color.White * alpha, 0, Vector2.Zero, 4, SpriteEffects.None, draw_layer);

            if (__instance.isLamp.Value && Game1.isDarkOut(__instance.Location))
            {
                spriteBatch.Draw(Game1.mouseCursors, position + new Vector2(-32f, -32f),
                    new Rectangle(88, 1779, 32, 32), Color.White * 0.75f, 0f, Vector2.Zero, 4f, SpriteEffects.None,
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
            spriteBatch.Draw(Game1.mouseCursors,
                Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 - 8, y * 64 - 96 - 16 + yOffset) + bmOffset),
                new Rectangle(141, 465, 20, 24), Color.White * 0.75f, 0f, Vector2.Zero, 4f, SpriteEffects.None, base_sort + 1E-06f);
            
            if (__instance.heldObject.Value == null)
            {
                return false;
            }
            
            if (__instance.heldObject.Value is ColoredObject coloredObj)
            {
                coloredObj.drawInMenu(spriteBatch,
                    Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 - 96f - 8f + yOffset) + bmOffset), 1f, 0.75f,
                    base_sort + 1.1E-05f);
                return false;
            }

            var heldItemData = ItemRegistry.GetDataOrErrorItem(__instance.heldObject.Value.QualifiedItemId);
            spriteBatch.Draw(heldItemData.GetTexture(),
                Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 - 64 - 8 + yOffset) + bmOffset),
                heldItemData.GetSourceRect(), Color.White * 0.75f, 0f, new Vector2(8f, 8f), 4f, SpriteEffects.None,
                base_sort + 1E-05f);

            var drawType = StackDrawType.Hide;
            if (__instance.heldObject.Value.Stack > 1)
                drawType = StackDrawType.Draw;
            else if (__instance.heldObject.Value.Quality > 0)
                drawType = StackDrawType.HideButShowQuality;

            __instance.heldObject.Value.DrawMenuIcons(spriteBatch,
                Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 - 64 - 32 + yOffset - 4f) + bmOffset), 1f, 1f,
                base_sort + 1.2E-05f, drawType, Color.White);
            return false;
        }
        
        private static bool Object_drawWhenHeld_prefix(Object __instance, SpriteBatch spriteBatch, Vector2 objectPosition, Farmer f)
        { 
            if (!__instance.bigCraftable.Value || !BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
            {
                return true;
            }

            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
            Rectangle sourceRect = new Rectangle(0, 0,bigMachineData.Width * 16, itemData.GetTexture().Height);
            spriteBatch.Draw(itemData.GetTexture(), objectPosition - new Vector2(0, itemData.GetTexture().Height * 4f) + new Vector2(0, 124), sourceRect, Color.White, 0, new Vector2(bigMachineData.Width / 2f * 16 - 8, 0), 4f, SpriteEffects.None, Math.Max(0.0f, (f.getStandingPosition().Y + 3) / 10000f));
            return false;
        }

        private static bool Object_drawInMenu_prefix(Object __instance, SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency, float layerDepth, StackDrawType drawStackNumber, Color color, bool drawShadow)
        {
            if (!__instance.bigCraftable.Value || !BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
            {
                return true;
            }

            if (__instance.IsRecipe)
            {
                transparency = 0.5f;
                scaleSize *= 0.75f;
            }
            
            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);
            float scale = Math.Min(4f * 16 / itemData.GetTexture().Height, 4f * 16 / itemData.GetTexture().Width);
            Rectangle sourceRect = new Rectangle(0, 0,bigMachineData.Width * 16, itemData.GetTexture().Height);
            spriteBatch.Draw(itemData.GetTexture(), location + new Vector2(32f, 32f), sourceRect, color * transparency, 0f, new Vector2(sourceRect.Width / 2, sourceRect.Height / 2), scale, SpriteEffects.None, layerDepth);
            __instance.DrawMenuIcons(spriteBatch, location, scaleSize, transparency, layerDepth, drawStackNumber, color);
            return false;
        }

        private static bool Object_drawPlacementBounds_prefix(Object __instance, SpriteBatch spriteBatch, GameLocation location)
        {
            if (!__instance.bigCraftable.Value || !BigMachinesList.TryGetValue(__instance.ItemId, out var bigMachineData))
            {
                return true;
            }
            Game1.isCheckingNonMousePlacement = !Game1.IsPerformingMousePlacement();
            int x = (int)Game1.GetPlacementGrabTile().X * 64;
            int y = (int)Game1.GetPlacementGrabTile().Y * 64;
            if (Game1.isCheckingNonMousePlacement)
            {
                Vector2 nearbyValidPlacementPosition = Utility.GetNearbyValidPlacementPosition(Game1.player, location, __instance, x, y);
                x = (int)nearbyValidPlacementPosition.X;
                y = (int)nearbyValidPlacementPosition.Y;
            }
            
            Vector2 tile = new Vector2(x / 64, y / 64);
            if (__instance.Equals(Game1.player.ActiveObject))
            {
                __instance.TileLocation = tile;
            }
            
            bool canPlaceHere = Utility.playerCanPlaceItemHere(location, __instance, x, y, Game1.player) || (Utility.isThereAnObjectHereWhichAcceptsThisItem(location, __instance, x, y) && Utility.withinRadiusOfPlayer(x, y, 1, Game1.player));
            Game1.isCheckingNonMousePlacement = false;
            for (var x_offset = 0; x_offset < bigMachineData.Width; ++x_offset)
            {
                for (var y_offset = 0; y_offset < bigMachineData.Height; ++y_offset)
                {
                    spriteBatch.Draw(Game1.mouseCursors,
                        new Vector2((tile.X + x_offset) * 64f - Game1.viewport.X,
                                            (tile.Y + y_offset) * 64f - Game1.viewport.Y),
                        new Rectangle(canPlaceHere ? 194 : 210, 388, 16, 16), Color.White, 0f, Vector2.Zero, 4f,
                        SpriteEffects.None, 0.01f);
                }
            }

            __instance.draw(spriteBatch, (int)tile.X, (int)tile.Y, 0.5f);
            return false;
        }
        
        public static void drawShadow(Object obj, BiggerMachineData bigMachineData, float alpha, SpriteBatch b)
        {
            Vector2 basePosition = Game1.GlobalToLocal(new Vector2(obj.TileLocation.X * 64, (obj.TileLocation.Y + bigMachineData.Height) * 64));
            b.Draw(Game1.mouseCursors, basePosition, Building.leftShadow, Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1E-05f);
            for (int x = 1; x < bigMachineData.Width - 1; x++)
            {
                b.Draw(Game1.mouseCursors, basePosition + new Vector2(x * 64, 0f), Building.middleShadow, Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1E-05f);
            }
            b.Draw(Game1.mouseCursors, basePosition + new Vector2((bigMachineData.Width - 1) * 64, 0f), Building.rightShadow, Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1E-05f);
        }
    }
}
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
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
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(ObjectDataDefinition), nameof(ObjectDataDefinition.CreateItem)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(ObjectDataDefinition_CreateItem_prefix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.canBePlacedHere)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_canBePlacedHere_postfix)));
        
        // Drawing
        harmony.Patch(AccessTools.Method(typeof(Object), nameof(Object.draw), new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
            transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_draw_transpiler)));
    }
    
    public static IEnumerable<CodeInstruction> Object_draw_transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        CodeMatcher matcher = new(instructions, generator);
        //var drawMethodInfo = AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw), new Type[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) });
        var torchDraw = AccessTools.Method(typeof(Torch), nameof(Torch.drawBasicTorch));
        var machineDrawBubbleTranspiler = AccessTools.Method(typeof(HarmonyPatches), nameof(MachineDrawBubbleTranspiler));
        Label jumpTarget = generator.DefineLabel();
        
        try
        {
            matcher.MatchEndForward(
                    new CodeMatch(OpCodes.Div),
                    new CodeMatch(OpCodes.Ldc_R4),
                    new CodeMatch(OpCodes.Call, torchDraw),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Object), nameof(Object.readyForHarvest))),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Brfalse)).
                ThrowIfNotMatch($"Could not find entry point for {nameof(Object_draw_transpiler)}");
            jumpTarget = (Label)matcher.Operand;
            
            matcher
                .Advance(1)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0)) // load this
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1)) // load spriteBatch
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_2)) // load x
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_3)) // load y
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, machineDrawBubbleTranspiler)) // load y
                .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue, jumpTarget) // return if true
                );
        }
        catch (Exception ex)
        {
            ModEntry.MonitorInst.Log($"Failed in {nameof(Object_draw_transpiler)}:\n{ex}", LogLevel.Error);
        }
        
        return matcher.InstructionEnumeration();
    }

    private static bool MachineDrawBubbleTranspiler(Object obj, SpriteBatch spriteBatch, int x, int y)
    {
        if (obj.heldObject.Value == null || obj.heldObject.Value.ItemId != ModEntry.FluidContainerID || obj.heldObject.Value.heldObject.Value is not Chest chest)
        {
            return false;
        }

        if (chest.Items.Count == 0)
        {
            obj.heldObject.Value = null;
        }
        
        var base_sort = (y + 1) * 64 / 10000f + obj.TileLocation.X / 50000f;
        var yOffset = 4f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2);
        spriteBatch.Draw(Game1.mouseCursors, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 - 8, y * 64 - 96 - 16 + yOffset)), new Rectangle(141, 465, 20, 24), Color.White * 0.75f, 0f,
           Vector2.Zero, 4f, SpriteEffects.None, base_sort + 1E-06f);
        
        List<ParsedItemData> itemData = new List<ParsedItemData>();
        List<Item> items = new List<Item>();
        foreach (var item in chest.Items)
        {
            var heldItemData = ItemRegistry.GetDataOrErrorItem(item.QualifiedItemId);
            itemData.Add(heldItemData);
            items.Add(item);
        }
        DrawItems(spriteBatch, items, itemData, x * 64, y * 64 - 64 + yOffset, base_sort);
        return true;
    }
    
    public static void DrawItems(SpriteBatch spriteBatch, List<Item> items,  List<ParsedItemData> itemData, float baseX, float baseY, float base_sort)
    {
        int count = items.Count;
        var origin = new Vector2(8f, 8f);
        var ld = base_sort + 1.2E-05f;
        
        if (count == 1)
        {
            spriteBatch.Draw(itemData[0].GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 32, baseY - 8)), itemData[0].GetSourceRect(), Color.White * 0.75f, 0f,
                origin, 4f, SpriteEffects.None, base_sort + 1E-05f);
            items[0].DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX, baseY - 36f)), 1f, 1f, ld, StackDrawType.Draw,
                Color.White);
        }
        else if (count == 2)
        {
            spriteBatch.Draw(itemData[0].GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 16, baseY - 8)), itemData[0].GetSourceRect(), Color.White * 0.75f, 0f,
                origin, 2.5f, SpriteEffects.None, base_sort + 1E-05f);
            items[0].DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX - 30, baseY - 44f)), .9f, 1f, ld, StackDrawType.Draw,
                Color.White);
            
            
            spriteBatch.Draw(itemData[1].GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 52, baseY - 8)), itemData[1].GetSourceRect(), Color.White * 0.75f, 0f,
                origin, 2.5f, SpriteEffects.None, base_sort + 1E-05f);
            items[1].DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 10, baseY - 44f)), .9f, 1f, ld, StackDrawType.Draw,
                Color.White);
        }
        else if (count > 2)
        {
            spriteBatch.Draw(itemData[0].GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 16, baseY - 28)), itemData[0].GetSourceRect(), Color.White * 0.75f, 0f,
                origin, 2f, SpriteEffects.None, base_sort + 1E-05f);
            items[0].DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX - 30, baseY - 72f)), .9f, 1f, ld, StackDrawType.Draw,
                Color.White);
            
            spriteBatch.Draw(itemData[1].GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 52, baseY - 28)), itemData[1].GetSourceRect(), Color.White * 0.75f, 0f,
                origin, 2f, SpriteEffects.None, base_sort + 1E-05f);
            items[1].DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 10, baseY - 72f)), .9f, 1f, ld, StackDrawType.Draw,
                Color.White);
            
            spriteBatch.Draw(itemData[2].GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 16, baseY + 12)), itemData[2].GetSourceRect(), Color.White * 0.75f, 0f,
                origin, 2f, SpriteEffects.None, base_sort + 1E-05f);
            items[2].DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX - 30, baseY - 32f)), .9f, 1f, ld, StackDrawType.Draw,
                Color.White);

            if (items.Count == 3)
            {
                return;
            }
            spriteBatch.Draw(itemData[3].GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 52, baseY + 12)), itemData[3].GetSourceRect(), Color.White * 0.75f, 0f,
                origin, 2f, SpriteEffects.None, base_sort + 1E-05f);
            items[3].DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(baseX + 10, baseY - 32f)), .9f, 1f, ld, StackDrawType.Draw,
                Color.White);
        }
    }
    

    public static void Object_canBePlacedHere_postfix(Object __instance, ref bool __result, GameLocation l, Vector2 tile, CollisionMask collisionMask = CollisionMask.All, bool showError = false)
    {
        if (__instance.bigCraftable.Value && Game1.bigCraftableData.TryGetValue(__instance.ItemId, out var bcData) && bcData != null && bcData.CustomFields != null && bcData.CustomFields.TryGetValue(ModEntry.PumpKey, out var value))
        {
            if (l is Caldera || l is VolcanoDungeon || l is MineShaft)
            {
                __result = false;
                return;
            }
            int x = (int)tile.X;
            int y = (int)tile.Y;
            Vector2 placement_tile = new Vector2(x, y);
            bool neighbor_check = (l.isWaterTile(x + 1, y) || l.isWaterTile(x - 1, y)) || (l.isWaterTile(x, y + 1) || l.isWaterTile(x, y - 1));
            if (l.objects.ContainsKey(placement_tile) || !neighbor_check || !l.isWaterTile((int)placement_tile.X, (int)placement_tile.Y) || l.doesTileHaveProperty((int)placement_tile.X, (int)placement_tile.Y, "Passable", "Buildings") != null)
            {
                __result = false;
                return;
            }
            __result = true;
        }
    }
    

    public static bool ObjectDataDefinition_CreateItem_prefix(ObjectDataDefinition __instance, ref Item __result, ParsedItemData data)
    {
        if (data == null || data.ItemId != ModEntry.PipesID)
        {
            return true;
        }

        __result = new FluidPipe(data.ItemId, 1);
        return false;
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
        if (__instance == null || !__instance.readyForHarvest.Value || __instance.heldObject.Value == null || __instance.heldObject.Value.modData == null)
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
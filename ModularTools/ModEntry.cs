using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceShared.APIs;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.Tools;
using Object = StardewValley.Object;

namespace ModularTools
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        public static IModHelper Helper;
        
        public override void Entry(IModHelper helper)
        {
            Helper = helper;
            I18n.Init(Helper.Translation);
            
            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            Helper.Events.Content.AssetRequested += OnAssetRequested;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            
            var def = new ModularUpgradeDefinition();
            ItemRegistry.ItemTypes.Add(def);
            Helper.Reflection.GetField<Dictionary<string, IItemDataDefinition>>(typeof(ItemRegistry), "IdentifierLookup").GetValue()[def.Identifier] = def;
            
            HarmonyPatches();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var sc = Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
            sc.RegisterSerializerType(typeof(ModularUpgradeItem));
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button == SButton.C)
            {
                if (Game1.player.CurrentTool is not null)
                {
                    Game1.player.CurrentTool.AttachmentSlotsCount += 1;
                }
            }
        }
        
         private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/ModularUpgrades"))
            { 
                e.LoadFrom(() =>
                { 
                    var modAssets = Helper.ModContent.Load <Dictionary<string, ModularUpgradeData>>("assets/modularupgrade_data.json");
                    Dictionary<string, ModularUpgradeData> ret = new();
                    foreach (string upgrade in modAssets.Keys)
                    {
                        ret.Add(upgrade,
                            new ModularUpgradeData()
                            {
                                TextureIndex = modAssets[upgrade].TextureIndex,
                                DisplayName = I18n.GetByKey(modAssets[upgrade].DisplayName),
                                Description = I18n.GetByKey(modAssets[upgrade].Description),
                                Price = modAssets[upgrade].Price
                            });
                    }
                    return ret;
                }, AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/modularupgrades.png"))
            {
                e.LoadFromModFile<Texture2D>("assets/modularupgrades.png", AssetLoadPriority.Low);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("spacechase0.SpaceCore/ObjectExtensionData"))
            {
                e.Edit((asset) =>
                {
                    var modAssets = Helper.ModContent.Load <Dictionary<string, ModularUpgradeData>>("assets/modularupgrade_data.json");
                    var data = asset.AsDictionary<string, SpaceCore.VanillaAssetExpansion.ObjectExtensionData>().Data;
                    foreach (string upgrade in modAssets.Keys)
                    {
                        data.Add(upgrade, new()
                        {
                            MaxStackSizeOverride = 1,
                            CategoryTextOverride = I18n.Modularupgrade_Category_Text(),
                            CategoryColorOverride = Color.LimeGreen
                        });
                    }
                });
            }
        }
        
        // TODO dont need to patch for this but it is interesting for magic mod: drawPlacementBounds --- isPlaceable
        private void HarmonyPatches()
        {
            var harmony = new Harmony(ModManifest.UniqueID);

            Type[] types = { typeof(int), typeof(SpriteBatch), typeof(int), typeof(int) };
            var originalToolsMethod = typeof(Tool).GetMethod("DrawAttachmentSlot",
                BindingFlags.Instance | BindingFlags.NonPublic, null, types, null);
            harmony.Patch(
                original: originalToolsMethod,
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DrawAttachmentSlot_prefix)));

            Type[] drawHoverTypes =
            {
                typeof(SpriteBatch), typeof(StringBuilder), typeof(SpriteFont), typeof(int), typeof(int), typeof(int),
                typeof(string), typeof(int), typeof(string[]), typeof(Item), typeof(int), typeof(string), typeof(int),
                typeof(int), typeof(int), typeof(float), typeof(CraftingRecipe), typeof(IList<Item>), typeof(Texture2D),
                typeof(Rectangle), typeof(Color), typeof(Color), typeof(float), typeof(int), typeof(int) };
            harmony.Patch(
                original: AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.drawHoverText),
                    drawHoverTypes),
                transpiler: new HarmonyMethod(typeof(ModEntry),
                    nameof(IClickableMenu_drawHoverTextTranspiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.canThisBeAttached),
                    new Type[] { typeof(Object), typeof(int) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(canThisBeAttached_postfix)));

            Type[] tileAffectedTypes = { typeof(Vector2), typeof(int), typeof(Farmer) };
            var originalTilesAffected = typeof(Tool).GetMethod("tilesAffected",
                BindingFlags.Instance | BindingFlags.NonPublic, null, tileAffectedTypes, null);
            harmony.Patch(
                original: originalTilesAffected,
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Post_tilesAffected)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.attach), new Type[] { typeof(Object) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(attachOrDetach_postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.UpgradeFrom), new Type[] { typeof(Tool) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(OnToolUpgrade_postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pickaxe), nameof(Pickaxe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(PickaxeDoFunction_prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pickaxe), nameof(Pickaxe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(PickaxeDoFunction_postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Axe), nameof(Axe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(AxeDoFunction_prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Axe), nameof(Axe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(AxeDoFunction_postfix)));
        }

        public static void AxeDoFunction_prefix(Axe __instance, out int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            __state = __instance.UpgradeLevel;
            //TODO config
            __instance.UpgradeLevel = GetToolStrength(__instance);
        }
        
        public static void AxeDoFunction_postfix(Axe __instance, int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            __instance.UpgradeLevel = __state;
        }
        
        public static void PickaxeDoFunction_prefix(Pickaxe __instance, out int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            __state = __instance.UpgradeLevel;
            //TODO config
            __instance.UpgradeLevel = GetToolStrength(__instance);
        }
        
        public static void PickaxeDoFunction_postfix(Pickaxe __instance, int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
           __instance.UpgradeLevel = __state;
        }
        
        private static int GetToolStrength(Tool tool)
        {
            int strength = 0;
            foreach (Object o in tool.attachments)
            {
                if (o is not null && o.QualifiedItemId == MUQIds.Power)
                {
                    strength++;
                }
            }
            return strength;
        }
        
        public static List<string> GetAttachmentQualifiedItemIDs(Tool tool)
        {
	        List<string> ids = new List<string>();
	        foreach (Object o in tool.attachments)
	        {
		        if (o != null)
		        {
			        ids.Add(o.QualifiedItemId);
		        }
	        }
	        return ids;
        }

        public static void Post_tilesAffected(ref List<Vector2> __result, Vector2 tileLocation, int power, Farmer who)
        {
	        if (who.CurrentTool == null || power == 1)
	        {
		        return;
	        }

            if (!IsAllowedTool(who.CurrentTool))
            {
                return;
            }

            List<Vector2> tileLocations = new List<Vector2>();
            tileLocations.Add(tileLocation);

	        List<string> attachments = GetAttachmentQualifiedItemIDs(who.CurrentTool);
	        int widthAttachCount = Utility.getStringCountInList(attachments, MUQIds.Width);
	        int heightAttachCount = Utility.getStringCountInList(attachments, MUQIds.Length);

	        if (widthAttachCount + heightAttachCount == 0)
            {
                __result = tileLocations;
		        return;
	        }
            
	        Vector2 offset = Vector2.Zero;
	        switch (who.FacingDirection)
	        {
		        case 0:
			        offset = new Vector2(0f, -1f);
			        break;
		        case 1:
			        offset = new Vector2(1f, 0f);
			        break;
		        case 2:
			        offset = new Vector2(0f, 1f);
			        break;
		        case 3:
			        offset = new Vector2(-1f, 0f);
			        break;
	        }
            
            Vector2 left = new Vector2(offset.Y, -offset.X);
            Vector2 right = new Vector2(-offset.Y, offset.X);

            int index = 0;
            int wCount = 0;
            int hCount = 0;
            foreach (string s in attachments)
            {
                if (s == MUQIds.Width)
                {
                    wCount++;
                } 
                else if (s == MUQIds.Length)
                {
                    hCount++;
                }

                index++;
                if (index >= power-1)
                {
                    break;
                }
            }

            int multiplier = 1;
            if (wCount == 0)
            {
                multiplier = hCount;
            }
            if (hCount == 0)
            {
                multiplier = wCount;
            }
            
            for (int i = 1; i <= wCount * multiplier; i++)
            {
                tileLocations.Add(tileLocation + left * i);
                tileLocations.Add(tileLocation + right * i);
            }

            int tilesCount = tileLocations.Count - 1;
            for (int i = 1; i <= hCount * 2 * multiplier; i++)
            {
                for (int i2 = 0; i2 <= tilesCount; i2++)
                {
                    tileLocations.Add(tileLocations[i2] + offset * i);
                }
            }
            
	        __result = tileLocations;
        }

        private static bool IsAllowedTool(Tool tool)
        {
            // TODO Config
            return tool is WateringCan or Hoe or Pickaxe or Axe or MeleeWeapon;
        }
        
        public static void OnToolUpgrade_postfix(Tool __instance)
        {
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            
            if (__instance is WateringCan wateringCan)
            {
                wateringCan.waterCanMax = 40;
                wateringCan.WaterLeft = 40;
            }
            __instance.AttachmentSlotsCount = __instance.UpgradeLevel;
        }
        
        private static int GetWateringCanCapacity(Tool tool)
        {
            const int wateringCapacityIncrease = 15;
            int Capacity = 40;
            foreach (Object o in tool.attachments)
            {
                if (o is not null && o.QualifiedItemId == MUQIds.Capacity)
                {
                    Capacity += wateringCapacityIncrease;
                }
            }
            return Capacity;
        }
        
        private static void SetToolSpeed(Tool tool)
        {
            const float speedStrength = .5f;

            float speed = 1;
            if (tool.hasEnchantmentOfType<SwiftToolEnchantment>())
            {
                speed = 0.66f;
            }

            foreach (Object o in tool.attachments)
            {
                if (o is not null && o.QualifiedItemId == MUQIds.Speed)
                {
                    speed *= speedStrength;
                }
            }
            tool.AnimationSpeedModifier = speed;
        }

        public static void attachOrDetach_postfix(Tool __instance, ref Object __result, Object o)
        {
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            
            if (__instance is WateringCan wateringCan)
            {
                wateringCan.waterCanMax = GetWateringCanCapacity(__instance);
                if (wateringCan.WaterLeft > wateringCan.waterCanMax) // reduce water if we have a new lower capacity
                {
                    wateringCan.WaterLeft = wateringCan.waterCanMax;
                }
            }
            SetToolSpeed(__instance);
            
            /*
            // Removing item
            if (o is null && __result is not null)
            {
                switch (__result.QualifiedItemId)
                {
                    case MUQIds.Speed:
                        __instance.AnimationSpeedModifier *= speedStrength;
                        break;
                }
            }
            else // attaching item
            {
                var attachedItem = __instance.attachments[^1];
                if(attachedItem is not null)
                {
                    switch (attachedItem.QualifiedItemId)
                    {
                        case MUQIds.Speed:
                            __instance.AnimationSpeedModifier /= speedStrength;
                            break;
                    }
                }
            }*/
        }

        public static void canThisBeAttached_postfix(Tool __instance, ref bool __result, Object o, int slot)
        {
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            
            if(o is ModularUpgradeItem)
            {
                __result = true;
            }
            else
            {
                __result = false;
            }
        }
        
        public static int AdjustHoverMenuHeight(Item hoveredItem)
        {
            int slots = hoveredItem.attachmentSlots();
            if (hoveredItem is not Tool tool)
            {
                return 68 * slots;
            }
            
            if (!IsAllowedTool(tool))
            {
                if (slots > 0)
                {
                    if (slots % 2 == 0)
                    {
                        return 68 * slots / 2;
                    }
                    return 68 * (slots + 1) / 2;
                }
            }
            return 68 * slots;
        }
        
        public static IEnumerable<CodeInstruction> IClickableMenu_drawHoverTextTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            CodeMatcher matcher = new(instructions, generator);
            MethodInfo attachmentSlots = AccessTools.PropertyGetter(typeof(Item), nameof(Item.attachmentSlots));
            MethodInfo adjustHeight = AccessTools.Method(typeof(ModEntry), nameof(AdjustHoverMenuHeight));

            // Transpile height for tools
            matcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldloc_2), // load height
                    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)68), // 68 
                    new CodeMatch(OpCodes.Ldarg_S, (byte)9), // hover item
                    new CodeMatch(OpCodes.Callvirt, attachmentSlots), // get hover item slot count
                    new CodeMatch(OpCodes.Mul), // 68 * slot count
                    new CodeMatch(OpCodes.Add), // add to height
                    new CodeMatch(OpCodes.Stloc_2) // store into height
                )
                .ThrowIfNotMatch($"Could not find tool entry point for {nameof(IClickableMenu_drawHoverTextTranspiler)}")
                .Advance(1)
                .RemoveInstruction() // remove 68
                .Advance(1)
                .RemoveInstruction() // remove get attach slots
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Call, adjustHeight) // returns int height
                ).RemoveInstruction(); // remove mul

            
            MethodInfo getToolForgeLevels = AccessTools.Method(typeof(Tool), nameof(Tool.GetTotalForgeLevels));
            var myJump = generator.DefineLabel();
            // Transpile height for weapons
            matcher.MatchStartForward(
                new CodeMatch(OpCodes.Ldloc_S), // grab the weapon, (byte)19
                new CodeMatch(OpCodes.Ldc_I4_0), // push a 0
                new CodeMatch(OpCodes.Callvirt, getToolForgeLevels), // , getToolForgeLevels
                new CodeMatch(OpCodes.Ldc_I4_0), // push a 0
                new CodeMatch(OpCodes.Ble_S) // if forge <= 0 skip next section
            )
            .ThrowIfNotMatch($"Could not find weapon entry point for {nameof(IClickableMenu_drawHoverTextTranspiler)}")
            .Advance(1)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_S, (byte)9), // load hover item
                new CodeInstruction(OpCodes.Call, adjustHeight), // returns int height
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Add),
                new CodeInstruction(OpCodes.Stloc_2)
            );

            return matcher.InstructionEnumeration();
        }
        
        internal static bool DrawAttachmentSlot_prefix(Tool __instance, int slot, SpriteBatch b, int x, int y)
        {
            if (!IsAllowedTool(__instance))
            {
                return true;
            }
            
            y -= slot * 68;
            
            x += slot % 2 * 68;
            y += slot / 2 * 68;
            
            Vector2 pixel = new Vector2(x, y);
            Texture2D texture = Game1.menuTexture;
            Rectangle sourceRect = Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 10);
            b.Draw(texture, pixel, sourceRect, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.86f);
            __instance.attachments[slot]?.drawInMenu(b, pixel, 1f);
            return false;
        }
    }
}
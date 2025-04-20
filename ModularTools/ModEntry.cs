using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            
            HarmonyPatches();
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

        private void HarmonyPatches()
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            
            Type[] types = { typeof(int),typeof(SpriteBatch),typeof(int),typeof(int) };
            var originalToolsMethod = typeof(Tool).GetMethod("DrawAttachmentSlot",
                BindingFlags.Instance | BindingFlags.NonPublic, null, types, null);
            harmony.Patch(
                original: originalToolsMethod,
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DrawAttachmentSlot_prefix))
            );
            
            Type[] drawHoverTypes = { typeof(SpriteBatch), typeof(StringBuilder), typeof(SpriteFont), typeof(int), typeof(int), typeof(int), typeof(string), typeof(int), typeof(string[]), typeof(Item), typeof(int), typeof(string), typeof(int), typeof(int), typeof(int), typeof(float), typeof(CraftingRecipe), typeof(IList<Item>), typeof(Texture2D), typeof(Rectangle), typeof(Color), typeof(Color), typeof(float), typeof(int), typeof(int)};
            harmony.Patch(
                original: AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.drawHoverText), drawHoverTypes),
                transpiler: new HarmonyMethod(typeof(ModEntry),
                    nameof(IClickableMenu_drawHoverTextTranspiler))
            );
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.canThisBeAttached), new Type[] { typeof(Object), typeof(int) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(canThisBeAttached_postfix))
            );
        }

        // patch GetCategoryColor
        // patch GetCategoryDisplayName
        // countsForShippedCollection
        // isPotentialBasicShipped
        // GetBaseContextTags??
        
        // dont need to patch for this but it is interesting for magic mod: drawPlacementBounds --- isPlaceable
        public static void canThisBeAttached_postfix(Tool __instance, ref bool __result, Object o, int slot)
        {
            /* fishing rod
            if (o.QualifiedItemId == "(O)789" && slot != 0)
            {
                return true;
            }
            if (slot != 0)
            {
                if (o.Category == -22)
                {
                    return this.CanUseTackle();
                }
                return false;
            }
            if (o.Category == -21)
            {
                return this.CanUseBait();
            }
            return false;*/
        }
        
        public static int AdjustHoverMenuHeight(Item hoveredItem)
        {
            // TODO config to disable this part
            int slots = hoveredItem.attachmentSlots();
            if (hoveredItem is WateringCan or Hoe or Pickaxe or Axe or Pan or MeleeWeapon)
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
            // TODO config to disable this part
            if (__instance is not WateringCan && __instance is not Pickaxe && __instance is not Hoe && __instance is not Axe && __instance is not Pan && __instance is not MeleeWeapon)
            {
                return true;
            }
            
            //DrawAttachmentSlot(slot, b, x, y + slot * 68);
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
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Tools;

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
            
            // TODO transpiler IClickableMenu drawHoverText --- height += 68 * hoveredItem.attachmentSlots(); is breaking tools
            // weapons don't expand at all
        }
        
        internal static bool DrawAttachmentSlot_prefix(Tool __instance, int slot, SpriteBatch b, int x, int y)
        {
            if (__instance is not WateringCan && __instance is not Pickaxe && __instance is not Hoe && __instance is not Axe && __instance is not Pan)
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
        
        // TODO override can this attach
        // protected override bool canThisBeAttached(Object o, int slot)
        /*
         * if (o.QualifiedItemId == "(O)789" && slot != 0)
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
		return false;
		
         */
    }
}
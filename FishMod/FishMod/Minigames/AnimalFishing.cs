using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace FishMod;

public class AnimalFishing
{
    public static void Post_CheckForAction(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
    {
        if (__instance.QualifiedItemId.Equals("(BC)Jok.Fishdew.CP.AnimalMachine") && !justCheckingForActivity && !__result)
        {
            Game1.activeClickableMenu =
                new BasicBobberBar(CompleteCallback, 3);
            

            void CompleteCallback(int treasures, bool success)
            {
                if (!(__instance.Location is AnimalHouse animalHouse))
                {
                    return;
                }

                // TODO: normal xp gain?
                int xp = 5 * animalHouse.animals.Pairs.Count();
                Game1.player.gainExperience(Farmer.farmingSkill, xp);
                foreach (KeyValuePair<long, FarmAnimal> pair in animalHouse.animals.Pairs)
                {
                    // TODO: should this be auto pet true?
                    pair.Value.pet(Game1.player, is_auto_pet: false);
                }
            }
        }
    }

    public static bool Pre_draw(StardewValley.Object __instance, SpriteBatch spriteBatch, int x, int y, float alpha = 1f)
    {
        if (__instance.QualifiedItemId.Equals("(BC)Jok.Fishdew.CP.AnimalMachine"))
        {
            var scaleFactor = __instance.getScale();
            scaleFactor *= 4f;
            var position2 = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 - 64));
            var destination = new Rectangle(
                (int)(position2.X - scaleFactor.X / 2f) + (__instance.shakeTimer > 0 ? Game1.random.Next(-1, 2) : 0),
                (int)(position2.Y - scaleFactor.Y / 2f) + (__instance.shakeTimer > 0 ? Game1.random.Next(-1, 2) : 0),
                (int)(64f + scaleFactor.X), (int)(128f + scaleFactor.Y / 2f));
            var draw_layer = Math.Max(0f, ((y + 1) * 64 - 24) / 10000f) + x * 1E-05f;
            var itemData = ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId);

            var texture = itemData.GetTexture();
            spriteBatch.Draw(texture, destination, itemData.GetSourceRect(1, __instance.ParentSheetIndex),
                Color.White * alpha, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            spriteBatch.Draw(texture, position2 + new Vector2(8.5f, 12f) * 4f,
                itemData.GetSourceRect(2, __instance.ParentSheetIndex), Color.White * alpha,
                (float)Game1.currentGameTime.TotalGameTime.TotalSeconds * -1.5f, new Vector2(7.5f, 15.5f), 4f,
                SpriteEffects.None, draw_layer + 1E-05f);
            return false;
        }
        return true;
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using Object = StardewValley.Object;

namespace FishMod;

public class AnimalFishing
{
    public static void Post_CheckForAction(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
    {
        if (__instance.QualifiedItemId.Equals("(BC)Jok.Fishdew.CP.AnimalMachine") && !justCheckingForActivity && !__result)
        {
            if (!(__instance.Location is AnimalHouse animalHouse))
            {
                return;
            }
            
            List<int> animals = new List<int>();
            foreach (KeyValuePair<long, FarmAnimal> pair in animalHouse.animals.Pairs)
            {
                if (pair.Value.wasPet.Value || pair.Value.wasAutoPet.Value)
                {
                    continue;
                }

                int index = GetAnimalSpriteIndex(pair.Value.type.Value);
                if (index == -1)
                {
                    animals.Add(animalHouse.Name.Contains("Barn") ? TreasureSprites.WhiteCow : TreasureSprites.WhiteChicken);
                    continue;
                }
                animals.Add(index);
            }
            
            Dictionary<string, int> produceCounts = new Dictionary<string, int>();
            
            bool treasure = Game1.random.NextDouble() < DeluxeFishingRodTool.baseChanceForTreasure + who.LuckLevel * 0.005 + who.DailyLuck / 2.0;
            DeluxeFishingRodTool.PlayHitEffectForRandomEncounter(who, new AnimalBobberBar(CompleteCallback, animals, treasure,3));
            
            void CompleteCallback(int treasures, bool success)
            {
                int xp = 5 * animalHouse.animals.Pairs.Count();
                Game1.player.gainExperience(Farmer.farmingSkill, xp);
                foreach (KeyValuePair<long, FarmAnimal> pair in animalHouse.animals.Pairs)
                {
                    pair.Value.pet(Game1.player, is_auto_pet: false);
                    string key = pair.Value.currentProduce.Value;
                    if (key == null)
                    {
                        continue;
                    }
                    if (produceCounts.ContainsKey(key))
                    {
                        produceCounts[key] = 1;
                    }
                    else
                    {
                        produceCounts[key]++;
                    }
                }
                
                if (treasures > 0 && produceCounts.Any())
                {
                    who.playNearbySoundLocal("openChest");
                    __instance.Location.temporarySprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(64, 1920, 32, 32), 200f, 4, 0, who.Position + new Vector2(-32f, -228f), false, false, (float) ( who.StandingPixel.Y / 10000.0 + 1.0 / 1000.0), 0.0f, Color.White, 4f, 0.0f, 0.0f, 0.0f)
                    {
                        endFunction = openTreasureMenu
                    });
                }
            }

            void openTreasureMenu(int extraInfo)
            {
                Game1.player.gainExperience(Farmer.farmingSkill, 10);
        
                List<Item> inventory = new List<Item>();
                
                foreach (var pair in produceCounts)
                {
                    if (pair.Value > 0)
                    {
                        inventory.Add(ItemRegistry.Create(pair.Key, pair.Value));
                    }
                }
        
                ItemGrabMenu itemGrabMenu = new ItemGrabMenu(inventory).setEssential(true);
                itemGrabMenu.source = 69;
                Game1.activeClickableMenu = itemGrabMenu;
            }
        }
    }

    public static bool Pre_draw(Object __instance, SpriteBatch spriteBatch, int x, int y, float alpha = 1f)
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

    private static int GetAnimalSpriteIndex(string animalType)
    {
        switch (animalType)
        {
            case "White Chicken":
                return TreasureSprites.WhiteChicken;
            case "Brown Chicken":
                return TreasureSprites.BrownChicken;
            case "Blue Chicken":
                return TreasureSprites.BlueChicken;
            case "Void Chicken":
                return TreasureSprites.VoidChicken;
            case "Golden Chicken":
                return TreasureSprites.GoldenChicken;
            case "Duck":
                return TreasureSprites.Duck;
            case "Rabbit":
                return TreasureSprites.Rabbit;
            case "Dinosaur":
                return TreasureSprites.Dino;
            case "White Cow":
                return TreasureSprites.WhiteCow;
            case "Brown Cow":
                return TreasureSprites.BrownCow;
            case "Goat":
                return TreasureSprites.Goat;
            case "Sheep":
                return TreasureSprites.Sheep;
            case "Pig":
                return TreasureSprites.Pig;
            case "Ostrich":
                return TreasureSprites.Ostrich;
        }

        return -1;
    }
}
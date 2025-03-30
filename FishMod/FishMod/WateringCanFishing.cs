using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace FishMod;

public class WateringCanFishing
{

    public static TimeSpan chargingUpTime;
    public static bool playerDidChargeUp;
    public static List<Vector2> tilesAffectedList;
    public static Dictionary<GameLocation, HashSet<Vector2>> tilesToWaterNextDay = new();
    private static IMonitor Monitor;
    private static Dictionary<string,int> crops;

    public static void WateringRewards(GameLocation location, Tool tool, bool treasureCaught)
    {
        crops = new  Dictionary<string,int>();
        int num = 0;
        foreach (Vector2 tile in tilesAffectedList)
        {
            if (location.terrainFeatures.ContainsKey(tile) && location.terrainFeatures[tile] is HoeDirt dirt)
            {
                if (tilesToWaterNextDay.ContainsKey(location))
                {
                    if (tilesToWaterNextDay[location].Add(tile))
                    {
                        if (dirt.crop != null)
                        {
                            string key = dirt.crop.indexOfHarvest.Value;
                            if (!crops.TryAdd(key, 1))
                            {
                                crops[key]++;
                            }
                        }
                    }
                }
                else
                {
                    tilesToWaterNextDay[location] = new HashSet<Vector2>() { tile };
                    if (dirt.crop != null)
                    {
                        string key = dirt.crop.indexOfHarvest.Value;
                        if (!crops.TryAdd(key, 1))
                        {
                            crops[key]++;
                        }
                    }
                }
            }
            
            Game1.Multiplayer.broadcastSprites(location, new TemporaryAnimatedSprite(13, new Vector2(tile.X * 64f, tile.Y * 64f), Color.White, 10, Game1.random.NextBool(), 70f, sourceRectWidth: 64, layerDepth: (float) (((double) tile.Y * 64.0 + 32.0) / 10000.0 - 0.009999999776482582))
            {
                delayBeforeAnimationStart = 250 + num * 10
            });
            ++num;
        }
        
        
        if (treasureCaught)
        {
            tool.lastUser.playNearbySoundLocal("openChest");
            location.temporarySprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors",
                new Rectangle(64, 1920, 32, 32), 200f, 4, 0, tool.lastUser.Position + new Vector2(-32f, -228f), false,
                false, (float)((double)tool.lastUser.StandingPixel.Y / 10000.0 + 1.0 / 1000.0), 0.0f, Color.White, 4f,
                0.0f, 0.0f, 0.0f)
            {
                endFunction = openWateringTreasureMenu
            });
        }

    }

    public static void openWateringTreasureMenu(int extraInfo)
    {
        List<Item> inventory = new List<Item>();
        foreach (var s in crops)
        {
            inventory.Add(ItemRegistry.Create(s.Key, s.Value));
        }
        ItemGrabMenu itemGrabMenu = new ItemGrabMenu(inventory).setEssential(true);
        itemGrabMenu.source = 70;
        Game1.activeClickableMenu = itemGrabMenu;
    }

    public static void Post_tilesAffected(ref List<Vector2> __result, Vector2 tileLocation, int power, Farmer who)
    {
        tilesAffectedList = __result;
    }

    public static void Post_wateringCanReleased(WateringCan __instance, GameLocation location, int x, int y, int power,
        Farmer who)
    {
        chargingUpTime = TimeSpan.Zero;
        if (playerDidChargeUp)
        {
            playerDidChargeUp = false;
            if (!__instance.isEfficient.Value)
                who.Stamina -= 2 * (power + 1) - who.FarmingLevel * 0.1f;
            if (!__instance.IsBottomless)
                __instance.WaterLeft -= power + 1;

            //bool treasure = Game1.random.NextDouble() < FishingRod.baseChanceForTreasure + who.LuckLevel * 0.005 + who.DailyLuck / 2.0;
            bool treasure = true;
            DeluxeFishingRodTool.PlayHitEffectForRandomEncounter(who, new WateringBobberBar(location, __instance, treasure));
        }
        else
        {
            tilesAffectedList = new List<Vector2>();
        }
    }

    public static void Post_toolCharging(Farmer __instance)
    {
        int hasReaching = __instance.CurrentTool.hasEnchantmentOfType<ReachingToolEnchantment>() ? 1 : 0;
        if (__instance.CurrentTool.UpgradeLevel + hasReaching == __instance.toolPower.Value)
        {
            chargingUpTime = Game1.currentGameTime.TotalGameTime + TimeSpan.FromMilliseconds(800);
        }
    }

    public static bool Pre_toolDraw(Tool __instance, SpriteBatch b)
    {
        if (!playerDidChargeUp || __instance is not WateringCan)
        {
            return true;
        }

        Farmer lastUser = __instance.lastUser;
        if (lastUser == null || !__instance.lastUser.canReleaseTool || !__instance.lastUser.IsLocalPlayer)
        {
            return true;
        }
        
        foreach (var vector2 in tilesAffected(__instance.lastUser.GetToolLocation() / 64f,
                     __instance.lastUser.toolPower.Value, __instance.lastUser))
            b.Draw(ObjectIds.fishingTextures,
                Game1.GlobalToLocal(new Vector2((int)vector2.X * 64, (int)vector2.Y * 64)),
                new Rectangle(237, 490, 16, 16), Color.White, 0.0f, Vector2.Zero, 4f, SpriteEffects.None, 0.01f);

        return false;
    }

    #region Copied functions from code because idk reflection
    public static void PlayToolIncreaseAnimation(Farmer farmer)
    {
        var color = Color.White;
        var num = farmer.FacingDirection == 0 ? 4 : farmer.FacingDirection == 2 ? 2 : 0;
        switch (farmer.toolPower.Value + 1)
        {
            case 1:
                color = Color.Orange;
                if (!(farmer.CurrentTool is WateringCan))
                    farmer.FarmerSprite.CurrentFrame = 72 + num;
                farmer.jitterStrength = 0.25f;
                break;
            case 2:
                color = Color.LightSteelBlue;
                if (!(farmer.CurrentTool is WateringCan))
                    ++farmer.FarmerSprite.CurrentFrame;
                farmer.jitterStrength = 0.5f;
                break;
            case 3:
                color = Color.Gold;
                farmer.jitterStrength = 1f;
                break;
            case 4:
                color = Color.Violet;
                farmer.jitterStrength = 2f;
                break;
            case 5:
                color = Color.BlueViolet;
                farmer.jitterStrength = 3f;
                break;
            case 6:
                color = Color.Blue;
                farmer.jitterStrength = 4f;
                break;
        }

        var x = 0;
        switch (farmer.FacingDirection)
        {
            case 1:
                x = 40;
                break;
            case 3:
                x = -40;
                break;
            case 2:
                x = 32;
                break;
            default:
                x = 0;
                break;
        }

        var y1 = 192;
        if (farmer.CurrentTool is WateringCan)
        {
            switch (farmer.FacingDirection)
            {
                case 1:
                    x = -48;
                    break;
                case 2:
                    x = 0;
                    break;
                case 3:
                    x = 48;
                    break;
            }

            y1 = 128;
        }

        int y2 = farmer.StandingPixel.Y;
        Game1.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(21,
            farmer.Position - new Vector2(x, y1), color, animationInterval: 70f,
            sourceRectWidth: 64, layerDepth: (float)(y2 / 10000.0 + 0.004999999888241291),
            sourceRectHeight: 128));
        Game1.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations",
            new Rectangle(192, 1152, 64, 64), 50f, 4, 0,
            farmer.Position - new Vector2(farmer.FacingDirection == 1 ? 0.0f : -64f, 128f), false,
            farmer.FacingDirection == 1, y2 / 10000f, 0.01f, Color.White, 1f, 0.0f, 0.0f, 0.0f));
        Game1.playSound("toolCharge",
            Utility.CreateRandom(Game1.dayOfMonth, (double)farmer.Position.X * 1000.0,
                (double)farmer.Position.Y).Next(12, 16) * 100 + (farmer.toolPower.Value + 1) * 100);
    }
    public static List<Vector2> tilesAffected(Vector2 tileLocation, int power, Farmer who)
    {
      ++power;
      List<Vector2> vector2List = new List<Vector2>();
      vector2List.Add(tileLocation);
      Vector2 vector2 = Vector2.Zero;
      switch (who.FacingDirection)
      {
        case 0:
          if (power >= 6)
          {
            vector2 = new Vector2(tileLocation.X, tileLocation.Y - 2f);
            break;
          }
          if (power >= 2)
          {
            vector2List.Add(tileLocation + new Vector2(0.0f, -1f));
            vector2List.Add(tileLocation + new Vector2(0.0f, -2f));
          }
          if (power >= 3)
          {
            vector2List.Add(tileLocation + new Vector2(0.0f, -3f));
            vector2List.Add(tileLocation + new Vector2(0.0f, -4f));
          }
          if (power >= 4)
          {
            vector2List.RemoveAt(vector2List.Count - 1);
            vector2List.RemoveAt(vector2List.Count - 1);
            vector2List.Add(tileLocation + new Vector2(1f, -2f));
            vector2List.Add(tileLocation + new Vector2(1f, -1f));
            vector2List.Add(tileLocation + new Vector2(1f, 0.0f));
            vector2List.Add(tileLocation + new Vector2(-1f, -2f));
            vector2List.Add(tileLocation + new Vector2(-1f, -1f));
            vector2List.Add(tileLocation + new Vector2(-1f, 0.0f));
          }
          if (power >= 5)
          {
            for (int index = vector2List.Count - 1; index >= 0; --index)
              vector2List.Add(vector2List[index] + new Vector2(0.0f, -3f));
            break;
          }
          break;
        case 1:
          if (power >= 6)
          {
            vector2 = new Vector2(tileLocation.X + 2f, tileLocation.Y);
            break;
          }
          if (power >= 2)
          {
            vector2List.Add(tileLocation + new Vector2(1f, 0.0f));
            vector2List.Add(tileLocation + new Vector2(2f, 0.0f));
          }
          if (power >= 3)
          {
            vector2List.Add(tileLocation + new Vector2(3f, 0.0f));
            vector2List.Add(tileLocation + new Vector2(4f, 0.0f));
          }
          if (power >= 4)
          {
            vector2List.RemoveAt(vector2List.Count - 1);
            vector2List.RemoveAt(vector2List.Count - 1);
            vector2List.Add(tileLocation + new Vector2(0.0f, -1f));
            vector2List.Add(tileLocation + new Vector2(1f, -1f));
            vector2List.Add(tileLocation + new Vector2(2f, -1f));
            vector2List.Add(tileLocation + new Vector2(0.0f, 1f));
            vector2List.Add(tileLocation + new Vector2(1f, 1f));
            vector2List.Add(tileLocation + new Vector2(2f, 1f));
          }
          if (power >= 5)
          {
            for (int index = vector2List.Count - 1; index >= 0; --index)
              vector2List.Add(vector2List[index] + new Vector2(3f, 0.0f));
            break;
          }
          break;
        case 2:
          if (power >= 6)
          {
            vector2 = new Vector2(tileLocation.X, tileLocation.Y + 2f);
            break;
          }
          if (power >= 2)
          {
            vector2List.Add(tileLocation + new Vector2(0.0f, 1f));
            vector2List.Add(tileLocation + new Vector2(0.0f, 2f));
          }
          if (power >= 3)
          {
            vector2List.Add(tileLocation + new Vector2(0.0f, 3f));
            vector2List.Add(tileLocation + new Vector2(0.0f, 4f));
          }
          if (power >= 4)
          {
            vector2List.RemoveAt(vector2List.Count - 1);
            vector2List.RemoveAt(vector2List.Count - 1);
            vector2List.Add(tileLocation + new Vector2(1f, 2f));
            vector2List.Add(tileLocation + new Vector2(1f, 1f));
            vector2List.Add(tileLocation + new Vector2(1f, 0.0f));
            vector2List.Add(tileLocation + new Vector2(-1f, 2f));
            vector2List.Add(tileLocation + new Vector2(-1f, 1f));
            vector2List.Add(tileLocation + new Vector2(-1f, 0.0f));
          }
          if (power >= 5)
          {
            for (int index = vector2List.Count - 1; index >= 0; --index)
              vector2List.Add(vector2List[index] + new Vector2(0.0f, 3f));
            break;
          }
          break;
        case 3:
          if (power >= 6)
          {
            vector2 = new Vector2(tileLocation.X - 2f, tileLocation.Y);
            break;
          }
          if (power >= 2)
          {
            vector2List.Add(tileLocation + new Vector2(-1f, 0.0f));
            vector2List.Add(tileLocation + new Vector2(-2f, 0.0f));
          }
          if (power >= 3)
          {
            vector2List.Add(tileLocation + new Vector2(-3f, 0.0f));
            vector2List.Add(tileLocation + new Vector2(-4f, 0.0f));
          }
          if (power >= 4)
          {
            vector2List.RemoveAt(vector2List.Count - 1);
            vector2List.RemoveAt(vector2List.Count - 1);
            vector2List.Add(tileLocation + new Vector2(0.0f, -1f));
            vector2List.Add(tileLocation + new Vector2(-1f, -1f));
            vector2List.Add(tileLocation + new Vector2(-2f, -1f));
            vector2List.Add(tileLocation + new Vector2(0.0f, 1f));
            vector2List.Add(tileLocation + new Vector2(-1f, 1f));
            vector2List.Add(tileLocation + new Vector2(-2f, 1f));
          }
          if (power >= 5)
          {
            for (int index = vector2List.Count - 1; index >= 0; --index)
              vector2List.Add(vector2List[index] + new Vector2(-3f, 0.0f));
            break;
          }
          break;
      }
      if (power >= 6)
      {
        vector2List.Clear();
        for (int x = (int) vector2.X - 2; (double) x <= (double) vector2.X + 2.0; ++x)
        {
          for (int y = (int) vector2.Y - 2; (double) y <= (double) vector2.Y + 2.0; ++y)
            vector2List.Add(new Vector2((float) x, (float) y));
        }
      }
      return vector2List;
    }
    #endregion
}
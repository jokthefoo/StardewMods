using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Enchantments;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace FishMod;

public class AxeFishing
{
    private static IMonitor Monitor;

    internal static bool TreeChopping_prefix(GameLocation location, int x, int y, int power, Farmer who)
    {
        try
        {
            if (who.CurrentTool is not Axe)
            {
                return true;
            }

            Tool t = who.CurrentTool;
            t.lastUser = who;

            int num1 = x / 64;
            int num2 = y / 64;
            Vector2 tile = new Vector2(num1, num2);
            if (location.terrainFeatures.TryGetValue(tile, out TerrainFeature terrainFeature))
            {
                if (terrainFeature is Tree tree && tree.growthStage.Value >= 3 &&
                    !tree.tapped.Value && tree.health.Value > 0) // tree no tapper
                {
                    if (!inTownCheck(location, tile, tree)) // weird check false means we don't want to allow
                    {
                        return true;
                    }

                    StartTreeChop(t, tree, tile);
                    return false;
                }
            }

            if (location.resourceClumps != null && t.upgradeLevel.Value > 0)
            {
                foreach (ResourceClump? clump in location.resourceClumps)
                {
                    if (clump != null && clump.getBoundingBox().Contains(x, y) && clump.health.Value > 0)
                    {
                        if (clump.parentSheetIndex.Value == 600 || clump.parentSheetIndex.Value == 602)
                        {
                            StartLogChop(clump, t, tile);
                            return false;
                        }
                    }
                }
            }

            // Do normal action
            return true;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed in {nameof(TreeChopping_prefix)}:\n{ex}", LogLevel.Error);
            return true; // run original logic --- return false blocks og logic
        }
    }

    private static void StartLogChop(ResourceClump clump, Tool tool, Vector2 tileLocation)
    {
        float toolStrength = tool.UpgradeLevel + (tool.hasEnchantmentOfType<PowerfulEnchantment>() ? 2 : 0);
        int chopAmountRequired = (int)Math.Ceiling(clump.health.Value / toolStrength);

        float stamCost = (2 * 1) - tool.lastUser.ForagingLevel * 0.1f;
        if (!tool.isEfficient.Value)
            tool.lastUser.Stamina -= stamCost * chopAmountRequired;

        for (int i = 0; i < chopAmountRequired; i++)
        {
            if (!tool.hasEnchantmentOfType<ShavingEnchantment>())
            {
                continue;
            }

            float num = Math.Max(1f, (toolStrength + 1) * 0.75f);
            if (Game1.random.NextDouble() <= num / 12.0)
            {
                var debris = new Debris(709,
                    new Vector2((float)(tileLocation.X * 64.0 + 32.0),
                        (float)((tileLocation.Y - 0.5) * 64.0 + 32.0)), Game1.player.getStandingPosition());
                debris.Chunks[0].xVelocity.Value += Game1.random.Next(-10, 11) / 10f;
                debris.chunkFinalYLevel = (int)(tileLocation.Y * 64.0 + 64.0);
                clump.Location.debris.Add(debris);
            }
        }

        clump.shakeTimer = 100f;

        // Start minigame
        clump.Location.playSound("axchop", tileLocation);
        bool treasure = Game1.random.NextDouble() < FishingRod.baseChanceForTreasure + tool.lastUser.LuckLevel * 0.005 + tool.lastUser.DailyLuck / 2.0;
        Game1.activeClickableMenu = new TreeBobberBar(clump.Location, treasure, chopAmountRequired, tool, tileLocation);
    }

    private static bool inTownCheck(GameLocation location, Vector2 tileLocation, Tree tree)
    {
        if (location is Town && (double)tileLocation.X < 100.0 && !tree.isTemporaryGreenRainTree.Value)
        {
            switch (location.getTileIndexAt((int)tileLocation.X, (int)tileLocation.Y, "Paths"))
            {
                case 9:
                case 10:
                case 11:
                    tree.shake(tileLocation, true);
                    Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\1_6_Strings:TownTreeWarning"));
                    return false;
            }
        }

        return true;
    }

    private static void StartTreeChop(Tool tool, Tree tree, Vector2 tileLocation)
    {
        int chopAmountRequired = 10;
        switch (tool.UpgradeLevel + (tool.hasEnchantmentOfType<PowerfulEnchantment>() ? 2 : 0))
        {
            case 0:
                chopAmountRequired = 10;
                break;
            case 1:
                chopAmountRequired = 8;
                break;
            case 2:
                chopAmountRequired = 6;
                break;
            case 3:
                chopAmountRequired = 4;
                break;
            case 4:
                chopAmountRequired = 2;
                break;
            default:
                chopAmountRequired = 1;
                break;
        }

        if (tree.growthStage.Value < 5 || tree.stump.Value)
        {
            chopAmountRequired /= 2;
        }

        tree.shake(tileLocation, false);

        float stamCost = (2 * 1) - tool.lastUser.ForagingLevel * 0.1f;
        if (!tool.isEfficient.Value)
            tool.lastUser.Stamina -= stamCost * chopAmountRequired;

        for (int i = 0; i < chopAmountRequired; i++)
        {
            if (!tool.hasEnchantmentOfType<ShavingEnchantment>())
            {
                continue;
            }

            if (Game1.random.NextDouble() <= 2f / chopAmountRequired)
            {
                string itemid = "388";
                switch (tree.treeType.Value)
                {
                    case "12":
                        itemid = "(O)259";
                        break;
                    case "7":
                        itemid = "(O)420";
                        break;
                    case "8":
                        itemid = "(O)709";
                        break;
                }

                Vector2 debrisOrigin = new Vector2((float)(tileLocation.X * 64.0 + 32.0),
                    (float)((tileLocation.Y - 0.5) * 64.0 + 32.0));
                Debris debris = new Debris(itemid, debrisOrigin, Game1.player.getStandingPosition());
                debris.Chunks[0].xVelocity.Value += Game1.random.Next(-10, 11) / 10f;
                debris.chunkFinalYLevel = (int)(tileLocation.Y * 64.0 + 64.0);
                tree.Location.debris.Add(debris);
            }
        }

        // Start minigame
        tree.Location.playSound("axchop", new Vector2?(tileLocation));
        bool treasure = true;//Game1.random.NextDouble() < FishingRod.baseChanceForTreasure + tool.lastUser.LuckLevel * 0.005 + tool.lastUser.DailyLuck / 2.0;
        Game1.activeClickableMenu = new TreeBobberBar(tree.Location, treasure, chopAmountRequired, tool, tileLocation);
    }

    internal static void TreeRewards(GameLocation location, Tool tool, Vector2 tileLocation, int chopAmountRequired, bool treasureCaught)
    {
        if (location.terrainFeatures.TryGetValue(tileLocation, out TerrainFeature terrainFeature))
        {
            if (terrainFeature is Tree tree)
            {
                // kill tree
                if (tree.health.Value > 0)
                {
                    tree.health.Value = -1f;
                    if (tree.performToolAction(tool, 0, tileLocation))
                    {
                        tree.Location.terrainFeatures.Remove(tileLocation);
                    }
                }

                // various rewards that usually are gotten while swinging
                tree.lastPlayerToHit.Value = tool.getLastFarmerToUse().UniqueMultiplayerID;
                Farmer farmer = tool.getLastFarmerToUse();
                if (farmer != null && location.HasUnlockedAreaSecretNotes(farmer) &&
                    Game1.random.NextDouble() < 0.005 * chopAmountRequired)
                {
                    StardewValley.Object unseenSecretNote = location.tryToCreateUnseenSecretNote(farmer);
                    if (unseenSecretNote != null)
                        Game1.createItemDebris((Item)unseenSecretNote,
                            new Vector2(tileLocation.X, tileLocation.Y - 3f) * 64f, -1, location,
                            Game1.player.StandingPixel.Y - 32);
                }
                else if (farmer != null && Utility.tryRollMysteryBox(0.005 * chopAmountRequired))
                    Game1.createItemDebris(
                        ItemRegistry.Create(farmer.stats.Get(StatKeys.Mastery(2)) > 0U
                            ? "(O)GoldenMysteryBox"
                            : "(O)MysteryBox"), new Vector2(tileLocation.X, tileLocation.Y - 3f) * 64f, -1,
                        location,
                        Game1.player.StandingPixel.Y - 32);
                else if (farmer != null &&
                         farmer.stats.Get("TreesChopped") > 20U && Game1.random.NextDouble() <
                         (0.0003 * chopAmountRequired) + (farmer.mailReceived.Contains("GotWoodcuttingBook")
                             ? (0.0007 * chopAmountRequired)
                             : farmer.stats.Get("TreesChopped") * 1E-05))
                {
                    Game1.createItemDebris(ItemRegistry.Create("(O)Book_Woodcutting"),
                        new Vector2(tileLocation.X, tileLocation.Y - 3f) * 64f, -1, location,
                        Game1.player.StandingPixel.Y - 32);
                    farmer.mailReceived.Add("GotWoodcuttingBook");
                }
                else
                {
                    Utility.trySpawnRareObject(Game1.player, new Vector2(tileLocation.X, tileLocation.Y - 3f) * 64f,
                        tree.Location, 0.33 * chopAmountRequired, groundLevel: Game1.player.StandingPixel.Y - 32);
                }
            }
        }

        foreach (ResourceClump? clump in location.resourceClumps)
        {
            if (clump != null && clump.getBoundingBox().Contains(tileLocation.X * 64, tileLocation.Y * 64))
            {
                clump.health.Value = 0;
                location.performToolAction(tool, (int)tileLocation.X, (int)tileLocation.Y);
                clump.destroy(tool, location, tileLocation);
                location.resourceClumps.Remove(clump);
                break;
            }
        }

        if (treasureCaught)
        {
            tool.lastUser.playNearbySoundLocal("openChest");
            location.temporarySprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(64, 1920, 32, 32), 200f, 4, 0, tool.lastUser.Position + new Vector2(-32f, -228f), false, false, (float) ((double) tool.lastUser.StandingPixel.Y / 10000.0 + 1.0 / 1000.0), 0.0f, Color.White, 4f, 0.0f, 0.0f, 0.0f)
            {
                endFunction = openWoodTreasureMenu
            });
        }
    }

    public static void openWoodTreasureMenu(int extraInfo)
    {
        List<Item> inventory = new List<Item>();
        inventory.Add(ItemRegistry.Create("(O)PrizeTicket")); // TODO: wood treasures
        ItemGrabMenu itemGrabMenu = new ItemGrabMenu(inventory).setEssential(true);
        itemGrabMenu.source = 69;
        Game1.activeClickableMenu = itemGrabMenu;
    }
}
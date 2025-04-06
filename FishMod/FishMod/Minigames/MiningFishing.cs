using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace FishMod;

public class MiningFishing
{
    private static int[] rewards = new int[4];
    
    public static void Post_PickaxeSwing(Pickaxe __instance, GameLocation location, int x, int y, int power, Farmer who)
    {
        if (!ModEntry.Config.MiningMiniGameEnabled)
        {
            return;
        }

        who.Stamina -= 30f / __instance.UpgradeLevel + 1 - who.MiningLevel * 0.1f ;
        if (location.resourceClumps != null)
        {
            foreach (ResourceClump? clump in location.resourceClumps)
            {
                if (clump != null && clump.getBoundingBox().Contains(x, y) && clump.health.Value > 0)
                {
                    string value;
                    if (clump.modData.TryGetValue("spacechase0.SpaceCore/LargeMinable", out value) && value == "Jok.Fishdew.CP_SpawnFishingRock")
                    {
                        // Start minigame
                        DeluxeFishingRodTool.PlayHitEffectForRandomEncounter(who, new MiningBobberBar(location, __instance, new Vector2(x / 64, y / 64)));
                    }
                }
            }
        }
    }

    public static void MiningRewards(GameLocation location, Tool tool, Vector2 tileLocation, int slimeCount, int rockCount, int mineralCount,
        int omniCount)
    {
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
        
        rewards[0] = slimeCount;
        rewards[1] = rockCount;
        rewards[2] = mineralCount;
        rewards[3] = omniCount;

        int locForSlimeRewards = 0;
        if (location is MineShaft mine)
        {
            if (Game1.random.NextDouble() < .15)
            {
                mine.createLadderAt(tileLocation, "newArtifact");
            }
            
            if (mine.mineLevel < 30)
            {
                locForSlimeRewards = 0;
            }
            else if (mine.mineLevel < 80)
            {
                locForSlimeRewards = 1;
            }
            else if (mine.mineLevel < 120)
            {
                locForSlimeRewards = 2;
            }
            else
            {
                locForSlimeRewards = 3;
            }
        }
        
        tool.lastUser.playNearbySoundLocal("openChest");
        location.temporarySprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors",
            new Rectangle(64, 1920, 32, 32), 200f, 4, 0, tool.lastUser.Position + new Vector2(-32f, -228f), false,
            false, (float)((double)tool.lastUser.StandingPixel.Y / 10000.0 + 1.0 / 1000.0), 0.0f, Color.White, 4f,
            0.0f, 0.0f, 0.0f)
        {
            endFunction = openMiningTreasureMenu,
            extraInfoForEndBehavior = locForSlimeRewards
        });
    }
    
    public static void openMiningTreasureMenu(int slimeRewardInfo)
    {
        Game1.player.gainExperience(Farmer.miningSkill, 3*rewards[1]);
        Game1.player.gainExperience(Farmer.miningSkill, 16*rewards[2]);
        Game1.player.gainExperience(Farmer.combatSkill, rewards[0] * Math.Max(3, Game1.player.CombatLevel + 1));
        
        List<Item> inventory = new List<Item>();

        var luckLevel = Game1.player.LuckLevel * 0.007 + Game1.player.DailyLuck / 10.0;
        // Ultra rares
        if (Game1.player.currentLocation is MineShaft mine && Game1.random.NextDouble() < luckLevel)
        {
            inventory.Add(MineShaft.getSpecialItemForThisMineLevel(mine.mineLevel, Game1.player.experiencePoints[3], Game1.player.experiencePoints[4]));
        } else if (Game1.random.NextDouble() < .005 && Game1.player.timesReachedMineBottom > 0)
        {
            inventory.Add(ItemRegistry.Create("74")); // prismatic shard
        } else if (Game1.random.NextDouble() < .003 && Game1.player.hasCompletedCommunityCenter())
        {
            inventory.Add(ItemRegistry.Create("(BC)272")); // auto-petter
        } else if (Game1.random.NextDouble() < .01)
        {
            inventory.Add(ItemRegistry.Create("(O)MysteryBox"));
        }

        for(int i = 0; i < rewards.Length; i++)
        {
            switch (i)
            {
                case 0: // slime rewards
                    int slimeCount = 0;
                    int sapCount = 0;
                    int specialCount = 0;
                    for (int j = 0; j < rewards[i]; j++)
                    {
                        if (Game1.random.NextDouble() < .75)
                        {
                            slimeCount++;
                        }
                        if (Game1.random.NextDouble() < .5)
                        {
                            sapCount++;
                        }
                        
                        if (Game1.random.NextDouble() < .009)
                        {
                            specialCount++;
                        }
                    }

                    if (slimeCount > 0)
                    {
                        inventory.Add(ItemRegistry.Create("766", slimeCount)); // slime
                    }
                    if (sapCount > 0)
                    {
                        inventory.Add(ItemRegistry.Create("92", sapCount)); // sap
                    }

                    if (specialCount > 0)
                    {
                        switch (slimeRewardInfo)
                        {
                            case 0:
                                inventory.Add(ItemRegistry.Create("66", specialCount)); // amethyst
                                break;
                            case 1:
                                inventory.Add(ItemRegistry.Create("70", specialCount)); // jade
                                break;
                            case 2:
                                inventory.Add(ItemRegistry.Create("72", specialCount)); // diamond
                                break;
                            case 3:
                                inventory.Add(ItemRegistry.Create("337", specialCount)); // iridium bar
                                break;
                        }
                    }
                    break;
                case 1: // rock rewards
                    int coalCount = 0;
                    int stoneCount = 0;
                    for (int j = 0; j < rewards[i]; j++)
                    {
                        if (Game1.random.NextDouble() < 0.1)
                        {
                            coalCount++;
                        }
                        stoneCount++;
                    }
                    inventory.Add(ItemRegistry.Create("390", stoneCount)); // stone
                    inventory.Add(ItemRegistry.Create("382", coalCount)); // coal
                    break;
                case 2: // mineral rewards
                    int[] mineralCounts = new int[7];
                    for (int j = 0; j < rewards[i]; j++)
                    {
                        mineralCounts[Game1.random.Next(0, 7)]++;
                    }

                    for (int j = 0; j < mineralCounts.Length; j++)
                    {
                        if (mineralCounts[j] == 0)
                        {
                            continue;
                        }
                        switch (j)
                        {
                            case 0:
                                inventory.Add(ItemRegistry.Create("60", mineralCounts[j])); // emerald
                                break;
                            case 1:
                                inventory.Add(ItemRegistry.Create("62", mineralCounts[j])); // aquamarine
                                break;
                            case 2:
                                inventory.Add(ItemRegistry.Create("64", mineralCounts[j])); // ruby
                                break;
                            case 3:
                                inventory.Add(ItemRegistry.Create("66", mineralCounts[j])); // amethyst
                                break;
                            case 4:
                                inventory.Add(ItemRegistry.Create("68", mineralCounts[j])); // topaz
                                break;
                            case 5:
                                inventory.Add(ItemRegistry.Create("70", mineralCounts[j])); // jade
                                break;
                            case 6:
                                inventory.Add(ItemRegistry.Create("72", mineralCounts[j])); // diamond
                                break;
                        }
                    }
                    break;
                case 3: // omni rewards
                    // stone, clay, coal, copper, iron, gold, iridium, prismatic shard, earth crystal, frozen tear, fire quartz
                    int[] objects = { 390, 330, 382, 378, 380, 384, 386, 74, 86, 84, 82 };
                    double[] probabilities = { 1/8f, 1/16f, 1/20f, 1/20f, 1/20f, 1/20f, 1/20f, 1/250f, 1/48f, 1/48f, 1/48f };

                    Dictionary<int, int> omniCounts = new Dictionary<int, int>();

                    for (int j = 0; j < rewards[i]; j++)
                    {
                        var winner = WeightedChoice(objects, probabilities);

                        int amount = 1;
                        if (winner > 333)
                        {
                            int[] amounts = { 1, 3, 5, 10, 20};
                            double[] probs = { .3, .3, .3, .9, .1 };
                            amount = WeightedChoice(amounts, probs);

                            if (winner == 390 && Game1.random.NextBool())
                            {
                                amount = 1;
                            }

                            if (winner == 386)
                            {
                                int[] iridiumAmounts = { 1, 2, 3, 6, 11};
                                amount = WeightedChoice(iridiumAmounts, probs);
                            }
                        }
                        
                        if (omniCounts.TryGetValue(winner, out int count))
                        {
                            omniCounts[winner] = count + amount;
                        }
                        else
                        {
                            omniCounts[winner] = amount;
                        }
                    }

                    foreach (var pair in omniCounts)
                    {
                        if (pair.Value > 0)
                        {
                            inventory.Add(ItemRegistry.Create(pair.Key.ToString(), pair.Value));
                        }
                    }
                    break;
            }
        }
        
        ItemGrabMenu itemGrabMenu = new ItemGrabMenu(inventory).setEssential(true);
        itemGrabMenu.source = 71;
        Game1.activeClickableMenu = itemGrabMenu;
    }
    
    public static T WeightedChoice<T>(T[] items, double[] weights)
    {
        double totalWeight = weights.Sum();
        double rand = Game1.random.NextDouble() * totalWeight;
        
        for (int i = 0; i < items.Length; i++)
        {
            if (rand < weights[i])
                return items[i];
            rand -= weights[i];
        }
        return items.First();
    }
}
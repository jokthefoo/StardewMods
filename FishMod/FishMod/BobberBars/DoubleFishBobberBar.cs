using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FishMod;

public class DoubleFishBobberBar : AdvBobberBar
{
    private bool spawnedSecond = false;
    private bool secondFishInBar = false;

    TreasureInstance secondFish;
    
    public DoubleFishBobberBar(string whichFish, float fishSize, int treasure, List<string> bobbers, string setFlagOnCatch,
        bool isBossFish = false, string? baitID = "", bool goldenTreasure = false, int colorIndex = -1)
        : base(whichFish, fishSize, 0, bobbers, setFlagOnCatch, isBossFish, baitID, goldenTreasure, colorIndex)
    {
    }

    public override void update(GameTime time)
    {
        base.update(time);
        if (distanceFromCatching > .5f && !spawnedSecond)
        {
            spawnedSecond = true;
            int diff = (int)difficulty + Game1.random.Next((int)(-difficulty / 2), (int)(difficulty / 2 + 1));
            secondFish = new MovingTreasureFishStyle(diff, (MovingTreasureFishStyle.MotionType)motionType, 10, 10);
            secondFish.initTreasurePosition = fishPosition;
            secondFish.treasureProgressColor = Color.Coral;
            secondFish.waitForScaling = true;
            secondFish.isSpecial = true;
            secondFish.decreaseRate /= 4;
            secondFish.increaseRate /= 3;
        }
    }

    protected override void TreasureUpdate(GameTime time)
    {
        treasureInBar = false;
        foreach (var t in treasures)
            if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
                treasureInBar = true;

        secondFishInBar = false;
        if (spawnedSecond && secondFish.treasureUpdate(time, bobberBarPos, bobberBarHeight))
            secondFishInBar = true;
    }
    
    protected override void DrawTreasures(SpriteBatch b)
    {
        foreach (var t in treasures) 
            t.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);
        
        if(spawnedSecond)
            secondFish.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);
    }
    
    protected override void DecreaseProgress()
    {
        if (secondFishInBar)
        {
            return;
        }
        
        if (bobbers.Contains("(O)694")) // Trap bobber makes it easier
        {
            var reduction = 0.003f;
            var amount = 0.001f;
            for (var i = 0; i < Utility.getStringCountInList(bobbers, "(O)694"); i++)
            {
                reduction -= amount;
                amount /= 2f;
            }

            reduction = Math.Max(0.001f, reduction);
            distanceFromCatching -= reduction * distanceFromCatchPenaltyModifier;
        }
        else
        {
            distanceFromCatching -= 0.003f * distanceFromCatchPenaltyModifier;
        }
    }
    
    public override void CheckVictory()
    {
        if (distanceFromCatching >= 1f && secondFish.treasureCaught)
        {
            VictoryEffects();
        }
    }
}
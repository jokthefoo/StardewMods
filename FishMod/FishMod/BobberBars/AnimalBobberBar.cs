using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FishMod;

public class AnimalBobberBar : AdvBobberBar
{
    public AnimalBobberBar(int treasure, List<int> animals, int colorIndex = -1)
        : base("136", 50, treasure, new List<string>(), "", false, "", false, colorIndex)
    {
    }

    public override void update(GameTime time)
    {
        base.update(time);
    }

    protected override void TreasureUpdate(GameTime time)
    {
        treasureInBar = false;
        foreach (var t in treasures)
            if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
                treasureInBar = true;
    }
    
    protected override void DrawTreasures(SpriteBatch b)
    {
        foreach (var t in treasures) 
            t.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);
    }
    
    protected override void DecreaseProgress()
    { 
        distanceFromCatching -= 0.003f * distanceFromCatchPenaltyModifier;
    }
    
    public override void CheckVictory()
    {
        if (distanceFromCatching >= 1f)
        {
            VictoryEffects();
        }
    }
}
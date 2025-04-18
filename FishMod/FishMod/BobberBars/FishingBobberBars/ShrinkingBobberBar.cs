using Microsoft.Xna.Framework;

namespace FishMod;

public class ShrinkingBobberBar : AdvBobberBar
{
    private int bobberBarHeightMax;
    private int bobberBarHeightMin;
    
    public ShrinkingBobberBar(string whichFish, float fishSize, int treasure, List<string> bobbers, string setFlagOnCatch,
        bool isBossFish = false, string? baitID = "", bool goldenTreasure = false, int colorIndex = -1)
        : base(whichFish, fishSize, treasure, bobbers, setFlagOnCatch, isBossFish, baitID, goldenTreasure, colorIndex)
    {
        bobberBarHeightMax = bobberBarHeight * 2;
        bobberBarHeightMin = bobberBarHeight / 2;
    }

    public override void update(GameTime time)
    {
        if (!preGameFadingIn)
        {
            float revDist= 1 - distanceFromCatching;
            bobberBarHeight = (int)(bobberBarHeightMin * (1 - revDist) + bobberBarHeightMax * revDist);
        }
            
        base.update(time);
    }
}
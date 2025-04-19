using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FishMod;

public class AnimalBobberBar : BasicBobberBar
{
    private float perAnimalProgress;
    public AnimalBobberBar(CallBack completeCallback, List<int> animals, bool treasure, int colorIndex = -1)
        : base(completeCallback, treasure ? 1 : 0, Game1.player.farmingLevel.Value, false, colorIndex)
    {
        perAnimalProgress = 1f / animals.Count;
        foreach (var animalIndex in animals)
        {
            var newTreasure = new MovingTreasure(animalIndex, false, 20, 20);
            newTreasure.difficulty = 20 + Game1.player.farmingLevel.Value * 3;
            newTreasure.catchEffect = TreasureSprites.HeartIcon;
            newTreasure.treasureShakeMultiplier = .5f;
            newTreasure.increaseRate *= 2;
            newTreasure.isSpecial = true;
            newTreasure.treasureProgressColor = Color.White;
            treasures.Add(newTreasure);
        }
        distanceFromCatching = 0.01f;
    }

    public override void update(GameTime time)
    {
        base.update(time);
    }

    protected override void TreasureUpdate(GameTime time)
    {
        treasureInBar = false;
        foreach (var t in treasures)
        {
            bool beforeCaught = t.treasureCaught;
            if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
                treasureInBar = true;
            
            if (!beforeCaught && t.treasureCaught && !t.realTreasure)
            {
                distanceFromCatching += perAnimalProgress;
            }
        }
    }
    
    protected override void DrawTreasures(SpriteBatch b)
    {
        foreach (var t in treasures) 
            t.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);
    }
    
    protected override void IncreaseProgress()
    {
        if (!treasureInBar)
        {
            return;
        }
        barShake = Vector2.Zero;
        Rumble.rumble(0.1f, 1000f);
        unReelSound?.Stop(AudioStopOptions.Immediate);
        if (reelSound == null || reelSound.IsStopped || reelSound.IsStopping || !reelSound.IsPlaying)
            Game1.playSound("fastReel", out reelSound);
    }
    
    protected override void DecreaseProgress()
    {
        if (treasureInBar)
        {
            return;
        }
        Rumble.stopRumbling();
        barShake.X = Game1.random.Next(-10, 11) / 10f;
        barShake.Y = Game1.random.Next(-10, 11) / 10f;
        reelSound?.Stop(AudioStopOptions.Immediate);
        if (unReelSound == null || unReelSound.IsStopped) Game1.playSound("slowReel", 600, out unReelSound);
    }
    
    protected override void DrawBackground(SpriteBatch b)
    {
        // bar background
        b.Draw(DeluxeFishingRodTool.fishingTextures, new Vector2(xPositionOnScreen + 126, yPositionOnScreen + 296) + everythingShake,
            new Rectangle(141, 362, 22, 148), Color.White * scale, 0f, new Vector2(18.5f, 74f) * scale, 4f * scale,
            SpriteEffects.None, 0.01f);
    }
    
    protected override void DrawProgressBar(SpriteBatch b)
    {
        // current level of progress bar
        b.Draw(Game1.staminaRect,
            new Rectangle(xPositionOnScreen + 124 - 8,
                yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatching)), 16,
                (int)(580f * distanceFromCatching)), Utility.getRedToGreenLerpColor(distanceFromCatching));

    }
    public override void CheckVictory()
    {
        if (distanceFromCatching >= 1f)
        {
            VictoryEffects();
        }
    }
}
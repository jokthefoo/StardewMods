using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FishMod;

public class AnimalBobberBar : BasicBobberBar
{
    public static Texture2D pettingBobberBarTextures;
    
    private float perAnimalProgress;
    public AnimalBobberBar(CallBack completeCallback, List<int> animals, bool treasure, int colorIndex = -5)
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
            newTreasure.treasureScaleMaxScale = 1.4f;
            treasures.Add(newTreasure);
        }
        distanceFromCatching = 0.01f;
        
        toolChoppingAngle = MathHelper.ToRadians(220);
        hasToolChopAnim = true;
        chopSoundName = "toolCharge";
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

    protected override void DrawToolAnim(SpriteBatch b)
    {
        b.Draw(DeluxeFishingRodTool.fishingTextures,
            new Vector2(xPositionOnScreen + 10, yPositionOnScreen + 530) + everythingShake, new Rectangle(20 * TreasureSprites.Dino, 0, 20, 24), Color.White, 0f,
            new Vector2(10f, 10f),  4f, SpriteEffects.None, 0.85f);

        // Petting
        b.Draw(pettingBobberBarTextures,
            new Vector2(xPositionOnScreen - 25, yPositionOnScreen + 510) + everythingShake,
            new Rectangle(66, 35, 10, 10), Color.White, toolChoppingAngle, new Vector2(5f, 5f), 4f,
            SpriteEffects.None, 0.9f);
        
        if (debrisAlpha > 0f)
        {
            // Debris
            foreach(Debris d in debris)
            {
                b.Draw(pettingBobberBarTextures,
                    new Vector2(xPositionOnScreen + d.x, yPositionOnScreen + 505 + d.y) + everythingShake,
                    new Rectangle(100 + 3 * d.index, 32, 3, 3), Color.White * debrisAlpha, toolChoppingAngle, new Vector2(0f, 0f), 4f,
                    SpriteEffects.None, 0.9f);
            }
        }
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
        int barXPos = xPositionOnScreen + 54;
        b.Draw(pettingBobberBarTextures, new Vector2(barXPos, yPositionOnScreen + 300 - 14) + everythingShake,
            new Rectangle(1, 13, 48, 153), Color.White * scale, 0f, new Vector2(18.5f, 74f) * scale, 4f * scale,
            SpriteEffects.None, 0.01f);
    }
    
    protected override void DrawProgressBar(SpriteBatch b)
    {
        // current level of progress bar
        b.Draw(Game1.staminaRect,
            new Rectangle(xPositionOnScreen + 128,
                yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatching)), 8,
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
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FishMod;

public class AnimalBobberBar : BasicBobberBar
{
    public static Texture2D pettingBobberBarTextures;
    
    private float perAnimalProgress;
    private int randomAnimal = 0;
    public AnimalBobberBar(CallBack completeCallback, List<int> animals, bool treasure, int colorIndex = -5)
        : base(completeCallback, treasure ? 1 : 0, Game1.player.farmingLevel.Value, false, colorIndex)
    {
        perAnimalProgress = 1f / animals.Count;
        randomAnimal = animals[Game1.random.Next(0, animals.Count)];
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

    private float handPettingXOffset = 0;
    private float handPettingYOffset = 0;
    private bool yPetUp = false;
    private float HeartIconTimer = 0;
    private float HeartIconScale = 1f;
    private bool heartIconGrowing = true;
    private int heartIconState = 0;
    private Vector2 heartIconShake;
    
    protected override void SwingingAnimationUpdate(GameTime time)
    {
        DebrisAlphaUpdate();
        foreach (Debris d in debris)
        {
            DebrisVelocityUpdate(d);
        }
	
        switch (toolAnimState)
        {
            case ToolAnimStates.Chop:
                handPettingXOffset += .5f;
                
                if (handPettingYOffset > 20)
                {
                    yPetUp = false;
                }
                else if(handPettingYOffset < 0)
                {
                    yPetUp = true;
                }

                handPettingYOffset += yPetUp ? .5f : -.5f;
                break;
            case ToolAnimStates.PullBack:
                handPettingXOffset -= .5f;
                handPettingYOffset = 0;
                break;
        }
	
        if (handPettingXOffset < 0 && toolAnimState != ToolAnimStates.Chop)
        {
            //start chop
            toolAnimState = ToolAnimStates.Chop;
        }
	
        if (handPettingXOffset > 40 && toolAnimState != ToolAnimStates.PullBack)
        {
            HeartIconTimer = 3f;
            HeartIconScale = 0;
            heartIconState = 0;
            
            //start backswing
            toolAnimState = ToolAnimStates.PullBack;
            debrisAlpha = 1.0f;
            ConstructDebris();
        }

        if (HeartIconTimer > 0)
        {
            HeartIconTimer -= .05f;
            Math.Clamp(HeartIconTimer, 0, 20);

            switch (heartIconState)
            {
                case 0:
                    HeartIconScale += .07f;
                    break;
                case 1:
                    break;
                case 2:
                    HeartIconScale -= .07f;
                    break;
            }
            Math.Clamp(HeartIconScale, 0, 1.5f);
            
            if (HeartIconTimer < 2.0f && heartIconState == 0)
            {
                heartIconState = 1;
            }
            if (HeartIconTimer < 1f && heartIconState == 1)
            {
                heartIconState = 2;
            }
        }
    }
    
    protected override void DrawToolAnim(SpriteBatch b)
    {
        // Draw animal
        b.Draw(DeluxeFishingRodTool.fishingTextures,
            new Vector2(xPositionOnScreen + 10, yPositionOnScreen + 530) + everythingShake, new Rectangle(20 * randomAnimal, 0, 20, 24), Color.White, 0f,
            new Vector2(10f, 10f),  4f, SpriteEffects.None, 0.85f);

        // pat pat
        b.Draw(pettingBobberBarTextures,
            new Vector2(xPositionOnScreen - 20 + handPettingXOffset, yPositionOnScreen + 510 - handPettingYOffset) + everythingShake,
            new Rectangle(66, 35, 10, 10), Color.White, 0, new Vector2(5f, 5f), 4f,
            SpriteEffects.None, 0.9f);
        
        // draw temporary heart
        if (HeartIconTimer > 0)
        {
            b.Draw(DeluxeFishingRodTool.fishingTextures,
                new Vector2(xPositionOnScreen + 25, yPositionOnScreen + 480) + everythingShake + heartIconShake, new Rectangle(20 * TreasureSprites.HeartIcon, 0, 20, 24), Color.White, 0f,
                new Vector2(10f, 10f), 2f * HeartIconScale, SpriteEffects.None, 0.85f);
        }
        
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
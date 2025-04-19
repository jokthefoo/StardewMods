using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FishMod;

public static class TreasureSprites
{
	public const int Fish = -2;
	public const int NormalChest = -1;
	public const int BlueChest = 0;
	public const int RedChest = 1;
	public const int GreenChest = 2;
	public const int Stick = 3;
	public const int Parsnip = 4;
	public const int WaterDrop = 5;
	public const int Fairy = 6;
	public const int Slime = 7;
	public const int Rock = 8;
	public const int MineralNode = 9;
	public const int OmniGeode = 10;
	public const int BossFish = 11;
	public const int WhiteChicken = 12;
	public const int HeartIcon = 13;
	public const int BrownChicken = 14;
	public const int VoidChicken = 15;
	public const int GoldenChicken = 16;
	public const int BlueChicken = 17;
	public const int Dino = 18;
	public const int Duck = 19;
	public const int Rabbit = 20;
	
	public const int WhiteCow = 501;
	public const int BrownCow = 502;
	public const int Goat = 503;
	public const int Ostrich = 504;
	public const int Pig = 505;
	public const int Sheep = 506;
}

public class TreasureInstance
{
	protected float treasurePosition;
	public float treasureCatchLevel;
	protected float treasureAppearTimer;
	public float treasureScale;
	public float treasureScaleMaxScale = 1f;
	public bool treasureCaught;
	public bool realTreasure;
	public bool goldenTreasure;
	private Vector2 treasureShake;
	public int spriteId;

	public int catchEffect = -1;
	public float playingCatchEffect = 0;
		
	public float initTreasurePosition = 0;
	
	public const int minPos = 8;
	public const int maxPos = 500;

	public float decreaseRate = 0.01f;
	public float increaseRate = 0.0135f;
	public bool canLoseTreasure = false;
	
	public bool lostTreasure = false;
	
	public bool showProgressBar = true;
	
	public float treasureShakeMultiplier = 1f;
	
	public Color treasureProgressColor = Color.Yellow;
	public bool isSpecial = false;
	public bool reverseProgress = false;
	public bool waitForScaling = false;

	public TreasureInstance(int spriteId, bool realTreasure, int treasureAppearMin = 1000, int treasureAppearMax = 3000, bool goldenTreasure = false, bool canLoseTreasure = false)
	{
		this.canLoseTreasure = canLoseTreasure;
		if (canLoseTreasure)
		{
			treasureCatchLevel = .6f;
			increaseRate /= 3;
			decreaseRate = 0.0025f;
			treasureProgressColor = Color.Aquamarine;
			isSpecial = true;
		}
		this.spriteId = spriteId;
		this.goldenTreasure = goldenTreasure;
		this.realTreasure = realTreasure;
		treasureAppearTimer = Game1.random.Next(treasureAppearMin, treasureAppearMax);
	}

	public virtual bool treasureUpdate(GameTime time, float bobberBarPos, int bobberBarHeight)
	{
		if ((lostTreasure || treasureCaught) && treasureScale <= 0.01f && playingCatchEffect <= 0.01f)
		{
			return false;
		}
		
		bool treasureInBar = false;
		float oldTreasureAppearTimer = treasureAppearTimer;
		treasureAppearTimer -= time.ElapsedGameTime.Milliseconds;
		if (treasureAppearTimer <= 0f)
		{
			if (treasureScale < treasureScaleMaxScale && !treasureCaught)
			{
				if (oldTreasureAppearTimer > 0f)
				{
					if (initTreasurePosition == 0)
					{
						if (bobberBarPos > 274f)
						{
							treasurePosition = Game1.random.Next(8, (int)bobberBarPos - 20);
						}
						else
						{
							int min = Math.Min(528, (int)bobberBarPos + bobberBarHeight);
							int max = 500;
							treasurePosition = ((min > max) ? (max - 1) : Game1.random.Next(min, max));
						}
					}
					else
					{
						treasurePosition = initTreasurePosition;
					}

					Game1.playSound("dwop");
				}

				treasureScale = Math.Min(treasureScaleMaxScale, treasureScale + 0.1f);

				if (waitForScaling && treasureScale < treasureScaleMaxScale)
				{
					return false;
				}
			}

			treasureInBar = treasurePosition + 12f <= bobberBarPos - 32f + bobberBarHeight &&
			                treasurePosition - 16f >= bobberBarPos - 32f;
			if (treasureInBar && !treasureCaught)
			{
				treasureCatchLevel += increaseRate;
				treasureShake = new Vector2(Game1.random.Next(-2, 3) * treasureShakeMultiplier, Game1.random.Next(-2, 3) * treasureShakeMultiplier);
				if (treasureCatchLevel >= 1f)
				{
					Game1.playSound("newArtifact");
					treasureCaught = true;
					playingCatchEffect = 2f;
				}
			}
			else if (treasureCaught)
			{
				treasureScale = Math.Max(0f, treasureScale - 0.1f);
				if (treasureScale <= 0.01f)
				{
					playingCatchEffect = Math.Max(0f, playingCatchEffect - 0.1f);
					treasureShake = new Vector2(Game1.random.Next(-2, 3) * treasureShakeMultiplier, Game1.random.Next(-2, 3) * treasureShakeMultiplier);
				}
			}
			else
			{
				treasureShake = Vector2.Zero;
				treasureCatchLevel = Math.Max(0f, treasureCatchLevel - decreaseRate);
				if (canLoseTreasure)
				{
					if (treasureCatchLevel == 0f)
					{
						lostTreasure = true;
						treasureScale = 0f;
						Game1.playSound("fishEscape");
					}
				}
			}
		}
		
		return treasureInBar;
	}

	public void drawTreasure(SpriteBatch b, Vector2 everythingShake, int xPositionOnScreen, int yPositionOnScreen)
    {
	    // Treasures
	    if (goldenTreasure)
	    {
		    b.Draw(Game1.mouseCursors_1_6, // golden treasure
			    new Vector2(xPositionOnScreen + 64 + 18, (yPositionOnScreen + 12 + 24) + treasurePosition) +
			    treasureShake + everythingShake, new Rectangle(256, 51, 20, 24), Color.White, 0f, 
			    new Vector2(10f, 10f),2f * treasureScale, SpriteEffects.None, 0.85f);
	    }
	    else if (spriteId == TreasureSprites.NormalChest)
	    {
		    b.Draw(Game1.mouseCursors, // normal treasure
			    new Vector2(xPositionOnScreen + 64 + 18, (yPositionOnScreen + 12 + 24) + treasurePosition) +
			    treasureShake + everythingShake, new Rectangle(638, 1865, 20, 24), Color.White, 0f,
			    new Vector2(10f, 10f), 2f * treasureScale, SpriteEffects.None, 0.85f);
	    }
	    else if (spriteId == TreasureSprites.Fish)
	    {
		    b.Draw(Game1.mouseCursors, // normal fish
			    new Vector2(xPositionOnScreen + 64 + 18, (yPositionOnScreen + 12 + 24) + treasurePosition) +
			    treasureShake + everythingShake, new Rectangle(614, 1840, 20, 20), Color.White, 0f,
			    new Vector2(10f, 10f), 2f * treasureScale, SpriteEffects.None, 0.88f);
	    }
	    else if(spriteId >= TreasureSprites.WhiteCow)
	    {
		    b.Draw(DeluxeFishingRodTool.fishingTextures,
			    new Vector2(xPositionOnScreen + 64 + 14, yPositionOnScreen + 12 + 24 + treasurePosition) +
			    treasureShake + everythingShake, new Rectangle(32 * (spriteId - 500), 32, 32, 32), Color.White, 0f,
			    new Vector2(10f, 10f), 1.6f * treasureScale, SpriteEffects.None, 0.85f);
	    }
	    else
	    {
		    // draw treasure
		    b.Draw(DeluxeFishingRodTool.fishingTextures,
			    new Vector2(xPositionOnScreen + 64 + 18, yPositionOnScreen + 12 + 24 + treasurePosition) +
			    treasureShake + everythingShake, new Rectangle(20 * spriteId, 0, 20, 24), Color.White, 0f,
			    new Vector2(10f, 10f), 2f * treasureScale, SpriteEffects.None, 0.85f);
	    }
	    
	    if (showProgressBar && treasureAppearTimer <= 0f && treasureCatchLevel > 0f && !treasureCaught) // Treasure catch progress
	    {
		    if (isSpecial)
		    {
			    // Outline progress bar for specials
			    b.Draw(Game1.staminaRect,
				    new Rectangle(xPositionOnScreen + 63, yPositionOnScreen + 11 + (int)treasurePosition, 42, 10),
				    Color.Black);
		    }
		    b.Draw(Game1.staminaRect,
			    new Rectangle(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)treasurePosition, 40, 8),
			    Color.DimGray * 0.5f);
		    
		    if (reverseProgress)
		    {
			    b.Draw(Game1.staminaRect,
				    new Rectangle(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)treasurePosition,
					    (int)((1f - treasureCatchLevel) * 40f), 8), treasureProgressColor);
		    }
		    else
		    {
			    b.Draw(Game1.staminaRect,
				    new Rectangle(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)treasurePosition,
					    (int)(treasureCatchLevel * 40f), 8), treasureProgressColor);
		    }
	    }

	    if (catchEffect != -1 && playingCatchEffect > 0)
	    {
		    // draw temporary sprite after catching
		    b.Draw(DeluxeFishingRodTool.fishingTextures,
			    new Vector2(xPositionOnScreen + 64 + 18, yPositionOnScreen + 12 + 24 + treasurePosition) +
			    treasureShake + everythingShake, new Rectangle(20 * catchEffect, 0, 20, 24), Color.White, 0f,
			    new Vector2(10f, 10f), 2f, SpriteEffects.None, 0.85f);
	    }
    }
}
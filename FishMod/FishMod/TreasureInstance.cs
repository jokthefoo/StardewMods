using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FishMod;

public class TreasureInstance
{
	private float treasurePosition;
	private float treasureCatchLevel;
	private float treasureAppearTimer;
	private float treasureScale;
	public bool treasureCaught;
	public bool goldenTreasure;
	private Vector2 treasureShake;
	private int spriteId;
	
	
	public float initTreasurePosition = 0;
	
	public const int minPos = 8;
	public const int maxPos = 500;

	public TreasureInstance(int spriteId, int treasureAppearMin = 1000, int treasureAppearMax = 3000, bool goldenTreasure = false)
	{
		this.spriteId = spriteId;
		this.goldenTreasure = goldenTreasure;
		treasureAppearTimer = Game1.random.Next(treasureAppearMin, treasureAppearMax);
	}

	public bool treasureUpdate(GameTime time, float bobberBarPos, int bobberBarHeight)
	{
		bool treasureInBar = false;
		float oldTreasureAppearTimer = treasureAppearTimer;
		treasureAppearTimer -= time.ElapsedGameTime.Milliseconds;
		if (treasureAppearTimer <= 0f)
		{
			if (treasureScale < 1f && !treasureCaught)
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

				treasureScale = Math.Min(1f, treasureScale + 0.1f);
			}

			treasureInBar = treasurePosition + 12f <= bobberBarPos - 32f + bobberBarHeight &&
			                treasurePosition - 16f >= bobberBarPos - 32f;
			if (treasureInBar && !treasureCaught)
			{
				treasureCatchLevel += 0.0135f;
				treasureShake = new Vector2(Game1.random.Next(-2, 3), Game1.random.Next(-2, 3));
				if (treasureCatchLevel >= 1f)
				{
					Game1.playSound("newArtifact");
					treasureCaught = true;
				}
			}
			else if (treasureCaught)
			{
				treasureScale = Math.Max(0f, treasureScale - 0.1f);
			}
			else
			{
				treasureShake = Vector2.Zero;
				treasureCatchLevel = Math.Max(0f, treasureCatchLevel - 0.01f);
			}
		}
		
		return treasureInBar;
	}

	public void drawTreasure(SpriteBatch b, Vector2 everythingShake, int xPositionOnScreen, int yPositionOnScreen)
    {
	    // Treasures
	    if (goldenTreasure)
	    {
		    b.Draw(Game1.mouseCursors_1_6,
			    new Vector2(xPositionOnScreen + 64 + 18,
				    (float)(yPositionOnScreen + 12 + 24) + treasurePosition) + treasureShake + everythingShake,
			    new Rectangle(256, 51, 20, 24), Color.White, 0f, new Vector2(10f, 10f), 2f * treasureScale,
			    SpriteEffects.None, 0.85f);
	    }
	    else if(spriteId == -1)
	    {
		    b.Draw(Game1.mouseCursors,
			    new Vector2(xPositionOnScreen + 64 + 18,
				    (float)(yPositionOnScreen + 12 + 24) + treasurePosition) + treasureShake + everythingShake,
			    new Rectangle(638, 1865, 20, 24), Color.White, 0f, new Vector2(10f, 10f), 2f * treasureScale,
			    SpriteEffects.None, 0.85f);
	    }
	    else
	    {
		    // draw treasure
		    b.Draw(ObjectIds.fishingTextures,
			    new Vector2(xPositionOnScreen + 64 + 18,
				    (float)(yPositionOnScreen + 12 + 24) + treasurePosition) + treasureShake + everythingShake,
			    new Rectangle(20*spriteId, 0, 20, 24), Color.White, 0f, new Vector2(10f, 10f), 2f * treasureScale,
			    SpriteEffects.None, 0.85f);
	    }
				
				
	    if (treasureCatchLevel > 0f && !treasureCaught) // Treasure catch progress
	    {
		    b.Draw(Game1.staminaRect,
			    new Rectangle(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)treasurePosition, 40, 8),
			    Color.DimGray * 0.5f);
		    b.Draw(Game1.staminaRect,
			    new Rectangle(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)treasurePosition,
				    (int)(treasureCatchLevel * 40f), 8), Color.Orange);
	    }
    }
}
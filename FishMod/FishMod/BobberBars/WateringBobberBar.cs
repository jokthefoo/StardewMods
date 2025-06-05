using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Extensions;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;


namespace FishMod
{
	public class WateringBobberBar : IClickableMenu
	{
		public static Texture2D farmingBobberBarTextures;
		
		public bool handledFishResult;

		public float uiScale;

		public float everythingShakeTimer;

		public bool treasureInBar;
		
		public bool buttonPressed;

		public bool flipBubble;

		public bool fadeIn;

		public bool fadeOut;
		
		public int bobberBarHeight;
		
		public Vector2 barShake;

		public Vector2 everythingShake;

		public float reelRotation;

		private SparklingText sparkleText;

		public float bobberBarPos;

		public float bobberBarSpeed;

		public float distancesFromCatchingTop = 0.01f;
		public float distancesFromCatchingBot = 0.01f;
		public int progressBarShakeTop;
		public int progressBarShakeBot;
		
		public static ICue reelSound;

		public static ICue unReelSound;
		
		public List<TreasureInstance> treasures = new ();

		public Tool tool;
		
		public GameLocation location;

		public WateringBobberBar(GameLocation location, Tool tool, bool treasure) : base(0, 0, 96, 636)
		{
			reelRotation = 359;
			this.tool = tool;
			this.location = location;
			
			var t1 = new MovingTreasure(TreasureSprites.Parsnip, false, 20, 20);
			t1.SetMovementBounds(MovingTreasure.defaultMinBound, MovingTreasure.defaultMaxBound / 2f);
			t1.showProgressBar = false;
			t1.increaseRate = 0;
			t1.treasureShakeMultiplier = .5f;
			t1.difficulty = 35;
			t1.treasureScale = 1.2f;
			
			var t2 = new MovingTreasure(TreasureSprites.WaterDrop, false, 20, 20);
			t2.SetMovementBounds(MovingTreasure.defaultMaxBound / 2f, MovingTreasure.defaultMaxBound);
			t2.showProgressBar = false;
			t2.increaseRate = 0;
			t2.treasureShakeMultiplier = .5f;
			t2.difficulty = 35;
			t2.treasureScale = 1.2f;
			treasures.Add(t1);
			treasures.Add(t2);

			if (treasure)
			{
				var t = new MovingTreasure(TreasureSprites.Fairy, true, 500, 500, canLoseTreasure: true);
				t.difficulty = tool.lastUser.FarmingLevel * 4 + 30;
				treasures.Add(t);
			}
			
			handledFishResult = false;
			fadeIn = true;
			uiScale = 0f;
			
			bobberBarHeight = 96 + Game1.player.FarmingLevel * 8;
			
			toolChoppingAngle = MathHelper.ToRadians(220);

			Reposition();

			bobberBarPos = 568 - bobberBarHeight;

			Game1.setRichPresence("fishing", Game1.currentLocation.Name);
		}

		#region prolly not gonna change these ui and inputstuffs
		public virtual void Reposition()
		{
			switch (Game1.player.FacingDirection)
			{
				case 1:
					xPositionOnScreen = (int)Game1.player.Position.X - 64 - 132;
					yPositionOnScreen = (int)Game1.player.Position.Y - 274;
					break;
				case 3:
					xPositionOnScreen = (int)Game1.player.Position.X + 128;
					yPositionOnScreen = (int)Game1.player.Position.Y - 274;
					flipBubble = true;
					break;
				case 0:
					xPositionOnScreen = (int)Game1.player.Position.X - 64 - 132;
					yPositionOnScreen = (int)Game1.player.Position.Y - 274;
					break;
				case 2:
					xPositionOnScreen = (int)Game1.player.Position.X - 64 - 132;
					yPositionOnScreen = (int)Game1.player.Position.Y - 274;
					break;
			}

			xPositionOnScreen -= Game1.viewport.X;
			yPositionOnScreen -= Game1.viewport.Y + 64;
			if (xPositionOnScreen + 96 > Game1.viewport.Width)
			{
				xPositionOnScreen = Game1.viewport.Width - 96;
			}
			else if (xPositionOnScreen < 0)
			{
				xPositionOnScreen = 0;
			}

			if (yPositionOnScreen < 0)
			{
				yPositionOnScreen = 0;
			}
			else if (yPositionOnScreen + 636 > Game1.viewport.Height)
			{
				yPositionOnScreen = Game1.viewport.Height - 636;
			}
		}

		public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
		{
			base.gameWindowSizeChanged(oldBounds, newBounds);
			Reposition();
		}

		public override void receiveLeftClick(int x, int y, bool playSound = true)
		{
		}

		public override void receiveRightClick(int x, int y, bool playSound = true)
		{
		}

		public override void performHoverAction(int x, int y)
		{
		}
		
		public override bool readyToClose()
		{
			return false;
		}

		public override void emergencyShutDown()
		{
			base.emergencyShutDown();
			unReelSound?.Stop(AudioStopOptions.Immediate);
			reelSound?.Stop(AudioStopOptions.Immediate);
			if (!handledFishResult)
			{
				Game1.playSound("fishEscape");
			}

			fadeOut = true;
			everythingShakeTimer = 500f;
			
			distancesFromCatchingTop = -1f;
			distancesFromCatchingBot = -1f;
		}

		public override void receiveKeyPress(Keys key)
		{
			if (Game1.options.menuButton.Contains(new InputButton(key)))
			{
				emergencyShutDown();
			}
		}
#endregion

public override void update(GameTime time)
{
	Reposition();
	if (sparkleText != null && sparkleText.update(time))
	{
		sparkleText = null;
	}

	if (everythingShakeTimer > 0f)
	{
		everythingShakeTimer -= time.ElapsedGameTime.Milliseconds;
		everythingShake = new Vector2(Game1.random.Next(-10, 11) / 10f,
			Game1.random.Next(-10, 11) / 10f);
		if (everythingShakeTimer <= 0f)
		{
			everythingShake = Vector2.Zero;
		}
	}

	if (fadeIn)
	{
		uiScale += 0.05f;
		if (uiScale >= 1f)
		{
			uiScale = 1f;
			fadeIn = false;
		}

		return;
	}

	if (fadeOut)
	{
		if (everythingShakeTimer > 0f || sparkleText != null)
		{
			return;
		}

		uiScale -= 0.05f;
		if (uiScale <= 0f)
		{
			uiScale = 0f;
			fadeOut = false;
			
			if (distancesFromCatchingTop > 0.9f && distancesFromCatchingBot > 0.9f)
			{
				bool treasureCaught = false;
				foreach (var t in treasures)
				{
					if (t.realTreasure && t.treasureCaught)
					{
						treasureCaught = true;
					}
				}
				WateringCanFishing.WateringRewards(location, tool, treasureCaught);
			}
			else
			{
				Game1.player.completelyStopAnimatingOrDoingAction();
			}

			Game1.exitActiveMenu();
			Game1.setRichPresence("location", Game1.currentLocation.Name);
		}

		return;
	}
	fishUpdate(time);
	SwingingAnimationUpdate(time);
}

public void fishUpdate(GameTime time)
{
	bool wasPressing = buttonPressed;
	buttonPressed = Game1.oldMouseState.LeftButton == ButtonState.Pressed ||
	                Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton) ||
	                (Game1.options.gamepadControls && (Game1.oldPadState.IsButtonDown(Buttons.X) ||
	                                                   Game1.oldPadState.IsButtonDown(Buttons.A)));
	if (!wasPressing && buttonPressed)
	{
		Game1.playSound("fishingRodBend");
	}

	float gravity = buttonPressed ? -0.25f : 0.25f;
	if (buttonPressed && (bobberBarPos == 0f || bobberBarPos == 568 - bobberBarHeight))
	{
		bobberBarSpeed = 0f;
	}

	float oldPos = bobberBarPos;
	bobberBarSpeed += gravity;
	bobberBarPos += bobberBarSpeed;
	if (bobberBarPos + bobberBarHeight > 568f)
	{
		bobberBarPos = 568 - bobberBarHeight;
		bobberBarSpeed = -bobberBarSpeed * 2f / 3f;
		if (oldPos + bobberBarHeight < 568f)
		{
			Game1.playSound("shiny4");
		}
	}
	else if (bobberBarPos < 0f)
	{
		bobberBarPos = 0f;
		bobberBarSpeed = -bobberBarSpeed * 2f / 3f;
		if (oldPos > 0f)
		{
			Game1.playSound("shiny4");
		}
	}

	treasureInBar = false;
	int index = 0;
	foreach (var t in treasures)
	{
		if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
		{
			treasureInBar = true;

			switch (index)
			{
				case 0:
					distancesFromCatchingTop += 0.0035f;
					if (distancesFromCatchingTop >= 1f && t.treasureCaught == false)
					{
						progressBarShakeTop = Game1.random.Next(-1, 1);
						t.treasureCatchLevel = 1f;
					}
					break;
				case 1:
					distancesFromCatchingBot += 0.0035f;
					if (distancesFromCatchingBot >= 1f && t.treasureCaught == false)
					{
						progressBarShakeBot = Game1.random.Next(-1, 1);
						t.treasureCatchLevel = 1f;
					}
					break;
			}
		}
		index++;
	}

	reelRotation += (float)Math.PI / 180f;
	if (reelRotation > 2*Math.PI)
	{
		reelRotation = 270f * (float)Math.PI / 180f;
	}
	
	if (treasureInBar)
	{
		barShake = Vector2.Zero;
		//Rumble.rumble(0.1f, 1000f);
		unReelSound?.Stop(AudioStopOptions.Immediate);
		if (reelSound == null || reelSound.IsStopped || reelSound.IsStopping || !reelSound.IsPlaying)
		{
			Game1.playSound("slosh", out reelSound);
		}
	}
	else
	{
		barShake.X = Game1.random.Next(-10, 11) / 10f;
		barShake.Y = Game1.random.Next(-10, 11) / 10f;
		progressBarShakeTop = 0;
		progressBarShakeBot = 0;
		
		reelSound?.Stop(AudioStopOptions.Immediate);
		if (unReelSound == null || unReelSound.IsStopped)
		{
			//Game1.playSound("leafrustle", 600, out unReelSound);
		}
	}
	
	distancesFromCatchingTop = Math.Max(0f, Math.Min(1f, distancesFromCatchingTop));
	distancesFromCatchingBot = Math.Max(0f, Math.Min(1f, distancesFromCatchingBot));
	
	if (distancesFromCatchingTop <= 0f || distancesFromCatchingBot <= 0f)
	{
		fadeOut = true;
		everythingShakeTimer = 500f;
		Game1.playSound("fishEscape");
		handledFishResult = true;
		unReelSound?.Stop(AudioStopOptions.Immediate);
		reelSound?.Stop(AudioStopOptions.Immediate);
		return;
	}
	
	if (distancesFromCatchingTop >= 1f && distancesFromCatchingBot >= 1f)
	{
		fadeOut = true;
		everythingShakeTimer = 500f;
		Game1.playSound("jingle1");
		handledFishResult = true;
		unReelSound?.Stop(AudioStopOptions.Immediate);
		reelSound?.Stop(AudioStopOptions.Immediate);
	}
}

//Debris
private List<Debris> debris = new List<Debris>();
private float debrisAlpha = 1.0f;
public float toolChoppingAngle;
private int toolAnimState = ToolAnimStates.Chop;
public static ICue chopSound;
private float toolXPosOffset = 0;

private static class ToolAnimStates
{
	public const int Chop = 0;
	public const int PullBack = 1;
	public const int Sow = 2;
}

private void SwingingAnimationUpdate(GameTime time)
{
	debrisAlpha -= 0.02f;
	
	foreach (Debris d in debris)
	{
		d.x += (int)d.velocity.X;
		d.y += (int)d.velocity.Y;
		d.y += 10;
		//d.velocity.X -= .1f;
		d.velocity.X = Math.Clamp(d.velocity.X, -4, 10);
		d.velocity.Y += .05f;
		d.velocity.Y = Math.Clamp(d.velocity.Y, -12, 0);
	}
	
	switch (toolAnimState)
	{
		case ToolAnimStates.Chop:
			toolChoppingAngle += MathHelper.ToRadians(2);
			break;
		case ToolAnimStates.PullBack:
			toolXPosOffset += .3f;
			toolXPosOffset = Math.Clamp(toolXPosOffset, -10, 0);
			toolChoppingAngle -= MathHelper.ToRadians(1.5f);
			break;
		case ToolAnimStates.Sow:
			toolXPosOffset -= .5f;
			break;
	}
	
	if (toolChoppingAngle < MathHelper.ToRadians(280) && toolAnimState == ToolAnimStates.PullBack)
	{
		//start chop
		toolAnimState = ToolAnimStates.Chop;
	}
	
	if (toolChoppingAngle > MathHelper.ToRadians(380) && toolAnimState == ToolAnimStates.Chop)
	{
		Game1.playSound("hoeHit", out chopSound);

		//start sowing
		toolAnimState = ToolAnimStates.Sow;
		debrisAlpha = 1.0f;
		ConstructDebris();
	}
	
	if (toolXPosOffset < -10 && toolAnimState == ToolAnimStates.Sow)
	{
		//start backswing
		toolAnimState = ToolAnimStates.PullBack;
	}
}

private void ConstructDebris()
{
	debris = new List<Debris>();
	for (int i = 0; i < 4; i++)
	{
		debris.Add(new Debris(Game1.random.Next(0,11), Game1.random.Next(-10,2), Game1.random.Next(-8,9), new Vector2(Game1.random.Next(-1, 1), Game1.random.Next(-20, -10))));
	}
}

public override void draw(SpriteBatch b)
		{
			// Draw order matters
			Game1.StartWorldDrawInUI(b);
			
			// bar white background transparent
			b.Draw(Game1.mouseCursors,
				new Vector2(xPositionOnScreen - (flipBubble ? 44 : 20) + 104, yPositionOnScreen - 16 + 314) +
				everythingShake, new Rectangle(652, 1685, 52, 157), Color.White * 0.6f * uiScale, 0f,
				new Vector2(26f, 78.5f) * uiScale, 4f * uiScale,
				flipBubble ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.001f);
			
			int barXPos = xPositionOnScreen + 20;
			// Pau's mining background
			b.Draw(farmingBobberBarTextures, new Vector2(barXPos, yPositionOnScreen + 300 - 32) + everythingShake,
				new Rectangle(13, 5, 58, 158), Color.White * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 4f * uiScale,
				SpriteEffects.None, 0.01f);
			
			// Gameplay bar
			if (uiScale == 1f)
			{
				// These 3 are bobber bar
				int colorIndex = -4;
				barXPos += 38;
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(barXPos, yPositionOnScreen + 12 + (int)bobberBarPos) + barShake +
					everythingShake, new Rectangle(216, 447 + 10 * colorIndex, 9, 2),
					treasureInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(barXPos, yPositionOnScreen + 12 + (int)bobberBarPos + 8) + barShake +
					everythingShake, new Rectangle(216, 453 + 10 * colorIndex, 9, 1),
					treasureInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, new Vector2(4f, bobberBarHeight - 16), SpriteEffects.None,
					0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(barXPos,
						yPositionOnScreen + 12 + (int)bobberBarPos + bobberBarHeight - 8) + barShake + everythingShake,
					new Rectangle(216, 454 + 10 * colorIndex, 9, 2),
					treasureInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				
				// current level of bottom Success bar
				b.Draw(Game1.staminaRect,
					new Rectangle(xPositionOnScreen + 121 + progressBarShakeBot,
						yPositionOnScreen + 0 + 290 + (int)(290f * (1f - distancesFromCatchingBot)) + progressBarShakeBot, 8,
						(int)(290f * distancesFromCatchingBot)), Color.Lerp(Color.Blue, Color.Aqua, distancesFromCatchingBot));
				// current level of top Success bar
				b.Draw(Game1.staminaRect,
					new Rectangle(xPositionOnScreen + 121 + progressBarShakeTop,
						yPositionOnScreen + 12, 8 + progressBarShakeTop,
						(int)(278f * distancesFromCatchingTop)), Color.Lerp(Color.DarkGreen, Color.LawnGreen, distancesFromCatchingTop));
				
				//Hoe
				b.Draw(farmingBobberBarTextures,
					new Vector2(xPositionOnScreen - 30 + toolXPosOffset, yPositionOnScreen + 520) + everythingShake,
					new Rectangle(79, 50, 15, 13), Color.White, toolChoppingAngle, new Vector2(4f, 10f), 4f,
					SpriteEffects.None, 0.9f);

				if (debrisAlpha > 0f)
				{
					// Debris
					foreach(Debris d in debris)
					{
						b.Draw(farmingBobberBarTextures,
							new Vector2(xPositionOnScreen + 20 + d.x, yPositionOnScreen + 507 + d.y) + everythingShake,
							new Rectangle(100 + 3 * d.index, 32, 3, 3), Color.White * debrisAlpha, toolChoppingAngle, new Vector2(0f, 0f), 4f,
							SpriteEffects.None, 0.9f);
					}
				}
					
				//draw treasures
				foreach (var t in treasures)
				{
					t.drawTreasure(b, everythingShake, xPositionOnScreen - 7, yPositionOnScreen);
				}
				
				sparkleText?.draw(b, new Vector2(xPositionOnScreen - 16, yPositionOnScreen - 64)); // "perfect text"
			}
			
			Game1.EndWorldDrawInUI(b);
		}
	}
}

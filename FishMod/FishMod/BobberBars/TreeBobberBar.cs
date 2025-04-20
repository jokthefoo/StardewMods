using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;

namespace FishMod
{
	public class TreeBobberBar : IClickableMenu
	{
		public static Texture2D treeBobberBarTextures;
		
		public bool handledFishResult;

		public float uiScale;

		public float everythingShakeTimer;

		public int logCount;
		
		public bool treasureInBar;
		
		public bool buttonPressed;

		public bool flipBubble;

		public bool fadeIn;

		public bool fadeOut;
		
		public int bobberBarHeight;
		
		public Vector2 barShake;

		public Vector2 everythingShake;

		public float axeChoppingAngle;

		private SparklingText sparkleText;

		public float bobberBarPos;

		public float bobberBarSpeed;

		public float distanceFromCatching = 0.01f;

		public static ICue reelSound;

		public static ICue unReelSound;
		
		public static ICue chopSound;
		
		public List<TreasureInstance> treasures = new ();
		
		public GameLocation location;
		
		public Tool tool;
		
		public Vector2 tileLocation;

		public Tree tree;
		public bool isTree;
		
		public int treeAnimTimer = 800;
		public int farmerAnimTimer = 100;
		public int farmerAnimIndex = 0;
		
		private class Debris
		{
			public Debris(int i, int x, int y, Vector2 vel)
			{
				index = i;
				this.x = x;
				this.y = y;
				velocity = vel;
			}
	
			public int index;
			public int x;
			public int y;
			public Vector2 velocity;
		}
		
		private List<Debris> debris = new List<Debris>();

		public TreeBobberBar(GameLocation location, bool treasure, int logCount, Tool t, Vector2 tileLocation)
			: base(0, 0, 96, 636)
		{
			tool = t;
			this.logCount = logCount;
			this.location = location;
			this.tileLocation = tileLocation;
			GetTree();
			
			axeChoppingAngle = MathHelper.ToRadians(265);
			
			for (int i = 0; i < logCount; i++)
			{
				treasures.Add(new TreasureInstance(TreasureSprites.Wood_Pau, false, 20,20));
			}

			if (treasure)
			{
				var realTreasure = new MovingTreasure(TreasureSprites.GreenChest, true, 1000, 1200, canLoseTreasure: true);
				realTreasure.difficulty = t.lastUser.ForagingLevel * 5 + 25;
				treasures.Add(realTreasure);
			}
			
			ConstructDebris();
			
			handledFishResult = false;
			fadeIn = true;
			uiScale = 0f;
			
			bobberBarHeight = 96 + Game1.player.ForagingLevel * 8;

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
			distanceFromCatching = -1f;
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
		everythingShake = new Vector2((float)Game1.random.Next(-10, 11) / 10f,
			(float)Game1.random.Next(-10, 11) / 10f);
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
			if (distanceFromCatching > 0.9f)
			{
				bool treasureCaught = false;
				foreach (var t in treasures)
				{
					if (t.realTreasure && t.treasureCaught)
					{
						treasureCaught = true;
					}
				}
				AxeFishing.TreeRewards(location, tool, tileLocation, logCount, treasureCaught);
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

	
	treeAnimTimer -= time.ElapsedGameTime.Milliseconds;
	if (treeAnimTimer <= 0)
	{
		if (isTree)
		{
			tree.shake(tileLocation, false);
			treeAnimTimer = 1000;
		}
	}
	
	/*
	switch (Game1.player.FacingDirection)
	{
		case 0:
			Game1.player.FarmerSprite.setCurrentFrame(160 + farmerAnimIndex);
			break;
		case 1:
			Game1.player.FarmerSprite.setCurrentFrame(168 + farmerAnimIndex);
			break;
		case 2:
			Game1.player.FarmerSprite.setCurrentFrame(176 + farmerAnimIndex);
			break;
		case 3:
			Game1.player.FarmerSprite.setCurrentFrame(184 + farmerAnimIndex);
			break;
	}

	farmerAnimTimer -= time.ElapsedGameTime.Milliseconds;
	if (farmerAnimTimer <= 0)
	{
		farmerAnimIndex++;
		if (farmerAnimIndex == 8)
		{
			farmerAnimIndex = 0;
		}
		
		farmerAnimTimer = 100;
	}*/

	fishUpdate(time);
}

public void GetTree()
{
	if (location.terrainFeatures.TryGetValue(tileLocation, out TerrainFeature terrainFeature))
	{
		if (terrainFeature is Tree t )
		{
			tree = t;
			isTree = true;
		}
	}
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
	foreach (var t in treasures)
	{
		bool beforeCaught = t.treasureCaught;
		if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
		{
			treasureInBar = true;
		}

		if (t.treasureCaught && !beforeCaught && !t.realTreasure)
		{
			distanceFromCatching += 1f / logCount;
		}
	}

	AxeAnimationUpdate(time);
	
	if (treasureInBar)
	{
		barShake = Vector2.Zero;
		//Rumble.rumble(0.1f, 1000f);
		unReelSound?.Stop(AudioStopOptions.Immediate);
		if (reelSound == null || reelSound.IsStopped || reelSound.IsStopping || !reelSound.IsPlaying)
		{
			Game1.playSound("axchop", out reelSound);
		}
	}
	else
	{
		barShake.X = Game1.random.Next(-10, 11) / 10f;
		barShake.Y = Game1.random.Next(-10, 11) / 10f;
		
		reelSound?.Stop(AudioStopOptions.Immediate);
		if (unReelSound == null || unReelSound.IsStopped)
		{
			//Game1.playSound("leafrustle", 600, out unReelSound);
		}
	}
	
	distanceFromCatching = Math.Max(0f, Math.Min(1f, distanceFromCatching));
	
	if (distanceFromCatching <= 0f)
	{
		fadeOut = true;
		everythingShakeTimer = 500f;
		Game1.playSound("fishEscape");
		handledFishResult = true;
		unReelSound?.Stop(AudioStopOptions.Immediate);
		reelSound?.Stop(AudioStopOptions.Immediate);
		return;
	}
	
	if (distanceFromCatching >= 1f)
	{
		fadeOut = true;
		everythingShakeTimer = 500f;
		Game1.playSound("jingle1");
		handledFishResult = true;
		unReelSound?.Stop(AudioStopOptions.Immediate);
		reelSound?.Stop(AudioStopOptions.Immediate);
	}
}

private static class AxeAnimStates
{
	public const int Chop = 0;
	public const int PullBack = 1;
}

private int axeAnimstate = AxeAnimStates.Chop;
private float debrisAlpha = 1.0f;
private void AxeAnimationUpdate(GameTime time)
{
	debrisAlpha -= 0.03f;
	
	foreach (Debris d in debris)
	{
		d.x += (int)d.velocity.X;
		d.y += (int)d.velocity.Y;
		d.y += 4;
		d.velocity.X -= .2f;
		d.velocity.X = Math.Clamp(d.velocity.X, 0, 100);
		d.velocity.Y -= .5f;
		d.velocity.Y = Math.Clamp(d.velocity.Y, 0, 100);
	}
	
	switch (axeAnimstate)
	{
		case AxeAnimStates.Chop:
			axeChoppingAngle += MathHelper.ToRadians(4);
			break;
		case AxeAnimStates.PullBack:
			axeChoppingAngle -= MathHelper.ToRadians(1.5f);
			break;
	}
	
	if (axeChoppingAngle < MathHelper.ToRadians(260) && axeAnimstate != AxeAnimStates.Chop)
	{
		//start chop
		axeAnimstate = AxeAnimStates.Chop;
	}
	
	if (axeChoppingAngle > MathHelper.ToRadians(380) && axeAnimstate != AxeAnimStates.PullBack)
	{
		if (chopSound == null || chopSound.IsStopped || chopSound.IsStopping || !chopSound.IsPlaying)
		{
			Game1.playSound("axchop", out chopSound);
		}

		//start backswing
		axeAnimstate = AxeAnimStates.PullBack;
		debrisAlpha = 1.0f;
		ConstructDebris();
	}
}

private void ConstructDebris()
{
	debris = new List<Debris>();
	for (int i = 0; i < 6; i++)
	{
		debris.Add(new Debris(Game1.random.Next(0,6), Game1.random.Next(-8,9), Game1.random.Next(-8,9), new Vector2(Game1.random.Next(-2, 3) * 5, Game1.random.Next(-2, 3) * 5)));
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
			
			
			// Pau's tree background
			b.Draw(treeBobberBarTextures, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 300 - 18) + everythingShake,
				new Rectangle(6, 8, 53, 156), Color.White * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 4f * uiScale,
				SpriteEffects.None, 0.01f);
			
			/*
			// bar background tree attempt
			b.Draw(DeluxeFishingRodTool.fishingTextures, new Vector2(xPositionOnScreen - 36, yPositionOnScreen + 300) + everythingShake,
				new Rectangle(0, 368, 72, 144), Color.White * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 6f * uiScale,
				SpriteEffects.None, 0.01f);
			
			// bar background
			b.Draw(DeluxeFishingRodTool.fishingTextures, new Vector2(xPositionOnScreen + 126, yPositionOnScreen + 296) + everythingShake,
				new Rectangle(83, 362, 22, 148), Color.White * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 4f * uiScale,
				SpriteEffects.None, 0.01f);*/
			
			// Gameplay bar
			if (uiScale == 1f)
			{
				// These 3 are bobber bar
				int colorIndex = -2;
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos) + barShake +
					everythingShake, new Rectangle(216, 447 + 10 * colorIndex, 9, 2),
					treasureInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos + 8) + barShake +
					everythingShake, new Rectangle(216, 453 + 10 * colorIndex, 9, 1),
					treasureInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, new Vector2(4f, bobberBarHeight - 16), SpriteEffects.None,
					0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64,
						yPositionOnScreen + 12 + (int)bobberBarPos + bobberBarHeight - 8) + barShake + everythingShake,
					new Rectangle(216, 454 + 10 * colorIndex, 9, 2),
					treasureInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				
				// current level of Success bar
				b.Draw(Game1.staminaRect,
					new Rectangle(xPositionOnScreen + 128,
						yPositionOnScreen + 14 + (int)(570f * (1f - distanceFromCatching)), 8,
						(int)(570f * distanceFromCatching)), Utility.getRedToGreenLerpColor(distanceFromCatching));
				
				// AXE
				b.Draw(treeBobberBarTextures,
					new Vector2(xPositionOnScreen - 18, yPositionOnScreen + 510) + everythingShake,
					new Rectangle(64, 81, 16, 15), Color.White, axeChoppingAngle, new Vector2(5f, 10f), 4f,
					SpriteEffects.None, 0.9f);

				if (debrisAlpha > 0f)
				{
					// Debris
					foreach(Debris d in debris)
					{
						b.Draw(treeBobberBarTextures,
							new Vector2(xPositionOnScreen + 18 + d.x, yPositionOnScreen + 505 + d.y) + everythingShake,
							new Rectangle(80 + 4 * d.index, 56, 4, 3), Color.White * debrisAlpha, axeChoppingAngle, new Vector2(0f, 0f), 4f,
							SpriteEffects.None, 0.9f);
					}
				}
				
				//draw treasures
				foreach (var t in treasures)
				{
					t.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);
				}
				
				sparkleText?.draw(b, new Vector2(xPositionOnScreen - 16, yPositionOnScreen - 64)); // "perfect text"
			}
			
			Game1.EndWorldDrawInUI(b);
		}
	}
}

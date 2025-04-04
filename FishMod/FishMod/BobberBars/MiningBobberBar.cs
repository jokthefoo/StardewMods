using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
using Vector2 = Microsoft.Xna.Framework.Vector2;


namespace FishMod
{
	public class MiningBobberBar : IClickableMenu
	{
		public bool handledFishResult;

		public float uiScale;

		public float everythingShakeTimer;

		public bool treasureInBar;

		private bool slimeInBar;
		private bool rockInBar;
		
		public bool buttonPressed;

		public bool flipBubble;

		public bool fadeIn;

		public bool fadeOut;
		
		public int bobberBarHeight;
		
		public Vector2 barShake;

		public Vector2 everythingShake;

		public float bobberBarPos;

		public float bobberBarSpeed;

		public float distanceFromCatchingRock = 0.01f;
		public float distanceFromCatchingSlime = 1f;
		
		public static ICue reelSound;

		public static ICue unReelSound;
		
		public List<TreasureInstance> treasures = new ();

		public Tool tool;
		public GameLocation location;
		public Vector2 tileLocation;

		private TreasureInstance Rock;
		private TreasureInstance TreasureNode;
		private MovingTreasure Slime;
		
		public MiningBobberBar(GameLocation location, Tool tool, Vector2 tileLocation) : base(0, 0, 96, 636)
		{
			this.tool = tool;
			this.location = location;
			this.tileLocation = tileLocation;
			
			Rock = new TreasureInstance(TreasureSprites.Rock, false, 20, 20);
			Rock.decreaseRate = 0;
			SpawnSlime(1000, 3000);

			TreasureNode = new TreasureInstance(Game1.random.NextBool() ? TreasureSprites.MineralNode : TreasureSprites.OmniGeode, true, 500, 2000);
			TreasureNode.decreaseRate = 0;
			treasures.Add(TreasureNode);
			
			handledFishResult = false;
			fadeIn = true;
			uiScale = 0f;
			
			bobberBarHeight = 96 + Game1.player.MiningLevel * 8;

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
			distanceFromCatchingRock = -1f;
			distanceFromCatchingSlime = -1f;
		}

		public override void receiveKeyPress(Keys key)
		{
			if (Game1.options.menuButton.Contains(new InputButton(key)))
			{
				emergencyShutDown();
			}
		}
#endregion

private void SpawnSlime(int minSpawn = 3000, int maxSpawn = 8000)
{
	Slime = new MovingTreasure(TreasureSprites.Slime, false, minSpawn, maxSpawn);
	Slime.treasureScaleMaxScale = 1.4f;
	
	Slime.increaseRate /= 2.5f;
	Slime.decreaseRate = 0;
	Slime.isSpecial = true;
	Slime.difficulty = tool.lastUser.MiningLevel * 4 + Game1.random.Next(20, 35);
	Slime.reverseProgress = true;
	distanceFromCatchingSlime = 1f;
}

public override void update(GameTime time)
{
	Reposition();

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
		if (everythingShakeTimer > 0f)
		{
			return;
		}

		uiScale -= 0.05f;
		if (uiScale <= 0f)
		{
			uiScale = 0f;
			fadeOut = false;
			
			if (distanceFromCatchingRock > 0.9f)
			{
				int slimeCount = 0;
				int rockCount = 0;
				int mineralCount = 0;
				int omniCount = 0;
				
				foreach (var t in treasures)
				{
					if (t.treasureCaught)
					{
						switch (t.spriteId)
						{
							case TreasureSprites.Slime:
								slimeCount++;
								break;
							case TreasureSprites.Rock:
								rockCount++;
								break;
							case TreasureSprites.MineralNode:
								mineralCount++;
								break;
							case TreasureSprites.OmniGeode:
								omniCount++;
								break;
						}
					}
				}
				MiningFishing.MiningRewards(location, tool, tileLocation, slimeCount, rockCount, mineralCount, omniCount);
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

	Slime.treasureProgressColor = Color.Lerp(Color.LimeGreen, Color.Red, Slime.treasureCatchLevel);
	slimeInBar = Slime.treasureUpdate(time, bobberBarPos, bobberBarHeight);
	rockInBar = Rock.treasureUpdate(time, bobberBarPos, bobberBarHeight);

	treasureInBar = false;
	foreach (var t in treasures)
	{
		if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
		{
			treasureInBar = true;
		}
	}

	string cueName = "";

	if (slimeInBar)
	{
		cueName = "daggerswipe";
		//distanceFromCatchingSlime += 0.002f;
		if (Slime.treasureCaught)
		{
			treasures.Add(Slime);
			if (treasures.Count < 30) // prevent infinity
			{
				SpawnSlime();
			}
		}
	}
	else if(Slime.treasureScale >= Slime.treasureScaleMaxScale)
	{
		distanceFromCatchingSlime -= 0.0033f;
		if (distanceFromCatchingSlime <= 0f)
		{
			tool.lastUser.health -= 5;
			tool.lastUser.currentLocation.debris.Add(new Debris(5, new Vector2(tool.lastUser.StandingPixel.X + 8, tool.lastUser.StandingPixel.Y), Color.Red, 1f, tool.lastUser));
			tool.lastUser.playNearbySoundAll("ow");
			
			distanceFromCatchingSlime = 1f;
		}
	}
	
	if (rockInBar)
	{
		cueName = "hammer";
		
		distanceFromCatchingRock += (float)Game1.random.NextDouble() / 1000f + 0.001f;
		if (Rock.treasureCaught)
		{
			treasures.Add(Rock);
			if (treasures.Count < 30) // prevent infinity
			{
				Rock = new TreasureInstance(8, false, 20, 20);
				Rock.decreaseRate = 0;

				if (TreasureNode.treasureCaught && Game1.random.NextDouble() < 0.15f)
				{
					TreasureNode = new TreasureInstance(Game1.random.NextBool() ? TreasureSprites.MineralNode : TreasureSprites.OmniGeode, true, 500, 2000);
					TreasureNode.decreaseRate = 0;
					TreasureNode.treasureProgressColor = Color.Lavender;
					treasures.Add(TreasureNode);
				}
			}
		}
	}
	
	if (!treasureInBar && !rockInBar && !slimeInBar)
	{
		barShake.X = Game1.random.Next(-10, 11) / 10f;
		barShake.Y = Game1.random.Next(-10, 11) / 10f;
		
		reelSound?.Stop(AudioStopOptions.Immediate);
		if (unReelSound == null || unReelSound.IsStopped)
		{
			//Game1.playSound("leafrustle", 600, out unReelSound);
		}
	}
	else
	{
		barShake = Vector2.Zero;
		//Rumble.rumble(0.1f, 1000f);
		unReelSound?.Stop(AudioStopOptions.Immediate);
		if (reelSound == null || reelSound.IsStopped || reelSound.IsStopping || !reelSound.IsPlaying)
		{
			if (cueName != "")
			{
				Game1.playSound(cueName, out reelSound);
			}
		}
	}
	
	distanceFromCatchingRock = Math.Max(0f, Math.Min(1f, distanceFromCatchingRock));
	distanceFromCatchingSlime = Math.Max(0f, Math.Min(1f, distanceFromCatchingSlime));
	
	if (distanceFromCatchingRock <= 0f)
	{
		fadeOut = true;
		everythingShakeTimer = 500f;
		Game1.playSound("fishEscape");
		handledFishResult = true;
		unReelSound?.Stop(AudioStopOptions.Immediate);
		reelSound?.Stop(AudioStopOptions.Immediate);
		return;
	}
	
	if (distanceFromCatchingRock >= 1f)
	{
		fadeOut = true;
		everythingShakeTimer = 500f;
		Game1.playSound("jingle1");
		handledFishResult = true;
		unReelSound?.Stop(AudioStopOptions.Immediate);
		reelSound?.Stop(AudioStopOptions.Immediate);
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
			
			b.Draw(DeluxeFishingRodTool.fishingTextures, new Vector2(xPositionOnScreen - 0, yPositionOnScreen + 0) + everythingShake,
				new Rectangle(0, 176, 42, 186), Color.White * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 4f * uiScale,
				SpriteEffects.None, 0.01f);
			b.Draw(DeluxeFishingRodTool.fishingTextures, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 200) + everythingShake,
				new Rectangle(0, 176, 42, 186), Color.Black * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 4f * uiScale,
				SpriteEffects.None, 0.01f);
			b.Draw(DeluxeFishingRodTool.fishingTextures, new Vector2(xPositionOnScreen - 50, yPositionOnScreen + 400) + everythingShake,
				new Rectangle(0, 176, 42, 186), Color.Red * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 4f * uiScale,
				SpriteEffects.None, 0.01f);

			int barXPos = xPositionOnScreen + 100;
			// bar background
			b.Draw(DeluxeFishingRodTool.fishingTextures, new Vector2(barXPos, yPositionOnScreen + 296) + everythingShake,
				new Rectangle(141, 362, 29, 148), Color.White * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 4f * uiScale,
				SpriteEffects.None, 0.01f);
			
			// Gameplay bar
			if (uiScale == 1f)
			{
				// These 3 are bobber bar
				int colorIndex = 4;
				int bobberXPos = barXPos - 62;
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(bobberXPos, yPositionOnScreen + 12 + (int)bobberBarPos) + barShake +
					everythingShake, new Rectangle(216, 447 + 10 * colorIndex, 9, 2),
					treasureInBar || slimeInBar || rockInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(bobberXPos, yPositionOnScreen + 12 + (int)bobberBarPos + 8) + barShake +
					everythingShake, new Rectangle(216, 453 + 10 * colorIndex, 9, 1),
					treasureInBar || slimeInBar || rockInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, new Vector2(4f, bobberBarHeight - 16), SpriteEffects.None,
					0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(bobberXPos,
						yPositionOnScreen + 12 + (int)bobberBarPos + bobberBarHeight - 8) + barShake + everythingShake,
					new Rectangle(216, 454 + 10 * colorIndex, 9, 2),
					treasureInBar || slimeInBar || rockInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				
				// current level of Success bar
				b.Draw(Game1.staminaRect,
					new Rectangle(barXPos - 10,
						yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatchingSlime)), 16,
						(int)(580f * distanceFromCatchingSlime)), Color.Lerp(Color.Red, Color.LimeGreen, distanceFromCatchingSlime));
				
				// current level of Success bar 2
				b.Draw(Game1.staminaRect,
					new Rectangle(barXPos + 18,
						yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatchingRock)), 16,
						(int)(580f * distanceFromCatchingRock)), Color.Lerp(Color.Gray, Color.White, distanceFromCatchingRock));
				
				/*
				// TODO add mining anim for bar?
				b.Draw(Game1.mouseCursors,
					new Vector2(xPositionOnScreen - 18, yPositionOnScreen + 514) + everythingShake,
					new Rectangle(32, 657, 16, 15), Color.White, reelRotation, new Vector2(2f, 10f), 4f,
					SpriteEffects.None, 0.9f);*/

				int xTreasureOffset = xPositionOnScreen - 26;
				//draw treasures
				foreach (var t in treasures)
				{
					t.drawTreasure(b, everythingShake, xTreasureOffset, yPositionOnScreen);
				}
				
				Rock.drawTreasure(b, everythingShake, xTreasureOffset, yPositionOnScreen);
				Slime.drawTreasure(b, everythingShake, xTreasureOffset, yPositionOnScreen);
			}
			
			Game1.EndWorldDrawInUI(b);
		}
	}
}

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
using StardewValley.Menus;
using StardewValley.Tools;

namespace FishMod
{
	public class FishFrenzyBobberBar : IClickableMenu
	{
		public bool handledFishResult;

		public float difficulty;

		public string whichFish;

		/// <summary>The mail flag to set for the current player when the current <see cref="F:StardewValley.Menus.BobberBar.whichFish" /> is successfully caught.</summary>
		public string setFlagOnCatch;
		
		public float scale;

		public float everythingShakeTimer;

		public bool bobberInBar;

		public bool buttonPressed;

		public bool flipBubble;

		public bool fadeIn;

		public bool fadeOut;

		public int bobberBarHeight;

		public int fishSize;

		public int fishQuality;

		public int minFishSize;

		public int maxFishSize;

		public int fishSizeReductionTimer;

		public List<string> bobbers;

		public Vector2 barShake;

		public Vector2 fishShake;

		public Vector2 everythingShake;

		public float reelRotation;

		public float bobberBarPos;

		public float bobberBarSpeed;

		public float distanceFromCatching = 1f;

		public static ICue reelSound;

		public static ICue unReelSound;

		private Item fishObject;
		
		public List<TreasureInstance> treasures = new ();
		public int colorIndex;
		public Color color = Color.White;
		private MovingTreasureFishStyle.MotionType motionType = MovingTreasureFishStyle.MotionType.Mixed;

		public FishFrenzyBobberBar(string whichFish, float fishSize, List<string> bobbers, string setFlagOnCatch,
			string baitID = "")
			: base(0, 0, 96, 636)
		{
			fishObject = ItemRegistry.Create(whichFish);
			this.bobbers = bobbers;
			this.setFlagOnCatch = setFlagOnCatch;
			handledFishResult = false;
			colorIndex = -1;
			
			fadeIn = true;
			scale = 0f;
			this.whichFish = whichFish;
			Dictionary<string, string> dictionary = DataLoader.Fish(Game1.content);
			
			bobberBarHeight = 96 + Game1.player.FishingLevel * 8;

			if (dictionary.TryGetValue(whichFish, out var rawData))
			{
				string[] fields = rawData.Split('/');
				difficulty = Convert.ToInt32(fields[1]);
				switch (fields[2].ToLower())
				{
					case "mixed":
						motionType = MovingTreasureFishStyle.MotionType.Mixed;
						break;
					case "dart":
						motionType = MovingTreasureFishStyle.MotionType.Dart;
						break;
					case "smooth":
						motionType = MovingTreasureFishStyle.MotionType.Smooth;
						break;
					case "floater":
						motionType = MovingTreasureFishStyle.MotionType.Floater;
						break;
					case "sinker":
						motionType = MovingTreasureFishStyle.MotionType.Sinker;
						break;
				}
			
				for (int i = 0; i < 15; i++)
				{
					if (i % 5 == 0)
					{
						treasures.Add(new MovingTreasureFishStyle((int)difficulty + Game1.random.Next((int)(-difficulty / 2),(int)( difficulty / 2 + 1)), MovingTreasureFishStyle.MotionType.Dart));
					}
					else
					{
						treasures.Add(new MovingTreasureFishStyle((int)difficulty + Game1.random.Next((int)(-difficulty / 2),(int)( difficulty / 2 + 1)), motionType));
					}
				}

				minFishSize = Convert.ToInt32(fields[3]);
				maxFishSize = Convert.ToInt32(fields[4]);
				this.fishSize = (int)(minFishSize + (maxFishSize - minFishSize) * fishSize);
				this.fishSize++;
				fishQuality = !(fishSize < 0.33) ? fishSize < 0.66 ? 1 : 2 : 0;
				fishSizeReductionTimer = 800;
				for (int i = 0; i < Utility.getStringCountInList(bobbers, "(O)877"); i++) // quality bobber
				{
					fishQuality++;
					if (fishQuality > 2)
					{
						fishQuality = 4;
					}
				}
			}

			Reposition();
			bobberBarHeight += Utility.getStringCountInList(bobbers, "(O)695") * 24;  // cork bobber
			if (baitID == "(O)DeluxeBait")
			{
				bobberBarHeight += 12;
			}

			bobberBarPos = 568 - bobberBarHeight;
			Game1.setRichPresence("fishing", Game1.currentLocation.Name);
		}

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

		public override void update(GameTime time)
		{
			Reposition();

			if (everythingShakeTimer > 0f)
			{
				everythingShakeTimer -= time.ElapsedGameTime.Milliseconds;
				everythingShake = new Vector2(Game1.random.Next(-10, 11) / 10f,
					(float)Game1.random.Next(-10, 11) / 10f);
				if (everythingShakeTimer <= 0f)
				{
					everythingShake = Vector2.Zero;
				}
			}

			if (fadeIn)
			{
				scale += 0.05f;
				if (scale >= 1f)
				{
					scale = 1f;
					fadeIn = false;
				}
			}
			else if (fadeOut)
			{
				if (everythingShakeTimer > 0f)
				{
					return;
				}

				scale -= 0.05f;
				if (scale <= 0f)
				{
					scale = 0f;
					fadeOut = false;
					FishingRod rod = Game1.player.CurrentTool as FishingRod;
					int numCaught = 1;

					if (distanceFromCatching > 0.9f && rod != null)
					{
						foreach (var t in treasures)
						{
							if (t.treasureCaught)
							{
								numCaught++;
							}
						}
						
						rod.pullFishFromWater(whichFish, fishSize, fishQuality, (int)difficulty, false,
							false, false, setFlagOnCatch, false, numCaught);
					}
					else
					{
						Game1.player.completelyStopAnimatingOrDoingAction();
						rod?.doneFishing(Game1.player, consumeBaitAndTackle: false);
					}

					Game1.exitActiveMenu();
					Game1.setRichPresence("location", Game1.currentLocation.Name);
				}
			}
			else
			{
				bool num = buttonPressed;
				buttonPressed = Game1.oldMouseState.LeftButton == ButtonState.Pressed ||
				                Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton) ||
				                (Game1.options.gamepadControls && (Game1.oldPadState.IsButtonDown(Buttons.X) ||
				                                                   Game1.oldPadState.IsButtonDown(Buttons.A)));
				if (!num && buttonPressed)
				{
					Game1.playSound("fishingRodBend");
				}

				float gravity = (buttonPressed ? (-0.25f) : 0.25f);
				if (buttonPressed && gravity < 0f &&
				    (bobberBarPos == 0f || bobberBarPos == 568 - bobberBarHeight))
				{
					bobberBarSpeed = 0f;
				}

				float oldPos = bobberBarPos;
				bobberBarSpeed += gravity;
				bobberBarPos += bobberBarSpeed;
				if (bobberBarPos + bobberBarHeight > 568f)
				{
					bobberBarPos = 568 - bobberBarHeight;
					bobberBarSpeed = (0f - bobberBarSpeed) * 2f / 3f * (bobbers.Contains("(O)692") // lead bobber prevents bounce
						? Utility.getStringCountInList(bobbers, "(O)692") * 0.1f
						: 1f);
					if (oldPos + bobberBarHeight < 568f)
					{
						Game1.playSound("shiny4");
					}
				}
				else if (bobberBarPos < 0f)
				{
					bobberBarPos = 0f;
					bobberBarSpeed = (0f - bobberBarSpeed) * 2f / 3f;
					if (oldPos > 0f)
					{
						Game1.playSound("shiny4");
					}
				}

				bobberInBar = false;
				int treasuresToAdd = 0;
				foreach (var t in treasures)
				{
					bool beforeCaught = t.treasureCaught;
					if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
					{
						bobberInBar = true;
					}

					if (t.treasureCaught && !beforeCaught)
					{
						treasuresToAdd++;
					}
				}
				
				for (int i = 0; i < treasuresToAdd; i++)
				{
					treasures.Add(new MovingTreasureFishStyle((int)difficulty + Game1.random.Next(1, 11), motionType));
				}

				if (bobberInBar)
				{
					reelRotation += (float)Math.PI / 8f;
					fishShake.X = Game1.random.Next(-10, 11) / 10f;
					fishShake.Y = Game1.random.Next(-10, 11) / 10f;
					barShake = Vector2.Zero;
					Rumble.rumble(0.1f, 1000f);
					unReelSound?.Stop(AudioStopOptions.Immediate);
					if (reelSound == null || reelSound.IsStopped || reelSound.IsStopping || !reelSound.IsPlaying)
					{
						Game1.playSound("fastReel", out reelSound);
					}
				}
				else
				{
					if (!fishShake.Equals(Vector2.Zero))
					{
						Game1.playSound("tinyWhip");
						Rumble.stopRumbling();
					}

					fishSizeReductionTimer -= time.ElapsedGameTime.Milliseconds;
					if (fishSizeReductionTimer <= 0)
					{
						fishSize = Math.Max(minFishSize, fishSize - 1);
						fishSizeReductionTimer = 800;
					}

					reelRotation -= (float)Math.PI / Math.Max(10f, 100f);
					barShake.X = Game1.random.Next(-10, 11) / 10f;
					barShake.Y = Game1.random.Next(-10, 11) / 10f;
					fishShake = Vector2.Zero;
					reelSound?.Stop(AudioStopOptions.Immediate);
					if (unReelSound == null || unReelSound.IsStopped)
					{
						Game1.playSound("slowReel", 600, out unReelSound);
					}
				}

				if (Game1.player.CurrentTool != null)
				{
					Game1.player.CurrentTool.tickUpdate(time, Game1.player);
				}
				
				distanceFromCatching -= 0.002f;

				if (distanceFromCatching <= 0f)
				{
					distanceFromCatching = 1f;
					fadeOut = true;
					everythingShakeTimer = 500f;
					Game1.playSound("jingle1");
					handledFishResult = true;
					unReelSound?.Stop(AudioStopOptions.Immediate);
					reelSound?.Stop(AudioStopOptions.Immediate);
					if (fishSize == maxFishSize)
					{
						fishSize--;
					}
				}
			}
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

		public override void draw(SpriteBatch b)
		{
			Game1.StartWorldDrawInUI(b);
			// bar white background transparent
			b.Draw(Game1.mouseCursors,
				new Vector2(xPositionOnScreen - (flipBubble ? 44 : 20) + 104, yPositionOnScreen - 16 + 314) +
				everythingShake, new Rectangle(652, 1685, 52, 157), Color.White * 0.6f * scale, 0f,
				new Vector2(26f, 78.5f) * scale, 4f * scale,
				flipBubble ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.001f);
			// bar background
			b.Draw(Game1.mouseCursors, new Vector2(xPositionOnScreen + 70, yPositionOnScreen + 296) + everythingShake,
				new Rectangle(644, 1999, 38, 150), Color.White * scale, 0f, new Vector2(18.5f, 74f) * scale, 4f * scale,
				SpriteEffects.None, 0.01f);
			if (scale == 1f)
			{
				// These 3 are bobber bar
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos) + barShake +
					everythingShake, new Rectangle(216, 447 + 10 * colorIndex, 9, 2),
					bobberInBar
						? color
						: color * 0.25f *
						  ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							  2) + 2f), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos + 8) + barShake +
					everythingShake, new Rectangle(216, 453 + 10 * colorIndex, 9, 1),
					bobberInBar
						? color
						: color * 0.25f *
						  ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							  2) + 2f), 0f, Vector2.Zero, new Vector2(4f, bobberBarHeight - 16), SpriteEffects.None,
					0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64,
						yPositionOnScreen + 12 + (int)bobberBarPos + bobberBarHeight - 8) + barShake + everythingShake,
					new Rectangle(216, 454 + 10 * colorIndex, 9, 2),
					bobberInBar
						? color
						: color * 0.25f *
						  ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							  2) + 2f), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);

				// current level of Success bar
				b.Draw(Game1.staminaRect,
					new Rectangle(xPositionOnScreen + 124,
						yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatching)), 16,
						(int)(580f * distanceFromCatching)), Utility.getRedToGreenLerpColor(distanceFromCatching));

				// reel rotation (basically useless)
				b.Draw(Game1.mouseCursors,
					new Vector2(xPositionOnScreen + 18, yPositionOnScreen + 514) + everythingShake,
					new Rectangle(257, 1990, 5, 10), Color.White, reelRotation, new Vector2(2f, 10f), 4f,
					SpriteEffects.None, 0.9f);

				//draw treasures
				foreach (var t in treasures) t.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);

				if (bobbers.Contains("(O)SonarBobber"))
				{
					var xPosition = xPositionOnScreen > Game1.viewport.Width * 0.75f
						? xPositionOnScreen - 80
						: xPositionOnScreen + 216;
					var flip = xPosition < xPositionOnScreen;
					b.Draw(Game1.mouseCursors_1_6,
						new Vector2(xPosition - 12, yPositionOnScreen + 40) + everythingShake,
						new Rectangle(227, 6, 29, 24), Color.White, 0f, new Vector2(10f, 10f), 4f,
						flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.88f);
					fishObject.drawInMenu(b,
						new Vector2(xPosition, yPositionOnScreen) + new Vector2(flip ? -8 : -4, 4f) * 4f +
						everythingShake, 1f);
				}
			}
			
			Game1.EndWorldDrawInUI(b);
		}
	}
}

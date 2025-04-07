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
	public class BossBobberBar : IClickableMenu
	{
		public bool handledFishResult;

		public float difficulty;

		public int motionType;

		public string whichFish;

		/// <summary>A modifier that only affects the "damage" for not having the fish in the bobber bar.</summary>
		public float distanceFromCatchPenaltyModifier = 1f;

		/// <summary>The mail flag to set for the current player when the current <see cref="F:StardewValley.Menus.BobberBar.whichFish" /> is successfully caught.</summary>
		public string setFlagOnCatch;

		public float bobberPosition = 548f;

		public float bobberSpeed;

		public float bobberAcceleration;

		public float bobberTargetPosition;

		public float scale;

		public float everythingShakeTimer;

		public float floaterSinkerAcceleration;

		public bool bobberInBar;

		public bool buttonPressed;

		public bool flipBubble;

		public bool fadeIn;

		public bool fadeOut;

		public bool perfect;

		public bool bossFish;

		public bool fromFishPond;

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

		private SparklingText sparkleText;

		public float bobberBarPos;

		public float bobberBarSpeed;

		public float distanceFromCatching1 = 0.3f;
		public float distanceFromCatching2 = 0.01f;
		public float distanceFromCatching3 = 0.01f;
		
		public float currentDistanceFromCatching = 0.3f;
		private int currentStage = 0;
		private bool inbetweenStages = false;
		private int startingDiff = 0;

		public static ICue reelSound;

		public static ICue unReelSound;

		private Item fishObject;
		
		public List<TreasureInstance> treasures = new ();
		public bool treasureInBar;
		public int colorIndex;
		public Color color = Color.White;

		public BossBobberBar(string whichFish, float fishSize, int treasure, List<string> bobbers, string setFlagOnCatch,
			bool isBossFish = false, string baitID = "", bool goldenTreasure = false, int colorIndex = -1)
			: base(0, 0, 96, 636)
		{
			fishObject = ItemRegistry.Create(whichFish);
			this.bobbers = bobbers;
			this.setFlagOnCatch = setFlagOnCatch;
			handledFishResult = false;
			this.colorIndex = colorIndex;
			
			DeluxeFishingRodTool.randomTreasureNumbers.Clear();
			for (int i = 0; i < treasure; i++)
			{
				DeluxeFishingRodTool.randomTreasureNumbers.Add(Game1.random.Next(-1, 3));
				treasures.Add(new TreasureInstance(DeluxeFishingRodTool.randomTreasureNumbers[i], true,20,20));
			}

			if (goldenTreasure)
			{
				treasures[0].goldenTreasure = true;
			}
			
			fadeIn = true;
			scale = 0f;
			this.whichFish = whichFish;
			Dictionary<string, string> dictionary = DataLoader.Fish(Game1.content);
			
			bobberBarHeight = 96 + Game1.player.FishingLevel * 8;

			bossFish = isBossFish;

			if (dictionary.TryGetValue(whichFish, out var rawData))
			{
				string[] fields = rawData.Split('/');
				startingDiff = Convert.ToInt32(fields[1]);
				switch (fields[2].ToLower())
				{
					case "mixed":
						motionType = 0;
						break;
					case "dart":
						motionType = 1;
						break;
					case "smooth":
						motionType = 2;
						break;
					case "floater":
						motionType = 4;
						break;
					case "sinker":
						motionType = 3;
						break;
				}

				motionType = 1; // Set first stage to be a dart
				difficulty = 80; // Set first stage to be difficulty 80

				minFishSize = Convert.ToInt32(fields[3]);
				maxFishSize = Convert.ToInt32(fields[4]);
				this.fishSize = (int)(minFishSize + (maxFishSize - minFishSize) * fishSize);
				this.fishSize++;
				perfect = true;
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

				if (Game1.player.stats.Get("blessingOfWaters") != 0)
				{
					difficulty *= 0.75f;
					distanceFromCatchPenaltyModifier = 0.5f;
					Game1.player.stats.Decrement("blessingOfWaters");
					if (Game1.player.stats.Get("blessingOfWaters") == 0)
					{
						Game1.player.buffs.Remove("statue_of_blessings_3");
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
			bobberPosition = 508f;
			bobberTargetPosition = (100f - difficulty) / 100f * 548f;

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
			if (sparkleText != null && sparkleText.update(time))
			{
				sparkleText = null;
			}

			if (everythingShakeTimer > 0f)
			{
				everythingShakeTimer -= time.ElapsedGameTime.Milliseconds;
				everythingShake = new Vector2(Game1.random.Next(-10, 11) / 10f, Game1.random.Next(-10, 11) / 10f);
				if (everythingShakeTimer <= 0f)
				{
					everythingShake = Vector2.Zero;
					if (inbetweenStages)
					{
						inbetweenStages = false;
						
						currentStage += 1;

						if (currentStage == 1)
						{
							motionType = 2; //smooth;
							difficulty = 70;
						}
						else if (currentStage == 2)
						{
							motionType = 0; //mixed;
							difficulty = startingDiff;
						}
					
						if (distanceFromCatchPenaltyModifier != 1)
						{
							difficulty *= .75f;
						}
						
						if (currentStage == 1)
						{
							treasures.Add(new TreasureInstance(-2, false,20,20));
						}
						
						currentDistanceFromCatching = 0.3f;
						switch (currentStage)
						{
							case 1:
								distanceFromCatching1 = 1f;
								distanceFromCatching2 = currentDistanceFromCatching;
								break;
							case 2:
								distanceFromCatching1 = 1f;
								distanceFromCatching2 = 1f;
								distanceFromCatching3 = currentDistanceFromCatching;
								break;
						}
					}
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
				if (everythingShakeTimer > 0f || sparkleText != null)
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

					if (distanceFromCatching1 > 0.9f && distanceFromCatching2 > 0.9f && distanceFromCatching3 > 0.9f && rod != null)
					{
						bool treasureCaught = false;
						foreach (var t in treasures)
						{
							if (t.treasureCaught)
							{
								treasureCaught = true;
							}
						}
						
						rod.pullFishFromWater(whichFish, fishSize, fishQuality, (int)difficulty, treasureCaught,
							perfect, fromFishPond, setFlagOnCatch, bossFish, numCaught);
					}
					else
					{
						Game1.player.completelyStopAnimatingOrDoingAction();
						rod?.doneFishing(Game1.player, consumeBaitAndTackle: true);
					}

					Game1.exitActiveMenu();
					Game1.setRichPresence("location", Game1.currentLocation.Name);
				}
			}
			else
			{
				if (Game1.random.NextDouble() < (difficulty * ((motionType != 2) ? 1 : 20) / 4000f) &&
				    (motionType != 2 || bobberTargetPosition == -1f))
				{
					float spaceBelow = 548f - bobberPosition;
					float spaceAbove = bobberPosition;
					float percent = Math.Min(99f, difficulty + Game1.random.Next(10, 45)) / 100f;
					bobberTargetPosition = bobberPosition + Game1.random.Next((int)Math.Min(0f - spaceAbove, spaceBelow), (int)spaceBelow) * percent;
				}

				switch (motionType)
				{
					case 4:
						floaterSinkerAcceleration = Math.Max(floaterSinkerAcceleration - 0.01f, -1.5f);
						break;
					case 3:
						floaterSinkerAcceleration = Math.Min(floaterSinkerAcceleration + 0.01f, 1.5f);
						break;
				}

				if (Math.Abs(bobberPosition - bobberTargetPosition) > 3f && bobberTargetPosition != -1f)
				{
					bobberAcceleration = (bobberTargetPosition - bobberPosition) /
					                     (Game1.random.Next(10, 30) + (100f - Math.Min(100f, difficulty)));
					bobberSpeed += (bobberAcceleration - bobberSpeed) / 5f;
				}
				else if (motionType != 2 && Game1.random.NextDouble() < difficulty / 2000f)
				{
					bobberTargetPosition = bobberPosition + (Game1.random.NextBool()
						? Game1.random.Next(-100, -51)
						: Game1.random.Next(50, 101));
				}
				else
				{
					bobberTargetPosition = -1f;
				}

				if (motionType == 1 && Game1.random.NextDouble() < difficulty / 1000f)
				{
					bobberTargetPosition = bobberPosition + (Game1.random.NextBool()
						? Game1.random.Next(-100 - (int)difficulty * 2, -51)
						: Game1.random.Next(50, 101 + (int)difficulty * 2));
				}

				bobberTargetPosition = Math.Max(-1f, Math.Min(bobberTargetPosition, 548f));
				bobberPosition += bobberSpeed + floaterSinkerAcceleration;
				if (bobberPosition > 532f)
				{
					bobberPosition = 532f;
				}
				else if (bobberPosition < 0f)
				{
					bobberPosition = 0f;
				}

				bobberInBar = bobberPosition + 12f <= bobberBarPos - 32f + bobberBarHeight &&
				              bobberPosition - 16f >= bobberBarPos - 32f;
				if (bobberPosition >= 548 - bobberBarHeight &&
				    bobberBarPos >= 568 - bobberBarHeight - 4)
				{
					bobberInBar = true;
				}

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

				if (bobberInBar)
				{
					gravity *= (bobbers.Contains("(O)691") ? 0.3f : 0.6f); // barbed hook bobber (garbage that messes you up)
					if (bobbers.Contains("(O)691"))
					{
						for (int i = 0; i < Utility.getStringCountInList(bobbers, "(O)691"); i++)
						{
							if (bobberPosition + 16f < bobberBarPos + bobberBarHeight / 2)
							{
								bobberBarSpeed -= i > 0 ? 0.05f : 0.2f;
							}
							else
							{
								bobberBarSpeed += i > 0 ? 0.05f : 0.2f;
							}

							if (i > 0)
							{
								gravity *= 0.9f;
							}
						}
					}
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

				treasureInBar = false;
				bool spawnNewTreasure = false;
				foreach (var t in treasures)
				{
					bool beforeCaught = t.treasureCaught;
					if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
					{
						treasureInBar = true;
					}

					if (currentStage == 1)
					{
						if (t.treasureCaught && !beforeCaught && !t.realTreasure)
						{
							currentDistanceFromCatching += 0.2f;

							if (currentDistanceFromCatching < 1f)
							{
								spawnNewTreasure = true;
							}
						}
					}
				}

				if (spawnNewTreasure)
				{
					treasures.Add(new TreasureInstance(-2, false,20,20));
				}

				bool treasureBobberSkip = treasureInBar && bobbers.Contains("(O)693") && bobbers.Contains("(O){{ModId}}.PirateTreasureHunter"); // treasure bobber stops bar

				float stage1TreasureReduction = 0;
				if (currentStage == 1 && treasureInBar)
				{
					stage1TreasureReduction = 0.0015f;
				}

				if (bobberInBar && !inbetweenStages)
				{
					if (currentStage != 1)
					{
						currentDistanceFromCatching += 0.002f;
					}
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
				else if (!treasureBobberSkip && !inbetweenStages)
				{
					if (!fishShake.Equals(Vector2.Zero))
					{
						Game1.playSound("tinyWhip");
						perfect = false;
						Rumble.stopRumbling();
					}

					fishSizeReductionTimer -= time.ElapsedGameTime.Milliseconds;
					if (fishSizeReductionTimer <= 0)
					{
						fishSize = Math.Max(minFishSize, fishSize - 1);
						fishSizeReductionTimer = 800;
					}

					if ((Game1.player.fishCaught != null && Game1.player.fishCaught.Length != 0) ||
					    Game1.currentMinigame != null)
					{
						if (bobbers.Contains("(O)694")) // Trap bobber makes it easier
						{
							float reduction = 0.003f;
							float amount = 0.001f;
							for (int i = 0; i < Utility.getStringCountInList(bobbers, "(O)694"); i++)
							{
								reduction -= amount;
								amount /= 2f;
							}

							reduction = Math.Max(0.001f, reduction);
							currentDistanceFromCatching -= reduction * distanceFromCatchPenaltyModifier;
						}
						else
						{
							currentDistanceFromCatching -= (.003f - stage1TreasureReduction) * distanceFromCatchPenaltyModifier;
						}
					}

					float distanceAway = Math.Abs(bobberPosition - (bobberBarPos + bobberBarHeight / 2));
					reelRotation -= (float)Math.PI / Math.Max(10f, 200f - distanceAway);
					barShake.X = Game1.random.Next(-10, 11) / 10f;
					barShake.Y = Game1.random.Next(-10, 11) / 10f;
					fishShake = Vector2.Zero;
					reelSound?.Stop(AudioStopOptions.Immediate);
					if (unReelSound == null || unReelSound.IsStopped)
					{
						Game1.playSound("slowReel", 600, out unReelSound);
					}
				}
				
				currentDistanceFromCatching = Math.Max(0f, Math.Min(1f, currentDistanceFromCatching));
				if (Game1.player.CurrentTool != null)
				{
					Game1.player.CurrentTool.tickUpdate(time, Game1.player);
				}

				if (!inbetweenStages)
				{
					if (currentDistanceFromCatching <= 0f)
					{
						fadeOut = true;
						everythingShakeTimer = 500f;
						Game1.playSound("fishEscape");
						handledFishResult = true;
						unReelSound?.Stop(AudioStopOptions.Immediate);
						reelSound?.Stop(AudioStopOptions.Immediate);
					}
					else if (currentDistanceFromCatching >= 1f && currentStage == 2)
					{
						everythingShakeTimer = 500f;
						Game1.playSound("jingle1");
						fadeOut = true;
						handledFishResult = true;
						unReelSound?.Stop(AudioStopOptions.Immediate);
						reelSound?.Stop(AudioStopOptions.Immediate);
						sparkleText = new SparklingText(Game1.dialogueFont, "Susebron Slumbers!", Color.Yellow, Color.White,
							rainbow: false, 0.1, 1500, 32, 200);
					} else if (currentDistanceFromCatching >= 1f)
					{
						inbetweenStages = true;
						everythingShakeTimer = 1500f;
						Game1.playSound("jingle1");

						string text = currentStage == 0 ? "Susebron Calls for Aid!" : "Susebron Angers!";
						sparkleText = new SparklingText(Game1.dialogueFont, text, Color.Yellow, Color.White,
							rainbow: false, 0.1, 1500, 16, 100);
					}
				}
			}

			switch (currentStage)
			{
				case 0:
					distanceFromCatching1 = currentDistanceFromCatching;
					break;
				case 1:
					distanceFromCatching2 = currentDistanceFromCatching;
					break;
				case 2:
					distanceFromCatching3 = currentDistanceFromCatching;
					break;
			}

			if (bobberPosition < 0f)
			{
				bobberPosition = 0f;
			}

			if (bobberPosition > 548f)
			{
				bobberPosition = 548f;
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
			distanceFromCatching1 = -1f;
			distanceFromCatching2 = -1f;
			distanceFromCatching3 = -1f;
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
			b.Draw(DeluxeFishingRodTool.fishingTextures, new Vector2(xPositionOnScreen + 74, yPositionOnScreen + 296) + everythingShake,
				new Rectangle(261, 359, 50, 150), Color.White * scale, 0f, new Vector2(18.5f, 74f) * scale, 4f * scale,
				SpriteEffects.None, 0.01f);
			
			if (scale == 1f)
			{
				// These 3 are bobber bar
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos) + barShake +
					everythingShake, new Rectangle(216, 447 + 10 * colorIndex, 9, 2),
					bobberInBar
						? color
						: (color * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos + 8) + barShake +
					everythingShake, new Rectangle(216, 453 + 10 * colorIndex, 9, 1),
					bobberInBar
						? color
						: (color * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, new Vector2(4f, bobberBarHeight - 16), SpriteEffects.None,
					0.89f);
				b.Draw(DeluxeFishingRodTool.fishingTextures,
					new Vector2(xPositionOnScreen + 64,
						yPositionOnScreen + 12 + (int)bobberBarPos + bobberBarHeight - 8) + barShake + everythingShake,
					new Rectangle(216, 454 + 10 * colorIndex, 9, 2),
					bobberInBar
						? color
						: (color * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				
				// current level of Success bar
				b.Draw(Game1.staminaRect,
					new Rectangle(xPositionOnScreen + 124,
						yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatching1)), 16,
						(int)(580f * distanceFromCatching1)), Utility.getRedToGreenLerpColor(distanceFromCatching1));
				b.Draw(Game1.staminaRect,
					new Rectangle(xPositionOnScreen + 124 + 28,
						yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatching2)), 16,
						(int)(580f * distanceFromCatching2)),
					Color.Lerp(new Color(0xff930f), new Color(0xfff95b), distanceFromCatching2));
				b.Draw(Game1.staminaRect,
					new Rectangle(xPositionOnScreen + 124 + 56,
						yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatching3)), 16,
						(int)(580f * distanceFromCatching3)),
					Color.Lerp(new Color(0xff0f7b), new Color(0xf89b29), distanceFromCatching3));
				
				// reel rotation
				b.Draw(Game1.mouseCursors,
					new Vector2(xPositionOnScreen + 18, yPositionOnScreen + 514) + everythingShake,
					new Rectangle(257, 1990, 5, 10), Color.White, reelRotation, new Vector2(2f, 10f), 4f,
					SpriteEffects.None, 0.9f);
				
				//draw treasures
				foreach (var t in treasures)
				{
					t.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);
				}

				// The Fish
				if (!inbetweenStages)
				{
					int spriteId = 11;
					b.Draw(DeluxeFishingRodTool.fishingTextures,
						new Vector2(xPositionOnScreen + 82, yPositionOnScreen + 36 + bobberPosition) +
						fishShake + everythingShake,  new Rectangle(20 * spriteId, 0, 20, 24), Color.White,
						0f, new Vector2(10f, 10f), 2f, SpriteEffects.None, 0.88f);
				}
				
				sparkleText?.draw(b, new Vector2(xPositionOnScreen, yPositionOnScreen + 200));
			}

			Game1.EndWorldDrawInUI(b);
		}
	}
}

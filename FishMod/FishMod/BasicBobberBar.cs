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
using StardewValley.Tools;

namespace FishMod
{
	public class BasicBobberBar : IClickableMenu
	{
		public bool handledFishResult;

		public float difficulty;

		public int motionType;

		public string whichFish;

		/// <summary>A modifier that only affects the "damage" for not having the fish in the bobber bar.</summary>
		public float distanceFromCatchPenaltyModifier = 1f;

		public float curFishPosition = 548f;

		public float fishSpeed;

		public float fishAcceleration;

		public float fishTargetPosition;

		public float uiScale;

		public float everythingShakeTimer;

		public float floaterSinkerAcceleration;

		

		public int treasureCount;
		
		
		
		public bool fishInBar;

		public bool buttonPressed;

		public bool flipBubble;

		public bool fadeIn;

		public bool fadeOut;

		public bool perfect;
		
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

		public float distanceFromCatching = 0.3f;

		public static ICue reelSound;

		public static ICue unReelSound;
		
		public List<TreasureInstance> treasures = new ();

		//private Item fishObject;

		public BasicBobberBar(string whichFish, float fishSize, int treasure, List<string> bobbers, string baitID = "")
			: base(0, 0, 96, 636)
		{
			//fishObject = ItemRegistry.Create(whichFish);
			this.bobbers = bobbers;
			this.whichFish = whichFish;
			
			treasureCount = treasure;
			for (int i = 0; i < treasureCount; i++)
			{
				treasures.Add(new TreasureInstance(i));
			}
			
			handledFishResult = false;
			fadeIn = true;
			uiScale = 0f;
			Dictionary<string, string> dictionary = DataLoader.Fish(Game1.content);
			
			bobberBarHeight = 96 + Game1.player.FishingLevel * 8;

			if (dictionary.TryGetValue(whichFish, out var rawData))
			{
				string[] fields = rawData.Split('/');
				difficulty = Convert.ToInt32(fields[1]);
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

				minFishSize = Convert.ToInt32(fields[3]);
				maxFishSize = Convert.ToInt32(fields[4]);
				this.fishSize = (int)(minFishSize + (maxFishSize - minFishSize) * fishSize);
				this.fishSize++;
				perfect = true;
				fishQuality = fishSize >= 0.33 ? fishSize >= 0.66 ? 2 : 1 : 0;
				fishSizeReductionTimer = 800;
			}

			Reposition();

			bobberBarPos = 568 - bobberBarHeight;
			curFishPosition = 508f;
			fishTargetPosition = (100f - difficulty) / 100f * 548f;

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
			FishingRod rod = Game1.player.CurrentTool as FishingRod;
			if (distanceFromCatching > 0.9f && rod != null)
			{
				bool treasureCaught = false;
				foreach (var t in treasures)
				{
					if (t.treasureCaught)
					{
						treasureCaught = true;
					}
				}

				// begin ending shit
				Farmer lastUser = Game1.player;

				rod.treasureCaught = treasureCaught;
				rod.fishSize = fishSize;
				rod.fishQuality = fishQuality;
				rod.whichFish = ItemRegistry.GetMetadata(whichFish);
				rod.fromFishPond = false;
				rod.setFlagOnCatch = null;
				rod.numberOfFishCaught = 1;
				Vector2 bobberTile = new Vector2(rod.bobber.X / 64f, rod.bobber.Y / 64f);
				if (fishQuality >= 2 && perfect)
					rod.fishQuality = 4;
				else if (fishQuality >= 1 && perfect)
					rod.fishQuality = 2;

				if (rod.fishQuality < 0)
					rod.fishQuality = 0;
				string textureName;
				Rectangle sourceRect;
				
				if (rod.whichFish.TypeIdentifier == "(O)")
				{
					ParsedItemData parsedOrErrorData = rod.whichFish.GetParsedOrErrorData();
					textureName = parsedOrErrorData.TextureName;
					sourceRect = parsedOrErrorData.GetSourceRect();
				}
				else
				{
					textureName = "LooseSprites\\Cursors";
					sourceRect = new Rectangle(228, 408, 16, 16);
				}

				float animationInterval;
				//if (lastUser.FacingDirection == 1 || lastUser.FacingDirection == 3)
				{
					float num5 = Vector2.Distance(rod.bobber.Value, lastUser.Position);
					float y1 = 1f / 1000f;
					float num6 = (float)(128.0 - ((double)lastUser.Position.Y - (double)rod.bobber.Y + 10.0));
					double a1 = 4.0 * Math.PI / 11.0;
					float f1 = (float)((double)num5 * (double)y1 * Math.Tan(a1) /
					                   Math.Sqrt(2.0 * (double)num5 * (double)y1 * Math.Tan(a1) -
					                             2.0 * (double)y1 * (double)num6));
					if (float.IsNaN(f1))
						f1 = 0.6f;
					float num7 = f1 * (float)(1.0 / Math.Tan(a1));
					animationInterval = num5 / num7;

					rod.animations.Add(new TemporaryAnimatedSprite(textureName, sourceRect, animationInterval, 1, 0,
						rod.bobber.Value, false, false, rod.bobber.Y / 10000f, 0.0f, Color.White, 4f, 0.0f, 0.0f, 0.0f)
					{
						motion = new Vector2((Game1.player.FacingDirection == 3 ? -1f : 1f) * -num7, -f1),
						acceleration = new Vector2(0.0f, y1),
						timeBasedMotion = true,
						endFunction =
							(TemporaryAnimatedSprite.endBehavior)(_ => rod.playerCaughtFishEndFunction(false)),
						endSound = "tinyWhip"
					});
					
					/* tool anim ranges
			  if (this.currentSingleAnimation >= 160 && this.currentSingleAnimation < 192 || this.currentSingleAnimation >= 232 && this.currentSingleAnimation < 264)
				return true;
			  return this.currentSingleAnimation >= 272 && this.currentSingleAnimation < 280;*/
					
					
					//rod.pullFishFromWater(whichFish, fishSize, fishQuality, (int)difficulty, treasureCaught, perfect, false, "", false, 1);
				}
				//end ending shit
			}
			else
			{
				Game1.player.completelyStopAnimatingOrDoingAction();
				rod?.doneFishing(Game1.player, consumeBaitAndTackle: true);
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
	if (Game1.random.NextDouble() < difficulty * (motionType != 2 ? 1 : 20) / 4000f &&
	    (motionType != 2 || fishTargetPosition == -1f))
	{
		var spaceBelow = 548f - curFishPosition;
		var spaceAbove = curFishPosition;
		var percent = Math.Min(99f, difficulty + Game1.random.Next(10, 45)) / 100f;
		fishTargetPosition = curFishPosition +
		                     Game1.random.Next((int)Math.Min(0f - spaceAbove, spaceBelow),
			                     (int)spaceBelow) * percent;
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

	if (Math.Abs(curFishPosition - fishTargetPosition) > 3f && fishTargetPosition != -1f)
	{
		fishAcceleration = (fishTargetPosition - curFishPosition) /
		                   ((float)Game1.random.Next(10, 30) + (100f - Math.Min(100f, difficulty)));
		fishSpeed += (fishAcceleration - fishSpeed) / 5f;
	}
	else if (motionType != 2 && Game1.random.NextDouble() < (double)(difficulty / 2000f))
	{
		fishTargetPosition = curFishPosition + (float)(Game1.random.NextBool()
			? Game1.random.Next(-100, -51)
			: Game1.random.Next(50, 101));
	}
	else
	{
		fishTargetPosition = -1f;
	}

	if (motionType == 1 && Game1.random.NextDouble() < (double)(difficulty / 1000f))
	{
		fishTargetPosition = curFishPosition + (float)(Game1.random.NextBool()
			? Game1.random.Next(-100 - (int)difficulty * 2, -51)
			: Game1.random.Next(50, 101 + (int)difficulty * 2));
	}

	fishTargetPosition = Math.Max(-1f, Math.Min(fishTargetPosition, 548f));
	curFishPosition += fishSpeed + floaterSinkerAcceleration;
	if (curFishPosition > 532f)
	{
		curFishPosition = 532f;
	}
	else if (curFishPosition < 0f)
	{
		curFishPosition = 0f;
	}

	fishInBar = curFishPosition + 12f <= bobberBarPos - 32f + bobberBarHeight && curFishPosition - 16f >= bobberBarPos - 32f;
	if (curFishPosition >= (548 - bobberBarHeight) && bobberBarPos >= (568 - bobberBarHeight - 4)) // extra check at top?
	{
		fishInBar = true;
	}

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

	foreach (var t in treasures)
	{
		t.treasureUpdate(time, bobberBarPos, bobberBarHeight);
	}

	if (fishInBar)
	{
		distanceFromCatching += 0.002f;
		reelRotation += (float)Math.PI / 8f;
		fishShake.X = (float)Game1.random.Next(-10, 11) / 10f;
		fishShake.Y = (float)Game1.random.Next(-10, 11) / 10f;
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
			perfect = false;
			Rumble.stopRumbling();
		}

		fishSizeReductionTimer -= time.ElapsedGameTime.Milliseconds;
		if (fishSizeReductionTimer <= 0)
		{
			fishSize = Math.Max(minFishSize, fishSize - 1);
			fishSizeReductionTimer = 800;
		}

		// Skip this to prevent failure
		if (false)
		{
			distanceFromCatching -= (0.003f) * distanceFromCatchPenaltyModifier;
		}

		float distanceAway = Math.Abs(curFishPosition - (bobberBarPos + (bobberBarHeight / 2)));
		reelRotation -= (float)Math.PI / Math.Max(10f, 200f - distanceAway);
		barShake.X = (float)Game1.random.Next(-10, 11) / 10f;
		barShake.Y = (float)Game1.random.Next(-10, 11) / 10f;
		fishShake = Vector2.Zero;
		reelSound?.Stop(AudioStopOptions.Immediate);
		if (unReelSound == null || unReelSound.IsStopped)
		{
			Game1.playSound("slowReel", 600, out unReelSound);
		}
	}
	
	distanceFromCatching = Math.Max(0f, Math.Min(1f, distanceFromCatching));
	
	if (Game1.player.CurrentTool != null)
	{
		Game1.player.CurrentTool.tickUpdate(time, Game1.player);
	}

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
		if (perfect)
		{
			sparkleText = new SparklingText(Game1.dialogueFont,
				Game1.content.LoadString("Strings\\UI:BobberBar_Perfect"), Color.Yellow, Color.White,
				rainbow: false, 0.1, 1500);
		}
		else if (fishSize == maxFishSize)
		{
			fishSize--;
		}
	}
	
	curFishPosition = Math.Clamp(curFishPosition, 0f, 548f);
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
			
			// bar background
			b.Draw(Game1.mouseCursors, new Vector2(xPositionOnScreen + 70, yPositionOnScreen + 296) + everythingShake,
				new Rectangle(644, 1999, 38, 150), Color.White * uiScale, 0f, new Vector2(18.5f, 74f) * uiScale, 4f * uiScale,
				SpriteEffects.None, 0.01f);
			
			// Gameplay bar
			if (uiScale == 1f)
			{
				// These 3 are bobber bar
				b.Draw(Game1.mouseCursors,
					new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos) + barShake +
					everythingShake, new Rectangle(682, 2078, 9, 2),
					fishInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				b.Draw(Game1.mouseCursors,
					new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos + 8) + barShake +
					everythingShake, new Rectangle(682, 2081, 9, 1),
					fishInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, new Vector2(4f, bobberBarHeight - 16), SpriteEffects.None,
					0.89f);
				b.Draw(Game1.mouseCursors,
					new Vector2(xPositionOnScreen + 64,
						yPositionOnScreen + 12 + (int)bobberBarPos + bobberBarHeight - 8) + barShake + everythingShake,
					new Rectangle(682, 2085, 9, 2),
					fishInBar
						? Color.White
						: (Color.White * 0.25f *
						   ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
							   2) + 2f)), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
				
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
				foreach (var t in treasures)
				{
					t.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);
				}

				// The fish!!
				b.Draw(Game1.mouseCursors,
					new Vector2(xPositionOnScreen + 64 + 18, (float)(yPositionOnScreen + 12 + 24) + curFishPosition) +
					fishShake + everythingShake, new Rectangle(614, 1840, 20, 20), Color.White,
					0f, new Vector2(10f, 10f), 2f, SpriteEffects.None, 0.88f);
				
				sparkleText?.draw(b, new Vector2(xPositionOnScreen - 16, yPositionOnScreen - 64)); // "perfect text"
			}
			
			Game1.EndWorldDrawInUI(b);
		}
	}
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.Tools;

namespace FishMod;

public class BasicBobberBar : IClickableMenu
{
    public bool handledFishResult;

    public float scale;

    public float everythingShakeTimer;
    
    public bool buttonPressed;

    public bool flipBubble;

    public bool preGameFadingIn;

    public bool gameEnding;

    public bool perfect = false;
    
    public int bobberBarHeight;
    
    public Vector2 barShake;

    public Vector2 everythingShake;
    
    private SparklingText sparkleText;

    public float bobberBarPos;

    public float bobberBarSpeed;

    public float distanceFromCatching = 0.3f;

    public static ICue reelSound;

    public static ICue unReelSound;

    public List<TreasureInstance> treasures = new();
    public bool treasureInBar;
    public int colorIndex;
    public Color color = Color.White;

    private CallBack minigameEndingCallback;
    public delegate void CallBack(int treasures, bool success);  

    public BasicBobberBar(CallBack completeCallback, int treasure, bool goldenTreasure = false, int colorIndex = -1)
        : base(0, 0, 96, 636)
    {
        minigameEndingCallback = completeCallback;
        TreasureSetup(treasure, goldenTreasure);
        BobberBarSetup(colorIndex);
            
        preGameFadingIn = true;
        scale = 0f;

        Reposition();
        Game1.setRichPresence("fishing", Game1.currentLocation.Name);
    }

    private void BobberBarSetup(int colorIndex)
    {
        this.colorIndex = colorIndex;
        bobberBarHeight = 96 + Game1.player.FishingLevel * 8;
        bobberBarPos = 568 - bobberBarHeight;
    }
    private void TreasureSetup(int treasureCount, bool goldenTreasure)
    {
        for (var i = 0; i < treasureCount; i++) treasures.Add(new TreasureInstance(i - 1, true));

        if (goldenTreasure && treasures.Count > 0) treasures[0].goldenTreasure = true;
    }
    
    #region randomUIStuff
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
            xPositionOnScreen = Game1.viewport.Width - 96;
        else if (xPositionOnScreen < 0) xPositionOnScreen = 0;

        if (yPositionOnScreen < 0)
            yPositionOnScreen = 0;
        else if (yPositionOnScreen + 636 > Game1.viewport.Height) yPositionOnScreen = Game1.viewport.Height - 636;
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
        if (!handledFishResult) Game1.playSound("fishEscape");

        gameEnding = true;
        everythingShakeTimer = 500f;
        distanceFromCatching = -1f;
    }

    public override void receiveKeyPress(Keys key)
    {
        if (Game1.options.menuButton.Contains(new InputButton(key))) emergencyShutDown();
    }
    
    #endregion
    
    private void EverythingShakeAndSparkleText(GameTime time)
    {
        if (sparkleText != null && sparkleText.update(time)) sparkleText = null;

        if (everythingShakeTimer > 0f)
        {
            everythingShakeTimer -= time.ElapsedGameTime.Milliseconds;
            everythingShake = new Vector2(Game1.random.Next(-10, 11) / 10f,
                Game1.random.Next(-10, 11) / 10f);
            if (everythingShakeTimer <= 0f) everythingShake = Vector2.Zero;
        }
    }

    private void FadeIn()
    {
        scale += 0.05f;
        if (scale >= 1f)
        {
            scale = 1f;
            preGameFadingIn = false;
        }
    }
    private void GameEnding()
    {
        if (everythingShakeTimer > 0f || sparkleText != null) return;

        scale -= 0.05f;
        if (scale <= 0f)
        {
            scale = 0f;
            gameEnding = false;
            
            if (distanceFromCatching > 0.9f)
            {
                int treasureCaughtCount = 0;
                foreach (var t in treasures)
                    if (t.treasureCaught)
                    {
                        treasureCaughtCount++; 
                    }
                
                // Do ending
                minigameEndingCallback(treasureCaughtCount, true);
            }
            else
            {
                minigameEndingCallback(0, false);
                Game1.player.completelyStopAnimatingOrDoingAction();
            }
            
            Game1.exitActiveMenu();
            Game1.setRichPresence("location", Game1.currentLocation.Name);
        }
    }
    private void BobberInput()
    {
        bool prevButtonPressed = buttonPressed;
            buttonPressed = Game1.oldMouseState.LeftButton == ButtonState.Pressed ||
                            Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton) ||
                            (Game1.options.gamepadControls && (Game1.oldPadState.IsButtonDown(Buttons.X) ||
                                                               Game1.oldPadState.IsButtonDown(Buttons.A)));
            if (!prevButtonPressed && buttonPressed) Game1.playSound("fishingRodBend");

            var gravity = buttonPressed ? -0.25f : 0.25f;
            if (buttonPressed && (bobberBarPos == 0f || bobberBarPos == 568 - bobberBarHeight))
                bobberBarSpeed = 0f;

            var oldPos = bobberBarPos;
            bobberBarSpeed += gravity;
            bobberBarPos += bobberBarSpeed;
            if (bobberBarPos + bobberBarHeight > 568f)
            {
                bobberBarPos = 568 - bobberBarHeight;
                bobberBarSpeed = (0f - bobberBarSpeed) * 2f / 3f;
                if (oldPos + bobberBarHeight < 568f) Game1.playSound("shiny4");
            }
            else if (bobberBarPos < 0f)
            {
                bobberBarPos = 0f;
                bobberBarSpeed = (0f - bobberBarSpeed) * 2f / 3f;
                if (oldPos > 0f) Game1.playSound("shiny4");
            }
    }

    protected virtual void TreasureUpdate(GameTime time)
    {
        treasureInBar = false;
        foreach (var t in treasures)
            if (t.treasureUpdate(time, bobberBarPos, bobberBarHeight))
                treasureInBar = true;
    }
    
    // Update progress, stop bar shake, start fish shake, play sound
    private void IncreaseProgress()
    {
        distanceFromCatching += 0.002f;
        barShake = Vector2.Zero;
        Rumble.rumble(0.1f, 1000f);
        unReelSound?.Stop(AudioStopOptions.Immediate);
        if (reelSound == null || reelSound.IsStopped || reelSound.IsStopping || !reelSound.IsPlaying)
            Game1.playSound("fastReel", out reelSound);
    }

    protected virtual void DecreaseProgress()
    {
        barShake.X = Game1.random.Next(-10, 11) / 10f;
        barShake.Y = Game1.random.Next(-10, 11) / 10f;
        reelSound?.Stop(AudioStopOptions.Immediate);
        if (unReelSound == null || unReelSound.IsStopped) Game1.playSound("slowReel", 600, out unReelSound);
        distanceFromCatching -= 0.003f;
    }

    private void CheckLoss()
    {
        if (distanceFromCatching <= 0f)
        {
            gameEnding = true;
            everythingShakeTimer = 500f;
            Game1.playSound("fishEscape");
            handledFishResult = true;
            unReelSound?.Stop(AudioStopOptions.Immediate);
            reelSound?.Stop(AudioStopOptions.Immediate);
        }
    }

    protected void VictoryEffects()
    {
        everythingShakeTimer = 500f;
        Game1.playSound("jingle1");
        gameEnding = true;
        handledFishResult = true;
        unReelSound?.Stop(AudioStopOptions.Immediate);
        reelSound?.Stop(AudioStopOptions.Immediate);
        if (perfect)
        {
            sparkleText = new SparklingText(Game1.dialogueFont,
                Game1.content.LoadString("Strings\\UI:BobberBar_Perfect"), Color.Yellow, Color.White,
                false, 0.1, 1500);
        }
    }
    public virtual void CheckVictory()
    {
        if (distanceFromCatching >= 1f)
        {
            VictoryEffects();
        }
    }

    public override void update(GameTime time)
    {
        Reposition();
        EverythingShakeAndSparkleText(time);

        if (preGameFadingIn)
        {
            FadeIn();
        }
        else if (gameEnding)
        {
            GameEnding();
        }
        else
        {
            BobberInput();

            TreasureUpdate(time);

            IncreaseProgress();
            
            DecreaseProgress();

            distanceFromCatching = Math.Max(0f, Math.Min(1f, distanceFromCatching));
            if (Game1.player.CurrentTool != null) Game1.player.CurrentTool.tickUpdate(time, Game1.player);

            CheckLoss();
            CheckVictory();
        }
    }


    public override void draw(SpriteBatch b)
    {
        Game1.StartWorldDrawInUI(b);
        DrawSpeechBubble(b);
        DrawBackground(b);
        if (scale == 1f)
        {
            DrawBobberBar(b);
            
            DrawProgressBar(b);

            DrawTreasures(b);
            
            sparkleText?.draw(b, new Vector2(xPositionOnScreen - 16, yPositionOnScreen - 64));
        }
        Game1.EndWorldDrawInUI(b);
    }

    protected virtual void DrawTreasures(SpriteBatch b)
    {
        foreach (var t in treasures) t.drawTreasure(b, everythingShake, xPositionOnScreen, yPositionOnScreen);
    }

    void DrawSpeechBubble(SpriteBatch b)
    {
        // bar white background transparent
        b.Draw(Game1.mouseCursors,
            new Vector2(xPositionOnScreen - (flipBubble ? 44 : 20) + 104, yPositionOnScreen - 16 + 314) +
            everythingShake, new Rectangle(652, 1685, 52, 157), Color.White * 0.6f * scale, 0f,
            new Vector2(26f, 78.5f) * scale, 4f * scale,
            flipBubble ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.001f);
    }

    void DrawBackground(SpriteBatch b)
    {
        // bar background
        b.Draw(Game1.mouseCursors, new Vector2(xPositionOnScreen + 70, yPositionOnScreen + 296) + everythingShake,
            new Rectangle(644, 1999, 38, 150), Color.White * scale, 0f, new Vector2(18.5f, 74f) * scale, 4f * scale,
            SpriteEffects.None, 0.01f);
    }
    
    void DrawBobberBar(SpriteBatch b)
    {
        // These 3 are bobber bar
        b.Draw(DeluxeFishingRodTool.fishingTextures,
            new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos) + barShake +
            everythingShake, new Rectangle(216, 447 + 10 * colorIndex, 9, 2),
            treasureInBar
                ? color
                : color * 0.25f *
                  ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
                      2) + 2f), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
        b.Draw(DeluxeFishingRodTool.fishingTextures,
            new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 12 + (int)bobberBarPos + 8) + barShake +
            everythingShake, new Rectangle(216, 453 + 10 * colorIndex, 9, 1),
            treasureInBar
                ? color
                : color * 0.25f *
                  ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
                      2) + 2f), 0f, Vector2.Zero, new Vector2(4f, bobberBarHeight - 16), SpriteEffects.None,
            0.89f);
        b.Draw(DeluxeFishingRodTool.fishingTextures,
            new Vector2(xPositionOnScreen + 64,
                yPositionOnScreen + 12 + (int)bobberBarPos + bobberBarHeight - 8) + barShake + everythingShake,
            new Rectangle(216, 454 + 10 * colorIndex, 9, 2),
            treasureInBar
                ? color
                : color * 0.25f *
                  ((float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 100.0),
                      2) + 2f), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
    }

    void DrawProgressBar(SpriteBatch b)
    {
        // current level of progress bar
        b.Draw(Game1.staminaRect,
            new Rectangle(xPositionOnScreen + 124,
                yPositionOnScreen + 4 + (int)(580f * (1f - distanceFromCatching)), 16,
                (int)(580f * distanceFromCatching)), Utility.getRedToGreenLerpColor(distanceFromCatching));

    }
}
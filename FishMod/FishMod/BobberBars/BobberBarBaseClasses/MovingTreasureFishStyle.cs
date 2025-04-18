using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Extensions;

namespace FishMod;

public class MovingTreasureFishStyle : TreasureInstance
{
    private float treasureTargetPosition;
	private float treasureSpeed;
	private float floaterSinkerAcceleration;
	
	public int difficulty;
	public MotionType motionType;

	private float minBound = 16f;
	private float maxBound = 520f;
	private float absoluteMinBound = 0f;
	private float absoluteMaxBound = 548f;
	
	public enum MotionType
	{
		Mixed,
		Dart,
		Smooth,
		Sinker,
		Floater
	}
	
    public MovingTreasureFishStyle(int difficulty, MotionType motionType, int treasureAppearMin = 200, int treasureAppearMax = 3000, bool canLoseTreasure = false) 
	    : base(TreasureSprites.Fish, false, treasureAppearMin, treasureAppearMax, false, canLoseTreasure)
    {
	    this.difficulty = difficulty;
	    this.motionType = motionType;
	    treasureTargetPosition = (100f - difficulty) / 100f * absoluteMaxBound;
    }

    public override bool treasureUpdate(GameTime time, float bobberBarPos, int bobberBarHeight)
    {
	    if (treasureAppearTimer <= 0f)
	    {
		    updatePosition();
	    }
        return base.treasureUpdate(time, bobberBarPos, bobberBarHeight);
    }

    private void updatePosition()
    {
	    if (Game1.random.NextDouble() < difficulty * (float)(motionType != MotionType.Smooth ? 1 : 20) / 4000f &&
	        (motionType != MotionType.Smooth || treasureTargetPosition == -1f))
	    {
		    var spaceBelow = absoluteMaxBound - treasurePosition;
		    float spaceAbove = treasurePosition;
		    var percent = Math.Min(99f, difficulty + (float)Game1.random.Next(10, 45)) / 100f;
		    treasureTargetPosition = treasurePosition +
		                             Game1.random.Next((int)Math.Min(0f - spaceAbove, spaceBelow),
			                             (int)spaceBelow) * percent;
	    }

	    switch (motionType)
	    {
		    case MotionType.Sinker:
			    floaterSinkerAcceleration = Math.Max(floaterSinkerAcceleration - 0.01f, -1.5f);
			    break;
		    case MotionType.Floater:
			    floaterSinkerAcceleration = Math.Min(floaterSinkerAcceleration + 0.01f, 1.5f);
			    break;
	    }

	    if (Math.Abs(treasurePosition - treasureTargetPosition) > 3f && treasureTargetPosition != -1f)
	    {
		    float acceleration = (treasureTargetPosition - treasurePosition) /
		                         (Game1.random.Next(10, 30) + (100f - Math.Min(100f, difficulty)));
		    treasureSpeed += (acceleration - treasureSpeed) / 5f;
	    }
	    else if (motionType != MotionType.Smooth && Game1.random.NextDouble() < difficulty / 2000f)
	    {
		    treasureTargetPosition = treasurePosition + (Game1.random.NextBool()
			    ? Game1.random.Next(-100, -51)
			    : Game1.random.Next(50, 101));
	    }
	    else
	    {
		    treasureTargetPosition = -1f;
	    }

	    if (motionType == MotionType.Dart && Game1.random.NextDouble() < difficulty / 1000f)
		    treasureTargetPosition = treasurePosition + (Game1.random.NextBool()
			    ? Game1.random.Next(-100 - difficulty * 2, -51)
			    : Game1.random.Next(50, 101 + difficulty * 2));

	    treasureTargetPosition = Math.Max(-1f, Math.Min(treasureTargetPosition, absoluteMaxBound));
	    treasurePosition += treasureSpeed + floaterSinkerAcceleration;
	    treasurePosition = Math.Clamp(treasurePosition, 0f, absoluteMaxBound);
    }
}
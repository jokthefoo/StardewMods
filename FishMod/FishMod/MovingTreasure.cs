using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Extensions;

namespace FishMod;

public class MovingTreasure : TreasureInstance
{
	private float treasureTargetPosition;
	private float treasureAcceleration;
	private float treasureSpeed;
	public int difficulty;
	public MotionType motionType;

	public enum MotionType
	{
		Mixed,
		Walk
	}
	
    public MovingTreasure(int spriteId, bool realTreasure, int treasureAppearMin = 1000, int treasureAppearMax = 3000, bool goldenTreasure = false, bool canLoseTreasure = false) : base(spriteId, realTreasure, treasureAppearMin, treasureAppearMax, goldenTreasure, canLoseTreasure)
    {
	    difficulty = 50;
	    motionType = Game1.random.NextBool() ? MotionType.Walk : MotionType.Mixed ;
	    treasureSpeed = 5f;
	    treasureTargetPosition = (100f - difficulty) / 100f * 548f;
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
	    switch (motionType)
	    {
		    case MotionType.Walk:
			    MotionWalk();
			    break;
		    case MotionType.Mixed:
			    MotionMixed();
			    break;
	    }

	    treasureTargetPosition = Math.Clamp(treasureTargetPosition, -1f, 548f);
	    treasurePosition += treasureSpeed;
	    treasurePosition = Math.Clamp(treasurePosition, 0f, 532f);
    }
    
    private void MotionWalk()
    {
	    if (Math.Abs(treasurePosition - treasureTargetPosition) < 5f)
	    {
		    treasureSpeed = Game1.random.Next(2, difficulty / 4);
		    treasureTargetPosition += Game1.random.NextBool() ? Game1.random.Next(20, 80) : -Game1.random.Next(20, 80);
		    treasureTargetPosition = Math.Clamp(treasureTargetPosition, 0f, 532f);
	    }
    }
    
    private void MotionMixed()
    {
	    if (Game1.random.NextDouble() < difficulty / 4000f)
	    {
		    var spaceBelow = 548f - treasurePosition;
		    float spaceAbove = treasurePosition;
		    var percent = Math.Min(99f, difficulty + Game1.random.Next(10, 45)) / 100f;
		    treasureTargetPosition = treasurePosition +
		                             Game1.random.Next((int)Math.Min(-spaceAbove, spaceBelow),
			                             (int)spaceBelow) * percent;
	    }

	    if (Math.Abs(treasurePosition - treasureTargetPosition) > 3f && treasureTargetPosition != -1f)
	    {
		    treasureAcceleration = (treasureTargetPosition - treasurePosition) /
		                           (Game1.random.Next(10, 30) + (100f - difficulty));
		    treasureSpeed += (treasureAcceleration - treasureSpeed) / 5f;
	    }
	    else if (Game1.random.NextDouble() < difficulty / 2000f)
	    {
		    treasureTargetPosition = treasurePosition + (Game1.random.NextBool()
			    ? Game1.random.Next(-100, -51)
			    : Game1.random.Next(50, 101));
	    }
	    else
	    {
		    treasureTargetPosition = -1f;
	    }
    }
}
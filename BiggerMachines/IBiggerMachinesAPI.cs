using Microsoft.Xna.Framework;
using StardewValley;
using Object = StardewValley.Object;

namespace BiggerMachines;

public interface IBiggerMachinesAPI
{
    // Returns the object (or Bigger Machine) at tile location, or null if none are found.
    public bool TryGetObjectAt(GameLocation location, Vector2 tile, out Object? obj);
    
    // Returns the SourceRectangle for a bigger machine for drawing. Default if not bigger machine.
    public bool GetBiggerMachineTextureSourceRect(Object obj, out Rectangle sourceRect);
    
    // Returns if the specified objects is a Bigger Machine
    public bool IsBiggerMachine(Object obj);
}
using Microsoft.Xna.Framework;
using StardewValley;
using Object = StardewValley.Object;

namespace BiggerMachines;

public interface IBiggerMachinesAPI
{
    // Returns the object (or Bigger Machine) at tile location, or null if none are found.
    public bool TryGetObjectAt(GameLocation location, Vector2 tile, out Object? obj);
    // Returns the SourceRectangle for a bigger machine for drawing. Null if not a bigger machine.
    public Rectangle? GetBiggerMachineTextureSourceRect(Object obj);
}
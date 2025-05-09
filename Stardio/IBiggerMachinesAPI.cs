using Microsoft.Xna.Framework;
using StardewValley;
using Object = StardewValley.Object;

namespace Jok.Stardio;

public interface IBiggerMachinesAPI
{
    // Returns true if an object was found, and the object (or Bigger Machine) at tile location, or false and null if none are found.
    public bool TryGetObjectAt(GameLocation location, Vector2 tile, out Object? obj);
    // Returns the SourceRectangle for a bigger machine for drawing. Null if not a bigger machine.
    public Rectangle? GetBiggerMachineTextureSourceRect(Object obj);
}
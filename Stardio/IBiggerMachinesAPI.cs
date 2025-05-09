using Microsoft.Xna.Framework;
using StardewValley;
using Object = StardewValley.Object;

namespace Jok.Stardio;

public interface IBiggerMachinesAPI
{
    // Returns the object (or Bigger Machine) at tile location, or null if none are found.
    public bool TryGetObjectAt(GameLocation location, Vector2 tile, out Object? obj);
    // Returns the SourceRectangle for a bigger machine for drawing. Null if not a bigger machine.
    public bool GetBiggerMachineTextureSourceRect(Object obj, out Rectangle sourceRect);
    
    // Returns if the specified objects is a Bigger Machine
    public bool IsBiggerMachine(Object obj);
}
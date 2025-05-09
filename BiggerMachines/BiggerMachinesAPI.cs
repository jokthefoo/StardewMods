using Microsoft.Xna.Framework;
using StardewValley;
using Object = StardewValley.Object;

namespace BiggerMachines;
public class BiggerMachinesAPI : IBiggerMachinesAPI
{
    public bool TryGetObjectAt(GameLocation location, Vector2 tile, out Object? outObject)
    {
        outObject = null;
        // Check normal items first
        if (location.objects.TryGetValue(tile, out Object? obj))
        {
            outObject = obj;
            return true;
        }
        
        // No BMs here at all
        if (!ModEntry.LocationBigMachines.ContainsKey(location.Name))
        {
            return false;
        }

        var position = default(Point);
        position.X = (int)((int)tile.X + 0.5f) * 64;
        position.Y = (int)((int)tile.Y + 0.5f) * 64;

        foreach (var (pos, bm) in ModEntry.LocationBigMachines[location.Name])
        {
            if (bm.GetBoundingBox().Contains(position))
            {
                outObject = bm.Object;
                return true;
            }
        }

        return false;
    }

    public bool GetBiggerMachineTextureSourceRect(Object obj, out Rectangle sourceRect)
    {
        var itemData = ItemRegistry.GetDataOrErrorItem(obj.QualifiedItemId);
        if (!obj.bigCraftable.Value || !ModEntry.BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData))
        {
            sourceRect = itemData.GetSourceRect();
            return false;
        }
        sourceRect = new Rectangle(0, 0, bigMachineData.Width * 16, itemData.GetTexture().Height);
        return true;
    }

    public bool IsBiggerMachine(Object obj)
    {
        return ModEntry.BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData);
    }
}
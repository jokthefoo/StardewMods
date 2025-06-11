using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace FluidPipes;

[XmlType("Mods_Jok_PipeItem")]
public class FluidPipe : Object
{
    [XmlElement("spriteOffset")]
    public readonly NetInt spriteOffset = new();
    public FluidPipe() : base()
    {
        NetFields.AddField(spriteOffset);
        ParentSheetIndex = 4;
        spriteOffset.Value = 4;
    }
    public FluidPipe(string itemId, int stack) : base(itemId, stack)
    {
        ItemId = itemId;
        ReloadData(ItemId);
    }

    protected override Item GetOneNew() // needed for right-clicking item
    {
        return new FluidPipe();
    }
    
    protected override void GetOneCopyFrom(Item source)
    {
        base.GetOneCopyFrom(source);
        ItemId = source.ItemId;
        ReloadData(ItemId);
        spriteOffset.Value = 4;
    }

    private void ReloadData(string itemid)
    {
        if (Game1.objectData.TryGetValue(itemid, out var data))
        {
            name = data.Name ?? ItemRegistry.GetDataOrErrorItem(base.QualifiedItemId).InternalName;
            price.Value = data.Price;
            edibility.Value = data.Edibility;
            type.Value = data.Type;
            Category = data.Category;
        }
        canBeSetDown.Value = true;
        canBeGrabbed.Value = true;
        ParentSheetIndex = 4;
        spriteOffset.Value = 4;
    }
    
    public override bool placementAction(GameLocation location, int x, int y, Farmer who = null)
    {
        bool wasPlaced = base.placementAction(location, x, y, who);

        if (wasPlaced)
        {
            CheckForNeighborCurves();
        }
        return wasPlaced;
    }
    
    public static void CheckForNeighborCurves(Vector2 tileLocation, GameLocation location)
    {
        if (location.objects.TryGetValue(tileLocation + new Vector2(0,-1), out Object obj1) && obj1 is FluidPipe pipe1)
        {
            pipe1.CheckForCurve();
        }
        if (location.objects.TryGetValue(tileLocation + new Vector2(0,1), out Object obj2) && obj2 is FluidPipe pipe2)
        {
            pipe2.CheckForCurve();
        }
        if (location.objects.TryGetValue(tileLocation + new Vector2(-1,0), out Object obj3) && obj3 is FluidPipe pipe3)
        {
            pipe3.CheckForCurve();
        }
        if (location.objects.TryGetValue(tileLocation + new Vector2(1,0), out Object obj4) && obj4 is FluidPipe pipe4)
        {
            pipe4.CheckForCurve();
        }
    }
    
    public void CheckForNeighborCurves()
    {
        CheckForNeighborCurves(TileLocation, Location);
    }
    
    public override void draw(SpriteBatch spriteBatch, int x, int y, float alpha = 1f)
    {
        if (isTemporarilyInvisible)
        {
            return;
        }

        if (!Game1.eventUp || (Game1.CurrentEvent != null && !Game1.CurrentEvent.isTileWalkedOn(x, y)))
        {
            var bounds = GetBoundingBoxAt(x, y);
            var shake = shakeTimer > 0 ? new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2)) : Vector2.Zero;
            var itemData = ItemRegistry.GetDataOrErrorItem(QualifiedItemId);
            
            DrawVWater(spriteBatch, x, y, alpha, shake, bounds);
            DrawHWater(spriteBatch, x, y, alpha, shake, bounds);
            
            spriteBatch.Draw(itemData.GetTexture(),
                    Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 32) + shake),
                    itemData.GetSourceRect(spriteOffset.Value - 4), Color.White * alpha, 0f, new Vector2(8f, 8f), scale.Y > 1f ? getScale().Y : 4f, SpriteEffects.None,
                    (bounds.Top - 100) / 10000f);
        }
    }

    private void DrawHWater(SpriteBatch spriteBatch, int x, int y, float alpha, Vector2 shake, Rectangle bounds)
    {
        if (spriteOffset.Value == 5 || spriteOffset.Value == 19)
        {
            // draw me some water
            spriteBatch.Draw(Game1.mouseCursors, 
                Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 + 24) + shake),
                new Rectangle(Game1.currentLocation.waterAnimationIndex * 64, 2064, 64, 16), Color.White * alpha, 0f, new Vector2(0, 0), 1f, SpriteEffects.None,
                (bounds.Top - 100 - 2) / 10000f);
        }
    }
    
    private void DrawVWater(SpriteBatch spriteBatch, int x, int y, float alpha, Vector2 shake, Rectangle bounds)
    {
        if (spriteOffset.Value == 5 || spriteOffset.Value == 17)
        {
            spriteBatch.Draw(Game1.mouseCursors, 
                Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 26, y * 64) + shake),
                new Rectangle(Game1.currentLocation.waterAnimationIndex * 64, 2064, 16, 64), Color.White * alpha, 0f, new Vector2(0, 0), 1f, SpriteEffects.None,
                (bounds.Top - 100 - 2) / 10000f);
        }
    }
    
    public override bool isPassable()
    {
        return true;
    }

    public void CheckForCurve()
    {
        spriteOffset.Value = 4;

        bool up = false;
        bool down = false;
        bool left = false;
        bool right = false;
        if (ModEntry.BMApi != null)
        {
            up = ModEntry.BMApi.TryGetObjectAt(Location,TileLocation + new Vector2(0, -1), out Object upObj) && (upObj is FluidPipe || upObj.bigCraftable.Value);
            down = ModEntry.BMApi.TryGetObjectAt(Location,TileLocation + new Vector2(0, 1), out Object downObj) && (downObj is FluidPipe || downObj.bigCraftable.Value);
            left = ModEntry.BMApi.TryGetObjectAt(Location,TileLocation + new Vector2(-1, 0), out Object leftObj) && (leftObj is FluidPipe || leftObj.bigCraftable.Value);
            right = ModEntry.BMApi.TryGetObjectAt(Location,TileLocation + new Vector2(1, 0), out Object rightObj) && (rightObj is FluidPipe || rightObj.bigCraftable.Value);
        }
        else
        {
           up = Location.objects.TryGetValue(TileLocation + new Vector2(0, -1), out Object upObj) && (upObj is FluidPipe || upObj.bigCraftable.Value);
           down = Location.objects.TryGetValue(TileLocation + new Vector2(0, 1), out Object downObj) && (downObj is FluidPipe || downObj.bigCraftable.Value);
           left = Location.objects.TryGetValue(TileLocation + new Vector2(-1, 0), out Object leftObj) && (leftObj is FluidPipe || leftObj.bigCraftable.Value);
           right = Location.objects.TryGetValue(TileLocation + new Vector2(1, 0), out Object rightObj) && (rightObj is FluidPipe || rightObj.bigCraftable.Value);
        }

        if (up && down && left && right)
        {
            spriteOffset.Value = 5;
        }
        else if (up && down)
        {
            if (left)
            {
                spriteOffset.Value = 10;
                return;
            }
            
            if (right)
            {
                spriteOffset.Value = 9;
                return;
            }
            

            if ((int)TileLocation.X % 2 == (int)TileLocation.Y % 2)
            {
                
                spriteOffset.Value = 16;
            }
            else
            {
                spriteOffset.Value = 17;
            }
        }
        else if (left && right)
        {
            if (up)
            {
                spriteOffset.Value = 8;
                return;
            }
            
            if (down)
            {
                spriteOffset.Value = 11;
                return;
            }

            if ((int)TileLocation.X % 2 == (int)TileLocation.Y % 2)
            {
                
                spriteOffset.Value = 18;
            }
            else
            {
                spriteOffset.Value = 19;
            }
        }
        else if (up && right)
        {
            spriteOffset.Value = 12;
        }
        else if (up && left)
        {
            spriteOffset.Value = 14;
        }
        else if (down && right)
        {
            spriteOffset.Value = 15;
        }
        else if (down && left)
        {
            spriteOffset.Value = 13;
        }
        else
        {
            spriteOffset.Value = 4;
        }
    }
}
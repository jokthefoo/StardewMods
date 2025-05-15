using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using Object = StardewValley.Object;

namespace Jok.Stardio;

[XmlType("Mods_Jok_SplitterItem")]
public class SplitterItem : Object
{
    public override string DisplayName => GetDisplayName();
    public string Description { get; set; }
    public override string TypeDefinitionId => "(Jok.Belt)";
    
    [XmlIgnore]
    private BeltItem.Direction nextDirection = BeltItem.Direction.Forward;
    [XmlIgnore] public float HeldItemPosition;

    public readonly NetString objName = new();
    
    public SplitterItem()
    {
        ParentSheetIndex = 4;
    }
    
    public SplitterItem(string itemid)
        : this()
    {
        ItemId = itemid;
        ReloadData(itemid);
    }

    private void ReloadData(string itemid)
    {
        var data = Game1.content.Load< Dictionary< string, BeltData > >("Jok.Stardio/FactoryItems");
        Category = equipmentCategory;
        Name = itemid;
        price.Value = data[ItemId].Price;
        displayName = data[ItemId].DisplayName;
        Description = data[ItemId].Description;
        ParentSheetIndex = 4;
        type.Value = "Crafting";
    }
    
    private string GetDisplayName()
    {
        try
        {
            if (!string.IsNullOrEmpty(objName.Value))
                return objName.Value;

            var data = Game1.content.Load<Dictionary<string, BeltData>>($"Jok.Stardio/FactoryItems");
            return data[ItemId].DisplayName;
        }
        catch (Exception)
        {
            return "Error Item";
        }
    }
    
    protected override Item GetOneNew() // needed for right-clicking item
    {
        return new SplitterItem();
    }

    protected override void GetOneCopyFrom(Item source)
    {
        base.GetOneCopyFrom(source);
        ItemId = source.ItemId;
        ReloadData(ItemId);
    }

    public override string getCategoryName()
    {
        return I18n.Belt_Category_Name();
    }
    
    public override Color getCategoryColor()
    {
        return new Color(255,50,50,255);
    }

    public override int maximumStackSize()
    {
        return 999;
    }
    
    public override bool canBeGivenAsGift()
    {
        return false;
    }
    
    public void splitterUpdate(bool isProcessTick)
    {
        if (Location == null)
        {
            return;
        }
        
        if (heldObject.Value != null && HeldItemPosition < .4f)
        {
            HeldItemPosition += 1.0f / Math.Clamp(ModEntry.Config.BeltUpdateMS, 10, ModEntry.Config.BeltUpdateMS);
            readyForHarvest.Value = true;
        }
        HeldItemPosition = Math.Clamp(HeldItemPosition, 0.0f, 1.0f);
        
        if (isProcessTick)
        {
            if (heldObject.Value == null || HeldItemPosition < .4f)
            {
                return;
            }
            nextDirection = (BeltItem.Direction)((int)nextDirection % 4);
            // try to push to belt
            if (TryPushToBelt(nextDirection++))
            {
                return;
            }
            if (TryPushToBelt(nextDirection++))
            {
                return;
            }
            if (TryPushToBelt(nextDirection++))
            {
                return;
            }
            if (TryPushToBelt(nextDirection++))
            {
                return;
            }
        }
    }
    
    private bool TryPushToBelt(BeltItem.Direction dir)
    {
        var targetTile = getTileInDirection(dir);
        if (Location.objects.TryGetValue(targetTile, out var outputTarget))
        {
            if (outputTarget is BeltItem belt && belt.heldObject.Value == null && belt.getTileInDirection(BeltItem.Direction.Forward, belt.TileLocation) != TileLocation)
            {
                belt.heldObject.Value = heldObject.Value;

                heldObject.Value = null;
                HeldItemPosition = 0;
                return true;
            }
        }
        return false;
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

            var spriteEffects = SpriteEffects.None;
            int sourceOffset = BridgeItem.BridgeAnim;
            
            spriteBatch.Draw(itemData.GetTexture(),
                Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 32) + shake),
                itemData.GetSourceRect(sourceOffset, itemData.SpriteIndex), Color.White * alpha, 0f, new Vector2(8f, 8f), scale.Y > 1f ? getScale().Y : 4f, spriteEffects,
                (isPassable() ? bounds.Top : bounds.Center.Y + 40) / 10000f);
        }
    }
    
    public override bool performToolAction(Tool t)
    {
        var returnVal = base.performToolAction(t);
        if (returnVal && heldObject.Value != null)
        {
            Location.debris.Add(new Debris(heldObject.Value, tileLocation.Value * 64f + new Vector2(32f, 32f)));
        }
        return returnVal;
    }
    
    public override bool isPassable()
    {
        return false;
    }

    public override bool placementAction(GameLocation location, int x, int y, Farmer who = null)
    {
        bool wasPlaced = base.placementAction(location, x, y, who);
        if (wasPlaced)
        {
            UpdateNeighborCurves();
        }
        return wasPlaced;
    }

    private void UpdateNeighborCurves()
    {
        UpdateNeighborCurves(TileLocation);
    }
    
    public void UpdateNeighborCurves(Vector2 tileLoc)
    {
        if (Location.objects.TryGetValue(getTileInDirection(BeltItem.Direction.Forward, tileLoc), out Object obj1) && obj1 is BeltItem forwardBelt)
        {
            forwardBelt.CheckForCurve();
        }
        if (Location.objects.TryGetValue(getTileInDirection(BeltItem.Direction.Right, tileLoc), out Object obj2) && obj2 is BeltItem rightBelt)
        {
            rightBelt.CheckForCurve();
        }
        if (Location.objects.TryGetValue(getTileInDirection(BeltItem.Direction.Left, tileLoc), out Object obj3) && obj3 is BeltItem leftBelt)
        {
            leftBelt.CheckForCurve();
        }
        if (Location.objects.TryGetValue(getTileInDirection(BeltItem.Direction.Behind, tileLoc), out Object obj4) && obj4 is BeltItem backBelt)
        {
            backBelt.CheckForCurve();
        }
    }

    public Vector2 getTileInDirection(BeltItem.Direction dir, Vector2 tileLoc)
    {
        var rot = (int)dir % 4;
        return tileLoc + BeltItem.rotationDict[rot];
    }
    
    private Vector2 getTileInDirection(BeltItem.Direction dir)
    {
        return getTileInDirection(dir, TileLocation);
    }
}
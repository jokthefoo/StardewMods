using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace Jok.Stardio;

[XmlType("Mods_Jok_BeltItem2")]
public class BeltItem2 : BeltItem
{
    [XmlIgnore]
    public static int Belt2Anim = 0;
    [XmlIgnore]
    public static int belt2AnimUpdateCountdown = 0;
    
    public BeltItem2()
    {
        NetFields.AddField(objName).AddField(currentRotation);
        ParentSheetIndex = 0;
    }
    
    public BeltItem2(string itemid)
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
        ParentSheetIndex = 0;
        type.Value = "Crafting";
    }
    protected override Item GetOneNew() // needed for right-clicking item
    {
        return new BeltItem2();
    }

    protected override void GetOneCopyFrom(Item source)
    {
        base.GetOneCopyFrom(source);
        ItemId = source.ItemId;
        if (source is BeltItem2 beltFrom)
        {
            currentRotation.Value = beltFrom.currentRotation.Value;
        }
        ReloadData(ItemId);
    }
    
    public override void beltUpdate(bool isProcessTick)
    {
        if (Location == null)
        {
            return;
        }
        
        if (heldObject.Value != null && HeldItemPosition < 1.0f)
        {
            HeldItemPosition += 1.0f / Math.Clamp(ModEntry.Config.BeltUpdateMS / 2, 10, ModEntry.Config.BeltUpdateMS / 2);
            readyForHarvest.Value = true;
        }
        HeldItemPosition = Math.Clamp(HeldItemPosition, 0.0f, 1.0f);
        
        if (isProcessTick)
        {
            PushItem(Direction.Forward);
            BeltPullItem();
        }
    }
    
    public override int GetBeltAnim()
    {
        return Belt2Anim;
    }
}
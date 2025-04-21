using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;

namespace ModularTools;

[XmlType("Mods_Jok_ModularUpgradeItem")]
public class ModularUpgradeItem : StardewValley.Object
{
    public override string DisplayName => GetDisplayName();
    public string Description { get; set; }

    public override string TypeDefinitionId => "(Jok.MU)";
    
    public readonly NetString upgradeName = new();

    public ModularUpgradeItem()
    {
        NetFields.AddField(upgradeName);
    }
    
    public ModularUpgradeItem(string itemid)
        : this()
    {
        ItemId = itemid;
        ReloadData(itemid);
    }

    private void ReloadData(string itemid)
    {
        var data = Game1.content.Load< Dictionary< string, ModularUpgradeData > >("Jok.ModularTools/ModularUpgrades");
        Category = equipmentCategory;
        Name = itemid;
        price.Value = data[ItemId].Price;
        displayName = data[ItemId].DisplayName;
        Description = data[ItemId].Description;
    }

    private string GetDisplayName()
    {
        try
        {
            if (!string.IsNullOrEmpty(upgradeName.Value))
                return upgradeName.Value;

            var data = Game1.content.Load<Dictionary<string, ModularUpgradeData>>($"Jok.ModularTools/ModularUpgrades");
            return data[ItemId].DisplayName;
        }
        catch (Exception e)
        {
            return "Error Item";
        }
    }
    
    protected override Item GetOneNew() // needed for right clicking item
    {
        return new ModularUpgradeItem();
    }
    
    protected override void GetOneCopyFrom(Item source)
    {
        base.GetOneCopyFrom(source);

        ItemId = source.ItemId;
        ReloadData(ItemId);
    }

    public override void drawInMenu(SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency,
        float layerDepth, StackDrawType drawStackNumber, Color color, bool drawShadow)
    {
        //spriteBatch.Draw( Assets.Necklaces, location + new Vector2( 32, 32 ), new Rectangle( ( ( int ) necklaceType.Value ) % 4 * 16, ( ( int ) necklaceType.Value ) / 4 * 16, 16, 16 ), color * transparency, 0, new Vector2( 8, 8 ) * scaleSize, scaleSize * Game1.pixelZoom, SpriteEffects.None, layerDepth );
        var data = ItemRegistry.GetDataOrErrorItem(QualifiedItemId);
        Rectangle rect = data.GetSourceRect(0);
        spriteBatch.Draw(data.GetTexture(), location + new Vector2(32, 32) * scaleSize, rect, color * transparency, 0,
            new Vector2(8, 8) * scaleSize, scaleSize * 4, SpriteEffects.None, layerDepth);
    }
}
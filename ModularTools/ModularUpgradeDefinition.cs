using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TokenizableStrings;

namespace ModularTools;

public class ModularUpgradeDefinition : BaseItemDataDefinition
{
    public override string Identifier => "(Jok.MU)";
    public override string StandardDescriptor => "Jok.MU";
    
    private int category = StardewValley.Object.equipmentCategory;
    
    public override IEnumerable<string> GetAllIds()
    {
        return GetDataSheet().Keys;
    }

    public override bool Exists(string itemId)
    {
        return GetDataSheet().ContainsKey(itemId);
    }

    public override ParsedItemData GetData(string itemId)
    {
        if (!GetDataSheet().TryGetValue(itemId, out var data))
        {
            return null;
        }
        return new ParsedItemData(this, itemId, data.TextureIndex, data.Texture, itemId, TokenParser.ParseText(data.DisplayName), TokenParser.ParseText(data.Description), category, null, null);
    }

    public override Item CreateItem(ParsedItemData data)
    {
        return new ModularUpgradeItem(data.ItemId);
    }

    public override Rectangle GetSourceRect(ParsedItemData data, Texture2D tileSheet, int spriteIndex)
    {
        int width = 16;
        int height = 16;
        return new Rectangle(spriteIndex * width % tileSheet.Width, spriteIndex * width / tileSheet.Width * height, width, height);
    }
    
    protected Dictionary<string, ModularUpgradeData> GetDataSheet()
    {
        return Game1.content.Load<Dictionary<string, ModularUpgradeData>>("Jok.ModularTools/ModularUpgrades");
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TokenizableStrings;

namespace Jok.Stardio;

public class BeltItemDataDefinition : BaseItemDataDefinition
{
    public override string Identifier => "(Jok.Belt)";
    
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
        return new ParsedItemData(this, itemId, data.TextureIndex, data.Texture, itemId, TokenParser.ParseText(data.DisplayName), TokenParser.ParseText(data.Description), StardewValley.Object.equipmentCategory, null, null);
    }

    public override Item CreateItem(ParsedItemData data)
    {
        if(data.ItemId == "Jok.Stardio.Bridge")
        {
            return new BridgeItem(data.ItemId);
        }
        
        if(data.ItemId == "Jok.Stardio.Splitter")
        {
            return new SplitterItem(data.ItemId);
        }
        
        if(data.ItemId == "Jok.Stardio.Filter")
        {
            return new FilterItem(data.ItemId);
        }

        if(data.ItemId == "Jok.Stardio.Belt2")
        {
            return new BeltItem2(data.ItemId);
        }
        
        if(data.ItemId == "Jok.Stardio.Belt3")
        {
            return new BeltItem3(data.ItemId);
        }

        return new BeltItem(data.ItemId);
    }

    public override Rectangle GetSourceRect(ParsedItemData data, Texture2D texture, int spriteIndex)
    {
        int width = 16;
        int height = 16;
        if (data.ItemId == "Jok.Stardio.Filter")
        {
            height = 32;
        }
        return new Rectangle(spriteIndex * width % texture.Width, spriteIndex * width / texture.Width * height, width, height);
    }
    
    protected Dictionary<string, BeltData> GetDataSheet()
    {
        return Game1.content.Load<Dictionary<string, BeltData>>("Jok.Stardio/FactoryItems");
    }
}
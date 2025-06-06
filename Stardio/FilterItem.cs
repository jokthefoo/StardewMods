﻿using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using Object = StardewValley.Object;

namespace Jok.Stardio;

[XmlType("Mods_Jok_FilterItem")]
public class FilterItem : IBeltPushing
{
    [XmlIgnore]
    public int pushDirection = 1;
    
    public override string DisplayName => GetDisplayName();
    public string Description { get; set; }
    public override string TypeDefinitionId => "(Jok.Belt)";

    public readonly NetString objName = new();
    
    public FilterItem()
    {
        ParentSheetIndex = 0;
    }
    
    public FilterItem(string itemid)
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
        return new FilterItem();
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
                Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 - 32) + shake),
                itemData.GetSourceRect(sourceOffset), Color.White * alpha, 0f, new Vector2(8f, 8f), scale.Y > 1f ? getScale().Y : 4f, spriteEffects,
                (isPassable() ? bounds.Top : bounds.Center.Y + 1) / 10000f);
            
            
            if (heldObject.Value == null)
            {
                return;
            }
            
            float base_sort = (y + 1) * 64 / 10000f + tileLocation.X / 50000f;
            float yOffset = 0f;
            float xOffset = 32f;
            
            // Draw item
            ParsedItemData heldItemData = ItemRegistry.GetDataOrErrorItem(heldObject.Value.QualifiedItemId);
            Texture2D texture = heldItemData.GetTexture();
            Rectangle? sourceRect = heldItemData.GetSourceRect();
            float heldScale = 2f;

            if (heldObject.Value.bigCraftable.Value)
            {
                heldScale *= .8f;
                yOffset -= 20f;
            }
            
            // Random extra draw support for Bigger Machines
            if (ModEntry.BMApi != null && ModEntry.BMApi.GetBiggerMachineTextureSourceRect(heldObject.Value, out var bmSourceRect))
            {
                if (Math.Abs(bmSourceRect.Width / 16f - 1) < 0.01f)
                {
                    heldScale /= bmSourceRect.Height / 16f;
                }
                if (Math.Abs(bmSourceRect.Height / 16f - 2) < 0.04f)
                {
                    yOffset += 32;
                }
                sourceRect = bmSourceRect;
                heldScale /= bmSourceRect.Width / 16f;
                xOffset -= 8;
                yOffset += 8;
            }
            
            spriteBatch.Draw(texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + xOffset, y * 64 + 8 + yOffset)), sourceRect, Color.White, 0f,
                new Vector2(8f, 8f), heldScale, SpriteEffects.None, base_sort + 1E-05f);
        }
    }
    
    public override void drawInMenu(SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency, float layerDepth, StackDrawType drawStackNumber, Color color, bool drawShadow)
    {
        AdjustMenuDrawForRecipes(ref transparency, ref scaleSize);
        var itemData = ItemRegistry.GetDataOrErrorItem(QualifiedItemId);
        var drawnScale = scaleSize;
        if (drawnScale > 0.2f)
        {
            drawnScale /= 2f;
        }
        var sourceRect = itemData.GetSourceRect(0, ParentSheetIndex);
        spriteBatch.Draw(itemData.GetTexture(), location + new Vector2(32f, 32f), sourceRect, color * transparency, 0f, new Vector2(sourceRect.Width / 2, sourceRect.Height / 2), 4f * drawnScale,
            SpriteEffects.None, layerDepth);
        DrawMenuIcons(spriteBatch, location, scaleSize, transparency, layerDepth, drawStackNumber, color);
    }
    
    public override void drawWhenHeld(SpriteBatch spriteBatch, Vector2 objectPosition, Farmer f)
    {
        base.drawWhenHeld(spriteBatch, new Vector2(objectPosition.X, objectPosition.Y - 64), f);
    }

    public override bool performObjectDropInAction(Item dropInItem, bool probe, Farmer who, bool returnFalseIfItemConsumed = false)
    {
        if (isTemporarilyInvisible || heldObject.Value != null || dropInItem is not Object || who.ActiveItem is not Object || autoLoadFrom != null || who.ActiveItem is FilterItem)
        {
            return false;
        }

        if (probe)
        {
            return true;
        }

        heldObject.Value = (Object)who.ActiveItem.getOne();
        who.reduceActiveItemByOne();
        Game1.playSound("stoneStep");
        return returnFalseIfItemConsumed;
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
}
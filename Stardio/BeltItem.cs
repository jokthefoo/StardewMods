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

[XmlType("Mods_Jok_BeltItem")]
public class BeltItem : IBeltPushing
{
    [XmlIgnore]
    public static int BeltAnim = 0;
    [XmlIgnore]
    public static int beltUpdateCountdown = 0;
    
    [XmlElement("spriveCurveOffset")]
    public readonly NetInt beltSpriteRotationOffset = new();
    
    [XmlElement("buildingSprite")]
    public readonly NetBool beltBuildingSprite = new();
    public override string DisplayName => GetDisplayName();
    public string Description { get; set; }
    public override string TypeDefinitionId => "(Jok.Belt)";
    
    public readonly NetString objName = new();
    public BeltItem()
    {
        NetFields.AddField(objName).AddField(currentRotation);
        ParentSheetIndex = 0;
    }
    
    public BeltItem(string itemid)
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

    public bool ValidPushFrom(int otherRotation)
    {
        if (Math.Abs(otherRotation - currentRotation.Value) == 2)
        {
            return false;
        }
        return true;
    }

    public void rotate(bool ghostRotate = false)
    {
        currentRotation.Value += 1;
        currentRotation.Value %= 4;

        if (!ghostRotate)
        {
            CheckForCurve();
            UpdateNeighborCurves();
        }
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
        return new BeltItem();
    }

    protected override void GetOneCopyFrom(Item source)
    {
        base.GetOneCopyFrom(source);
        ItemId = source.ItemId;
        if (source is BeltItem beltFrom)
        {
            currentRotation.Value = beltFrom.currentRotation.Value;
        }
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

    public void beltUpdate(bool isProcessTick)
    {
        if (Location == null)
        {
            return;
        }
        
        if (heldObject.Value != null && HeldItemPosition < 1.0f)
        {
            HeldItemPosition += 1.0f / Math.Clamp(ModEntry.Config.BeltUpdateMS, 10, ModEntry.Config.BeltUpdateMS);
            readyForHarvest.Value = true;
        }
        HeldItemPosition = Math.Clamp(HeldItemPosition, 0.0f, 1.0f);
        
        if (isProcessTick)
        {
            PushItem(Direction.Forward);
            BeltPullItem();
        }
    }

    private void BeltPullItem()
    {
        if (heldObject.Value != null || beltSpriteRotationOffset.Value != 0)
        {
            return;
        }

        var targetTile = getTileInDirection(Direction.Behind);
        // Try Grab item with BM first
        if(ModEntry.BMApi != null && ModEntry.BMApi.TryGetObjectAt(Location, targetTile, out var inputObj))
        {
            TryPullFromChestOrMachine(inputObj);
            return;
        }
        
        // Grab item
        if (Location.objects.TryGetValue(targetTile, out inputObj))
        {
            TryPullFromChestOrMachine(inputObj);
            return;
        }
        
        // Try Grab item with FM
        if (ModEntry.FMApi != null)
        {
            Furniture? fm = Location.GetFurnitureAt(targetTile);
            if (fm != null && ModEntry.FMApi.IsFurnitureMachine(fm))
            {
                inputObj = fm;
                TryPullFromMachine(inputObj);
            }
        }

        if (TryPullFromFishPond(targetTile))
        {
            return;
        }
        TryPullFromBuilding(targetTile);
    }

    private void TryPullFromChestOrMachine(Object inputObj)
    {
        if (inputObj.QualifiedItemId == "(BC)165" || ModEntry.IsObjectDroneHub(inputObj)) // auto grabber and drone hubs
        {
            if (inputObj.heldObject.Value is Chest)
            {
                inputObj = inputObj.heldObject.Value;
            }
            else
            {
                return;
            }
        }
            
        if (TryPullFromChest(inputObj))
        {
            return;
        }
        TryPullFromMachine(inputObj);
    }
    
    private void TryPullFromBuilding(Vector2 targetTile)
    {
        var building = Location.getBuildingAt(targetTile);
        if (building != null && building.GetIndoors() != null)
        {
            foreach (var obj in building.GetIndoors().objects.Values)
            {
                if (obj.QualifiedItemId == ModEntry.OUTPUT_CHEST_QID && obj.heldObject.Value is Chest chest)
                {
                    if (TryPullFromChest(chest))
                    {
                        return;
                    }
                }
            }
        }
    }
    
    private bool TryPullFromFishPond(Vector2 targetTile)
    {
        var building = Location.getBuildingAt(targetTile);
        if (building is FishPond pond && pond.output.Value is Object outputObj)
        {
            heldObject.Value = (Object)outputObj.getOne();
            pond.output.Value.Stack -= 1;

            if (pond.output.Value.Stack != 0)
            {
                return true;
            }
            
            pond.output.Value = null;
            Game1.playSound("coin");
            int bonusExperience = (int)(heldObject.Value.sellToStorePrice() * FishPond.HARVEST_OUTPUT_EXP_MULTIPLIER);
            Game1.MasterPlayer.gainExperience(1, bonusExperience + FishPond.HARVEST_BASE_EXP);
            return true;
        }
        return false;
    }
    
    private void TryPullFromMachine(Object inputObj)
    {
        if (inputObj == null || !inputObj.readyForHarvest.Value || inputObj is BeltItem || inputObj is SplitterItem)
        {
            return;
        }

        //Object.CheckForActionOnMachine
        MachineData machineData = inputObj.GetMachineData();
        Object output = inputObj.heldObject.Value;

        if (inputObj.lastOutputRuleId.Value != null && machineData != null)
        {
            MachineOutputRule outputRule = machineData.OutputRules?.FirstOrDefault(p => p.Id == inputObj.lastOutputRuleId.Value);

            if (outputRule != null && outputRule.RecalculateOnCollect)
            {
                inputObj.heldObject.Value = null;
                inputObj.OutputMachine(machineData, outputRule, inputObj.lastInputItem.Value, null, inputObj.Location, probe: false);

                if (inputObj.heldObject.Value != null)
                {
                    output = inputObj.heldObject.Value;
                }
                else
                {
                    inputObj.heldObject.Value = output;
                }
            }
        }

        // If emc is installed check for extra outputs
        if (ModEntry.EMCApi != null)
        {
            if (inputObj.heldObject.Value.heldObject.Value is Chest chest && chest.Items.Count > 0)
            {
                foreach (var item in chest.Items)
                {
                    if (item is not null && item is Object)
                    {
                        heldObject.Value = (Object)item.getOne();
                        item.Stack -= 1;
                        
                        if (item.Stack == 0)
                        {
                            chest.Items.Remove(item);
                            return;
                        }
                        
                        if (chest.Items.Count == 0)
                        {
                            inputObj.heldObject.Value.heldObject.Value = null;
                        }
                        return;
                    }
                }
            }
        }

        if (output is not Object)
        {
            return;
        }

        heldObject.Value = (Object)output.getOne();
        output.Stack -= 1;

        if (output.Stack != 0)
        {
            return;
        }
        
        OnMachineEmptied(inputObj, machineData, output);
    }

    private void OnMachineEmptied(Object inputObj, MachineData machineData, Object output)
    {
        MachineDataUtility.UpdateStats(machineData?.StatsToIncrementWhenHarvested, output, output.Stack);
        inputObj.heldObject.Value = null;
        inputObj.readyForHarvest.Value = false;
        inputObj.showNextIndex.Value = false;
        inputObj.ResetParentSheetIndex();

        if (MachineDataUtility.TryGetMachineOutputRule(inputObj, machineData, MachineOutputTrigger.OutputCollected, output.getOne(), null, inputObj.Location, out var outputCollectedRule, out var _,
                out var _, out var _))
        {
            inputObj.OutputMachine(machineData, outputCollectedRule, inputObj.lastInputItem.Value, null, inputObj.Location, probe: false);
        }

        if (inputObj.IsTapper() && inputObj.Location.terrainFeatures.TryGetValue(inputObj.TileLocation, out var terrainFeature) && terrainFeature is Tree tree)
        {
            tree.UpdateTapperProduct(inputObj, output);
        }

        if (machineData != null && machineData.ExperienceGainOnHarvest != null)
        {
            string[] expSplit = machineData.ExperienceGainOnHarvest.Split(' ');

            for (int i = 0; i < expSplit.Length; i += 2)
            {
                int skill = Farmer.getSkillNumberFromName(expSplit[i]);

                if (skill != -1 && ArgUtility.TryGetInt(expSplit, i + 1, out var amount, out var _, "int amount"))
                {
                    Game1.player.gainExperience(skill, amount);
                }
            }
        }
    }

    private bool TryPullFromChest(Object inputObj)
    {
        if (inputObj is not Chest inputChest || inputChest.GetMutex().IsLocked())
        {
            return false;
        }
        
        var items = inputChest.GetItemsForPlayer();

        for (int i = items.Count - 1; i >= 0; --i)
        {
            if (!(items[i] is Object obj))
            {
                continue;
            }

            heldObject.Value = (Object)obj.getOne();
            obj.Stack -= 1;

            if (obj.Stack == 0)
            {
                items.Remove(obj);
            }

            inputChest.clearNulls();
            return true;
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

            /* circle shadow
            if (fragility.Value != 2)
            {
                spriteBatch.Draw(Game1.shadowTexture, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 51 + 4)), Game1.shadowTexture.Bounds, Color.White * alpha, 0f,
                    new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y), 4f, SpriteEffects.None, bounds.Bottom / 15000f);
            }*/
            
            var shake = shakeTimer > 0 ? new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2)) : Vector2.Zero;
            var itemData = ItemRegistry.GetDataOrErrorItem(QualifiedItemId);

            var spriteEffects = SpriteEffects.None;
            int sourceOffset = 0;
            int curveSpriteOffset = 0;
            switch (currentRotation.Value)
            {
                case 1: // right
                    sourceOffset = 4;
                    curveSpriteOffset = 4;
                    if (beltSpriteRotationOffset.Value == -1) // left turn
                    {
                        curveSpriteOffset += 8;
                        spriteEffects = SpriteEffects.FlipHorizontally;
                    }
                    break;
                case 2: // down
                    spriteEffects = SpriteEffects.FlipVertically;
                    if (beltSpriteRotationOffset.Value == -1) // left turn
                    {
                        curveSpriteOffset = 20;
                        spriteEffects = SpriteEffects.FlipVertically;
                    }
                    if (beltSpriteRotationOffset.Value == 1) // right turn
                    {
                        curveSpriteOffset = 12;
                        spriteEffects = SpriteEffects.None;
                    }
                    break;
                case 3: // left
                    sourceOffset = 4;
                    spriteEffects = SpriteEffects.FlipHorizontally;
                    curveSpriteOffset = 4;
                    if (beltSpriteRotationOffset.Value == 1) // right turn
                    {
                        curveSpriteOffset += 8;
                        spriteEffects = SpriteEffects.None;
                    }
                    break;
                case 0: // up
                    sourceOffset = 0;
                    if (beltSpriteRotationOffset.Value == -1) // left turn
                    {
                        curveSpriteOffset = 12;
                        spriteEffects = SpriteEffects.FlipVertically;
                    }
                    if (beltSpriteRotationOffset.Value == 1) // right turn
                    {
                        curveSpriteOffset = 20;
                        spriteEffects = SpriteEffects.None;
                    }
                    break;
            }

            if (beltSpriteRotationOffset.Value == 0)
            {
                //no curve
                curveSpriteOffset = 0;
            }

            if (beltBuildingSprite.Value && ModEntry.Config.DroneHub)
            {
                float yOffset2 = 4f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2); // makes it bob
                spriteBatch.Draw(ModEntry.dronesTexture, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 - 32 + yOffset2) + shake),
                    new Rectangle(BeltAnim * 16, 0, 16, 32), Color.White * alpha, 0f, new Vector2(8f, 8f), scale.Y > 1f ? getScale().Y : 4f, SpriteEffects.None,
                    (isPassable() ? bounds.Top - 100 : bounds.Center.Y + 2) / 10000f);
                spriteBatch.Draw(ModEntry.dronepadTexture, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 - 32) + shake),
                    new Rectangle(BeltAnim * 16, 0, 16, 32), Color.White * alpha, 0f, new Vector2(8f, 8f), scale.Y > 1f ? getScale().Y : 4f, SpriteEffects.None,
                    (isPassable() ? bounds.Top - 100 : bounds.Center.Y + 1) / 10000f);
            }
            
            spriteBatch.Draw(itemData.GetTexture(),
                    Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 32) + shake),
                    itemData.GetSourceRect(sourceOffset + curveSpriteOffset, BeltAnim), Color.White * alpha, 0f, new Vector2(8f, 8f), scale.Y > 1f ? getScale().Y : 4f, spriteEffects,
                    (isPassable() ? bounds.Top - 100 : bounds.Center.Y) / 10000f);
            
            
            
            if (heldObject.Value == null)
            {
                return;
            }
            
            float base_sort = y * 64 / 10000f + tileLocation.X / 50000f;
            float yOffset = 4f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2); // makes it bob
            float xOffset = 0f;

            if (heldObject.Value.bigCraftable.Value)
            {
                yOffset -= 48;
            }

            switch (currentRotation.Value)
            {
                case 0: // up
                    if (HeldItemPosition < .5f && beltSpriteRotationOffset.Value == 1) // right turn -- we need yoffset to pretend we are going up until > .5 then go right
                    {
                        xOffset -= 64 * HeldItemPosition - 64;
                    } 
                    else if (HeldItemPosition < .5f && beltSpriteRotationOffset.Value == -1) // left turn -- we need yoffset to pretend we are going down until > .5 then go right
                    {
                        xOffset += 64 * HeldItemPosition;
                    }
                    else if (beltSpriteRotationOffset.Value != 0)
                    {
                        yOffset -= 48 * HeldItemPosition - 24;
                        xOffset += 32;
                    }
                    else
                    {
                        yOffset -= 64 * HeldItemPosition - 40;
                        xOffset += 32;
                    }
                    break;
                case 1: // right
                    if (HeldItemPosition < .5f && beltSpriteRotationOffset.Value == 1) // right turn -- we need yoffset to pretend we are going up until > .5 then go right
                    {
                        yOffset -= 80 * HeldItemPosition - 40;
                        xOffset += 32;
                    } 
                    else if (HeldItemPosition < .5f && beltSpriteRotationOffset.Value == -1) // left turn -- we need yoffset to pretend we are going down until > .5 then go right
                    {
                        yOffset += 64 * HeldItemPosition - 32;
                        xOffset += 32;
                    }
                    else
                    {
                        xOffset += 64 * HeldItemPosition;
                    }
                    break;
                case 2: // down
                    if (HeldItemPosition < .5f && beltSpriteRotationOffset.Value == 1) // right turn -- we need xoffset to pretend we are going to the right until > .5 then go down
                    {
                        xOffset += 64 * HeldItemPosition;
                    } 
                    else if (HeldItemPosition < .5f && beltSpriteRotationOffset.Value == -1) // left turn -- we need xoffset to pretend we are going to the left until > .5 then go down
                    {
                        xOffset -= 64 * HeldItemPosition - 64;
                    }
                    else
                    {
                        yOffset += 64 * HeldItemPosition - 32;
                        xOffset += 32;
                    }
                    break;
                case 3: // left
                    if (HeldItemPosition < .5f && beltSpriteRotationOffset.Value == 1) // right turn -- we need yoffset to pretend we are going down until > .5 then go left
                    {
                        yOffset += 64 * HeldItemPosition - 32;
                        xOffset += 32;
                    } 
                    else if (HeldItemPosition < .5f && beltSpriteRotationOffset.Value == -1) // left turn -- we need yoffset to pretend we are going up until > .5 then go left
                    {
                        yOffset -= 80 * HeldItemPosition - 40;
                        xOffset += 32;
                    }
                    else
                    {
                        xOffset -= 64 * HeldItemPosition - 64;
                    }
                    break;
            }
            
            if (heldObject.Value is ColoredObject coloredObj)
            {
                coloredObj.drawInMenu(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + xOffset - 32, y * 64 - 16 + yOffset)), 1f, 1f, base_sort + 1.1E-05f);
                return;
            }

            // Draw item
            ParsedItemData heldItemData = ItemRegistry.GetDataOrErrorItem(heldObject.Value.QualifiedItemId);
            Texture2D texture = heldItemData.GetTexture();
            Rectangle? sourceRect = heldItemData.GetSourceRect();
            float heldScale = 4f;
            
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
                xOffset -= 16;
                yOffset += 16;
            }
            
            spriteBatch.Draw(texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + xOffset, y * 64 + 8 + yOffset)), sourceRect, Color.White, 0f,
                new Vector2(8f, 8f), heldScale, SpriteEffects.None, base_sort + 1E-05f);

            StackDrawType drawType = StackDrawType.Hide;
            /* if (heldObject.Value.Stack > 1) // Draw stack count
            {
                drawType = StackDrawType.Draw;
            }*/
            if (heldObject.Value.Quality > 0 && ModEntry.Config.ShowQualityOnBelts) // Draw quality
            {
                drawType = StackDrawType.HideButShowQuality;
            }
            heldObject.Value.DrawMenuIcons(spriteBatch, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + xOffset - 32, y * 64 - 20 + yOffset)), 1f, 1f, base_sort + 1.2E-05f,
                drawType, Color.White);
        }
    }

    public override bool isPassable()
    {
        if (ModEntry.Config.DroneHub)
        {
            return !beltBuildingSprite.Value;
        }
        return true;
    }

    public override bool placementAction(GameLocation location, int x, int y, Farmer who = null)
    {
        bool wasPlaced = base.placementAction(location, x, y, who);
        if (wasPlaced)
        {
            if (location.objects.TryGetValue(getTileInDirection(Direction.Forward), out Object obj) && obj is BeltItem otherBelt)
            {
                otherBelt.CheckForCurve();
            }
        }
        return wasPlaced;
    }

    private void UpdateNeighborCurves()
    {
        UpdateNeighborCurves(TileLocation);
    }
    
    public void UpdateNeighborCurves(Vector2 tileLoc)
    {
        if (Location.objects.TryGetValue(getTileInDirection(Direction.Forward, tileLoc), out Object obj1) && obj1 is BeltItem forwardBelt)
        {
            forwardBelt.CheckForCurve();
        }
        if (Location.objects.TryGetValue(getTileInDirection(Direction.Right, tileLoc), out Object obj2) && obj2 is BeltItem rightBelt)
        {
            rightBelt.CheckForCurve();
        }
        if (Location.objects.TryGetValue(getTileInDirection(Direction.Left, tileLoc), out Object obj3) && obj3 is BeltItem leftBelt)
        {
            leftBelt.CheckForCurve();
        }
        if (Location.objects.TryGetValue(getTileInDirection(Direction.Behind, tileLoc), out Object obj4) && obj4 is BeltItem backBelt)
        {
            backBelt.CheckForCurve();
        }
    }

    public void CheckForCurve()
    {
        // If belt behind then default
        beltBuildingSprite.Value = false;
        
        var buildingBehind = Location.getBuildingAt(getTileInDirection(Direction.Behind));
        var buildingForward = Location.getBuildingAt(getTileInDirection(Direction.Forward));
                
        if (buildingBehind != null && buildingBehind.modData.ContainsKey(ModEntry.BUILDING_CHEST_KEY) || buildingForward != null && buildingForward.modData.ContainsKey(ModEntry.BUILDING_CHEST_KEY))
        {
            beltBuildingSprite.Value = true;
        }
        
        if (Location.objects.TryGetValue(getTileInDirection(Direction.Behind), out Object backObj))
        {
            if ((backObj is BeltItem backBelt && IsOtherBeltFacingMe(backBelt)) || backObj is SplitterItem || backObj is BridgeItem)
            {
                beltSpriteRotationOffset.Value = 0;
                return;
            }
        }
        
        bool foundLeft = Location.objects.TryGetValue(getTileInDirection(Direction.Left), out Object leftObj) && ((leftObj is BeltItem leftBelt && IsOtherBeltFacingMe(leftBelt)) || leftObj is SplitterItem || leftObj is BridgeItem);
        bool foundRight = Location.objects.TryGetValue(getTileInDirection(Direction.Right), out Object rightObj) && ((rightObj is BeltItem rightBelt && IsOtherBeltFacingMe(rightBelt)) || rightObj is SplitterItem || rightObj is BridgeItem);

        // if belts on both sides then default
        if (foundLeft && foundRight)
        {
            beltSpriteRotationOffset.Value = 0;
        } 
        else if (foundRight)
        {
            beltSpriteRotationOffset.Value = 1;
            beltBuildingSprite.Value = false;
        } 
        else if (foundLeft)
        {
            beltSpriteRotationOffset.Value = -1;
            beltBuildingSprite.Value = false;
        }
        else
        {
            beltSpriteRotationOffset.Value = 0;
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
    
    private bool IsOtherBeltFacingMe(BeltItem otherBelt)
    {
        return TileLocation == otherBelt.getTileInDirection(Direction.Forward);
    }
}
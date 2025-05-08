using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Inventories;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using xTile.Tiles;
using Object = StardewValley.Object;

namespace Stardio;

[XmlType("Mods_Jok_BeltItem")]
public class BeltItem : StardewValley.Object
{
    [XmlIgnore]
    public static int BeltAnim = 0;
    [XmlIgnore]
    public static int beltUpdateCountdown = 0;
    
    [XmlIgnore] private static Dictionary<int, Vector2> rotationDict = new()
    {
        { 0, new Vector2(0, -1) },
        { 1, new Vector2(1, 0) },
        { 2, new Vector2(0, 1) },
        { 3, new Vector2(-1, 0) }
    };

    [XmlIgnore] public float HeldItemPosition = 0;
    
    [XmlElement("currentRotation")]
    public readonly NetInt currentRotation = new();
	
    [XmlIgnore]
    public static IInventory tempBeltInventory;
    
    public enum Direction
    {
        Forward = 0,
        Left = 3,
        Right = 1,
        Behind = 2
    };

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

    public void rotate()
    {
        currentRotation.Value += 1;
        currentRotation.Value %= 4;
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
        catch (Exception e)
        {
            return "Error Item";
        }
    }
    
    protected override Item GetOneNew() // needed for right clicking item
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

    public override void updateWhenCurrentLocation(GameTime time)
    {
        base.updateWhenCurrentLocation(time);
    }

    public void beltUpdate(bool isProcessTick)
    {
        if (Location == null)
        {
            return;
        }
        
        if (heldObject.Value != null)
        {
            HeldItemPosition += 1.0f / Math.Clamp(ModEntry.Config.BeltUpdateMS, 10, ModEntry.Config.BeltUpdateMS);
            readyForHarvest.Value = true;
        }
        HeldItemPosition = Math.Clamp(HeldItemPosition, 0.0f, 1.0f);
        
        if (isProcessTick)
        {
            BeltPushItem();

            BeltPullItem();
        }
    }

    private void BeltPushItem()
    {
        // Push item
        Location.objects.TryGetValue(getTileInDirection(Direction.Forward), out var outputTarget);
        if (outputTarget == null)
        {
            return;
        }
        
        if (heldObject.Value != null && HeldItemPosition >= 1.0f)
        {
            tempBeltInventory = new Inventory();
            tempBeltInventory.Add(heldObject.Value);

            // try to add to chest
            if (TryPushToChest(outputTarget))
            {
                return;
            }

            // try to push to belt
            if (TryPushToBelt(outputTarget) || outputTarget is BeltItem)
            {
                return;
            }

            // try to load single item
            if (outputTarget.AttemptAutoLoad(tempBeltInventory, Game1.player))
            {
                heldObject.Value = null;
                HeldItemPosition = 0;
                return;
            }

            TryPushToMultiInputMachine(outputTarget);
            return;
        }
        
        var state = MachineStateManager.GetState(outputTarget.Location, outputTarget.TileLocation);
        // try to load machine's current inventory
        if (ModMachineState.IsValid(state))
        {
            tempBeltInventory = new Inventory();
            tempBeltInventory.AddRange(state.currentInventory);
            if (outputTarget.AttemptAutoLoad(tempBeltInventory, Game1.player))
            {
                // inventory should be emptied by auto-load
                state.outputRule = null;
                state.outputTrigger = null;
            }
        }

    }

    private void TryPushToMultiInputMachine(Object outputTarget)
    {
        MachineData machineData = outputTarget.GetMachineData();
        if (machineData == null)
        {
            return;
        }

        var state = MachineStateManager.GetState(outputTarget.Location, outputTarget.TileLocation);
        if (!ModMachineState.IsValid(state))
        {
            // Try to start new rule
            if (!MachineDataUtility.TryGetMachineOutputRule(this, machineData, MachineOutputTrigger.ItemPlacedInMachine, heldObject.Value, Game1.player, Location, out var outputRule,
                    out var triggerRule, out var outputRuleIgnoringCount, out var triggerIgnoringCount))
            {
                if (outputRuleIgnoringCount != null)
                {
                    // start new rule
                    MachineStateManager.CreateState(Location, outputTarget.TileLocation, outputRuleIgnoringCount, triggerIgnoringCount, heldObject.Value);
                    heldObject.Value = null;
                    HeldItemPosition = 0;
                }
            }
            return;
        }

        tempBeltInventory = new Inventory();
        tempBeltInventory.AddRange(state.currentInventory);
        
        // If we have started a rule, check if we are trying to load additional missing requirements (aka coal)
        if (ModMachineState.IsValid(state) && !MachineDataUtility.HasAdditionalRequirements(tempBeltInventory, machineData.AdditionalConsumedItems, out var failedRequirement))
        {
            // TODO extra machine configs?
            foreach (MachineItemAdditionalConsumedItems requirement in machineData.AdditionalConsumedItems)
            {
                if (ItemRegistry.QualifyItemId(requirement.ItemId) == heldObject.Value.QualifiedItemId && state.CountItemId(requirement.ItemId) < requirement.RequiredCount)
                {
                    state.AddObject(heldObject.Value);

                    // Check if we now have enough
                    if (outputTarget.AttemptAutoLoad(tempBeltInventory, Game1.player))
                    {
                        // inventory should be emptied by auto-load
                        state.outputRule = null;
                        state.outputTrigger = null;
                    }

                    heldObject.Value = null;
                    HeldItemPosition = 0;
                    return;
                }
            }
        }

        // auto load failed
        // If we have started with a rule continue with that rule
        if (ModMachineState.IsValid(state))
        {
            if (MachineDataUtility.CanApplyOutput(outputTarget, state.outputRule, MachineOutputTrigger.ItemPlacedInMachine, heldObject.Value, Game1.player, Location, out var triggerRule,
                    out var matchesExceptCount))
            {
                // we shouldn't get in here, this would mean we can load item but failed in earlier conditions that should succeed
                ModEntry.MonitorInst.Log($"Stardio failed to correctly load a machine.", LogLevel.Error);
                return;
            }
            
            if (matchesExceptCount && triggerRule.Id == state.outputTrigger.Id && state.CountItemId(heldObject.Value.ItemId) < triggerRule.RequiredCount)
            {
                // add item if matching
                state.AddObject(heldObject.Value);

                // Check if we now have enough
                if (outputTarget.AttemptAutoLoad(tempBeltInventory, Game1.player))
                {
                    // inventory should be emptied by auto-load
                    state.outputRule = null;
                    state.outputTrigger = null;
                }

                heldObject.Value = null;
                HeldItemPosition = 0;
            }

            // if we don't add the item belt is blocked
        }
    }

    private bool TryPushToChest(Object outputTarget)
    {
        if (outputTarget is Chest outputChest)
        {
            if (outputChest.addItem(heldObject.Value) != null)
            {
                // Chest full so drop item
                Game1.createItemDebris(heldObject.Value, TileLocation * 64f, -1, Location);
            }

            outputChest.clearNulls();
            heldObject.Value = null;
            HeldItemPosition = 0;
            return true;
        }
        return false;
    }
    
    private bool TryPushToBelt(Object outputTarget)
    {
        if (outputTarget is BeltItem belt && belt.heldObject.Value == null && belt.ValidPushFrom(currentRotation.Value))
        {
            belt.heldObject.Value = heldObject.Value;

            if (belt.currentRotation.Value != currentRotation.Value)
            {
                belt.HeldItemPosition = .3f;
            }

            heldObject.Value = null;
            HeldItemPosition = 0;
            return true;
        }
        return false;
    }

    private void BeltPullItem()
    {
        // Grab item
        if (heldObject.Value == null && Location.objects.TryGetValue(getTileInDirection(Direction.Behind), out var inputObj))
        {
            if (TryPullFromChest(inputObj))
            {
                return;
            }

            TryPullFromMachine(inputObj);
        }
    }

    private void TryPullFromMachine(Object inputObj)
    {
        if (!inputObj.readyForHarvest.Value || inputObj is BeltItem)
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
        if (inputObj is not Chest inputChest)
        {
            return false;
        }
        
        var items = inputChest.GetItemsForPlayer();

        foreach (var item in items)
        {
            if (!(item is Object obj))
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
            switch (currentRotation.Value)
            {
                case 1: // right
                    sourceOffset = 8; // TODO 4 -- for sprite testing
                    break;
                case 2: // down
                    sourceOffset = 12; // TODO delete  -- for sprite testing
                    spriteEffects = SpriteEffects.FlipVertically;
                    break;
                case 3: // left
                    sourceOffset = 4;
                    spriteEffects = SpriteEffects.FlipHorizontally;
                    break;
            }
            
            spriteBatch.Draw(itemData.GetTexture(),
                Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32, y * 64 + 32) + shake),
                itemData.GetSourceRect(sourceOffset, BeltAnim), Color.White * alpha, 0f, new Vector2(8f, 8f), scale.Y > 1f ? getScale().Y : 4f, spriteEffects,
                (isPassable() ? bounds.Top : bounds.Center.Y) / 10000f);
            
            
            if (heldObject.Value == null)
            {
                return;
            }
            
            float base_sort = (y + 1) * 64 / 10000f + tileLocation.X / 50000f;
            float yOffset = 4f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2); // makes it bob
            float xOffset = 0f;

            if (heldObject.Value.bigCraftable.Value)
            {
                yOffset -= 48;
            }
            
            switch (currentRotation.Value)
            {
                case 0: // up
                    yOffset -= 64 * HeldItemPosition - 32 - 8;
                    xOffset += 32;
                    break;
                case 1: // right
                    xOffset += 64 * HeldItemPosition;
                    break;
                case 2: // down
                    yOffset += 64 * HeldItemPosition - 32;
                    xOffset += 32;
                    break;
                case 3: // left
                    xOffset -= 64 * HeldItemPosition - 64;
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
            spriteBatch.Draw(texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + xOffset, y * 64 + 8 + yOffset)), heldItemData.GetSourceRect(), Color.White, 0f,
                new Vector2(8f, 8f), 4f, SpriteEffects.None, base_sort + 1E-05f);

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
        return true;
    }

    public override bool placementAction(GameLocation location, int x, int y, Farmer who = null)
    {
        bool wasPlaced = base.placementAction(location, x, y, who);

        if (wasPlaced)
        {
            //grab neighbors
        }

        return wasPlaced;
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

    private Vector2 getTileInDirection(Direction dir)
    {
        var rot = (currentRotation.Value + (int)dir) % 4;
        return TileLocation + rotationDict[rot];
    }

    ///// <summary>Update the object instance before it's placed in the world.</summary>
    /// <param name="who">The player placing the item.</param>
    /// <returns>Returns <c>true</c> if the item should be destroyed, or <c>false</c> if it should be set down.</returns>
    /// <remarks>This is called on the object instance being placed, after it's already been split from the inventory stack if applicable. At this point the <see cref="P:StardewValley.Object.Location" /> and <see cref="P:StardewValley.Object.TileLocation" /> values should already be set.</remarks>
    //public virtual bool performDropDownAction(Farmer who)

    //public virtual bool performUseAction(GameLocation location)
    
    /// <summary>Perform an action when the user interacts with this object.</summary>
    /// <param name="who">The player interacting with the object.</param>
    /// <param name="justCheckingForActivity">Whether to check if an action would be performed, without actually doing it. Setting this to true may have inconsistent effects depending on the action.</param>
    /// <returns>Returns true if the action was performed, or false if the player should pick up the item instead.</returns>
    //public virtual bool checkForAction(Farmer who, bool justCheckingForActivity = false)

    /// <summary>Check whether the object can be added to a location, and (sometimes) add it to the location.</summary>
    /// <param name="location">The location in which to place it.</param>
    /// <param name="x">The X tile position at which to place it.</param>
    /// <param name="y">The Y tile position at which to place it.</param>
    /// <param name="who">The player placing the object, if applicable.</param>
    /// <returns>Returns whether the object should be (or was) added to the location.</returns>
    /// <remarks>For legacy reasons, the behavior of this method is inconsistent. It'll sometimes add the object to the location itself, and sometimes expect the caller to do it.</remarks>
    //public virtual bool placementAction(GameLocation location, int x, int y, Farmer who = null)

}
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Inventories;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace Jok.Stardio;

public abstract class IBeltPushing : Object
{
    [XmlElement("currentRotation")]
    public readonly NetInt currentRotation = new();
    
    [XmlIgnore] public static Dictionary<int, Vector2> rotationDict = new()
    {
        { 0, new Vector2(0, -1) },
        { 1, new Vector2(1, 0) },
        { 2, new Vector2(0, 1) },
        { 3, new Vector2(-1, 0) }
    };
    
    public enum Direction
    {
        Forward = 0,
        Left = 3,
        Right = 1,
        Behind = 2
    };

    [XmlIgnore] public float HeldItemPosition { get; set; }
    [XmlIgnore] public IInventory tempBeltInventory = new Inventory();

    public Vector2 getTileInDirection(Direction dir, Vector2 tileLoc)
    {
        var rot = Math.Abs((currentRotation.Value + (int)dir) % 4);
        return tileLoc + rotationDict[rot];
    }
    
    public Vector2 getTileInDirection(Direction dir)
    {
        return getTileInDirection(dir, TileLocation);
    }
    
    public void PushItem(Direction dir)
    {
        if (heldObject.Value == null || HeldItemPosition < 1.0f)
        {
            return;
        }

        var targetTile = getTileInDirection(dir);
        tempBeltInventory.Clear();
        tempBeltInventory.Add(heldObject.Value);

        Object? outputTarget = getObjectAtTile(targetTile);
        
        // Try push item with FM
        if (ModEntry.FMApi != null && outputTarget == null)
        {
            Furniture? fm = Location.GetFurnitureAt(targetTile);
            if (fm != null && ModEntry.FMApi.IsFurnitureMachine(fm))
            {
                outputTarget = fm;
            }
        }

        if (outputTarget == null)
        {
            if (TryPushToShippingBin(targetTile))
            {
                return;
            }
            if (TryPushToJunimoHut(targetTile))
            {
                return;
            }
            if (TryPushToSilo(targetTile))
            {
                return;
            }
            if (TryPushToBuilding(targetTile))
            {
                return;
            }
            return;
        }

        // try to push to filter
        Object? newTarget = null;
        if (outputTarget is FilterItem)
        {
            if (TryPushToFilter(outputTarget, ref newTarget, ref dir))
            {
                outputTarget = newTarget;
            }
            
            if (outputTarget == null)
            {
                return;
            }
        }
        
        // try to push to bridge
        newTarget = null;
        if (outputTarget is BridgeItem)
        {
            if (TryPushToBridge(outputTarget, ref newTarget, dir))
            {
                outputTarget = newTarget;
            }
            
            if (outputTarget == null)
            {
                return;
            }
        }
            
        // try to add to chest
        if (TryPushToChest(outputTarget))
        {
            return;
        }
        
        // try to add to building output chest
        if (outputTarget.QualifiedItemId == ModEntry.OUTPUT_CHEST_QID && outputTarget.heldObject.Value is Chest chest)
        {
            TryPushToChest(chest);
            return;
        }

        // try to push to belt
        if (TryPushToBelt(outputTarget) || outputTarget is BeltItem)
        {
            return;
        }
        
        // try to push to splitter
        if (TryPushToSplitter(outputTarget) || outputTarget is SplitterItem)
        {
            return;
        }

        if (!ModEntry.Config.PushIntoMachines)
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

        var state = MachineStateManager.GetState(outputTarget.Location, outputTarget.TileLocation);
        // try to load machine's current inventory -- belt held item isn't used here
        if (ModMachineState.IsValid(state))
        {
            tempBeltInventory = new Inventory();
            tempBeltInventory.AddRange(state!.currentInventory);

            if (outputTarget.AttemptAutoLoad(tempBeltInventory, Game1.player))
            {
                // inventory should be emptied by autoload
                state.outputRule = null;
                state.outputTrigger = null;
            }
        }
    }

    private bool TryPushToBuilding(Vector2 targetTile)
    {
        var building = Location.getBuildingAt(targetTile);
        if (building != null && building.GetIndoors() != null)
        {
            foreach (var obj in building.GetIndoors().objects.Values)
            {
                if (obj.QualifiedItemId == ModEntry.INPUT_CHEST_QID && obj.heldObject.Value is Chest chest)
                {
                    if (TryPushToChest(chest))
                    {
                        return true;
                    }
                }
            }
            return true;
        }
        return false;
    }
    
    private bool TryPushToJunimoHut(Vector2 targetTile)
    {
        var building = Location.getBuildingAt(targetTile);
        if (building is JunimoHut hut)
        {
            return TryPushToChest(hut.GetOutputChest());
        }
        return false;
    }
    
    private bool TryPushToSilo(Vector2 targetTile)
    {
        var building = Location.getBuildingAt(targetTile);
        if (building != null && building.hayCapacity.Value > 0 && building.daysOfConstructionLeft.Value <= 0 && heldObject.Value.QualifiedItemId == "(O)178") // hay
        {
            building.GetParentLocation().GetRootLocation().tryToAddHay(1);
            heldObject.Value = null;
            HeldItemPosition = 0;
            return true;
        }
        return false;
    }
    
    private bool TryPushToShippingBin(Vector2 targetTile)
    {
        var building = Location.getBuildingAt(targetTile);
        if (building is ShippingBin && heldObject.Value.canBeShipped())
        {
            var farm = Game1.getFarm();
            farm.getShippingBin(Game1.MasterPlayer).Add(heldObject.Value);
            farm.lastItemShipped = heldObject.Value;
            farm.playSound("Ship");
            heldObject.Value = null;
            HeldItemPosition = 0;
            return true;
        }
        return false;
    }

    private void TryPushToMultiInputMachine(Object outputTarget)
    {
        MachineData machineData = outputTarget.GetMachineData();
        if (machineData == null || outputTarget.heldObject.Value != null)
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
            // We matched a rule that requires only one base item but has EMC additional requirements so we start the rule here
            else if (ModEntry.EMCApi != null) 
            {
                // start new rule
                MachineStateManager.CreateState(Location, outputTarget.TileLocation, outputRule, triggerRule, heldObject.Value);
                heldObject.Value = null;
                HeldItemPosition = 0;
            }
            return;
        }

        IInventory tempBeltInventory2 = new Inventory();
        tempBeltInventory2.AddRange(state!.currentInventory);
        
        // If we have started a rule, check if we are trying to load additional missing requirements (aka coal)
        if (ModMachineState.IsValid(state) && !MachineDataUtility.HasAdditionalRequirements(tempBeltInventory2, machineData.AdditionalConsumedItems, out var failedRequirement))
        {
            foreach (MachineItemAdditionalConsumedItems requirement in machineData.AdditionalConsumedItems)
            {
                if (ItemRegistry.QualifyItemId(requirement.ItemId) == heldObject.Value.QualifiedItemId && state.CountItemId(requirement.ItemId) < requirement.RequiredCount)
                {
                    AddItemToMachineStateThenCheck(tempBeltInventory2, state, outputTarget);
                    return;
                }
            }
        }

        // autoload failed
        // If we have started with a rule continue with that rule
        if (ModMachineState.IsValid(state))
        {
            if (MachineDataUtility.CanApplyOutput(outputTarget, state.outputRule, MachineOutputTrigger.ItemPlacedInMachine, heldObject.Value, Game1.player, Location, out var triggerRule,
                    out var matchesExceptCount))
            {
                // we shouldn't get in here, this would mean we can load item but failed in earlier conditions that should succeed
                // ModEntry.MonitorInst.Log($"Stardio failed to correctly load a machine.", LogLevel.Error);
                // pipes hits in here
                return;
            }
            
            if (matchesExceptCount && triggerRule.Id == state.outputTrigger!.Id && state.CountItemId(heldObject.Value.ItemId) < triggerRule.RequiredCount)
            {
                AddItemToMachineStateThenCheck(tempBeltInventory2, state, outputTarget);
                return;
            }

            // if we don't add the item belt is blocked
        }
        
        // If all else fails check for EMC requirements
        if (ModMachineState.IsValid(state) && ModEntry.EMCApi != null)
        {
            foreach ((string extraItemId, int extraCount) in ModEntry.EMCApi.GetExtraRequirements(state.outputRule!.OutputItem[0]))
            {
                if (CraftingRecipe.ItemMatchesForCrafting(heldObject.Value, extraItemId) && state.CountItemId(heldObject.Value.ItemId) < extraCount)
                {
                    AddItemToMachineStateThenCheck(tempBeltInventory2, state, outputTarget);
                    return;
                }
            }

            foreach ((string extraContextTags, int extraCount) in ModEntry.EMCApi.GetExtraTagsRequirements(state.outputRule.OutputItem[0]))
            {
                if (ItemContextTagManager.DoesTagQueryMatch(extraContextTags, heldObject.Value.GetContextTags() ?? new HashSet<string>()) && state.CountItemId(heldObject.Value.ItemId) < extraCount)
                {
                    AddItemToMachineStateThenCheck(tempBeltInventory2, state, outputTarget);
                    return;
                }
            }
        }
    }

    private void AddItemToMachineStateThenCheck(IInventory tempBeltInventory2, ModMachineState state, Object outputTarget)
    {
        // add item
        state.AddObject(heldObject.Value);

        // Check if we now have enough
        if (outputTarget.AttemptAutoLoad(tempBeltInventory2, Game1.player))
        {
            // inventory should be emptied by autoload
            state.outputRule = null;
            state.outputTrigger = null;
        }

        heldObject.Value = null;
        HeldItemPosition = 0;
    }

    private bool TryPushToChest(Object outputTarget)
    {
        if (outputTarget is Chest outputChest && !outputChest.GetMutex().IsLocked())
        {
            if (outputChest.addItem(heldObject.Value) != null)
            {
                // Chest full
                return true;
            }

            outputChest.clearNulls();
            heldObject.Value = null;
            HeldItemPosition = 0;
            return true;
        }
        return false;
    }
    
    private bool TryPushToBridge(Object outputTarget, ref Object? newTarget, Direction dir)
    {
        if (outputTarget is BridgeItem bridge)
        {
            var targetTile = getTileInDirection(dir, bridge.TileLocation);
            Object? outputTarget2 = getObjectAtTile(targetTile);
            
            if (outputTarget2 == null)
            {
                return false;
            }
            
            if (outputTarget2 is BridgeItem)
            {
                return TryPushToBridge(outputTarget2, ref newTarget, dir);
            }
            newTarget = outputTarget2;
            return true;
        }
        return false;
    }
    
    private bool TryPushToSplitter(Object outputTarget)
    {
        if (outputTarget is SplitterItem splitter && splitter.heldObject.Value == null)
        {
            splitter.heldObject.Value = heldObject.Value;

            heldObject.Value = null;
            HeldItemPosition = 0;
            return true;
        }
        return false;
    }
    
    protected virtual bool TryPushToBelt(Object outputTarget)
    {
        if (outputTarget is BeltItem belt && belt.heldObject.Value == null && belt.ValidPushFrom(currentRotation.Value))
        {
            belt.heldObject.Value = heldObject.Value;

            heldObject.Value = null;
            HeldItemPosition = 0;
            return true;
        }
        return false;
    }
    
    private bool TryPushToFilter(Object outputTarget, ref Object? newTarget, ref Direction dir)
    {
        if (outputTarget is FilterItem filter)
        {
            // if we match filter attempt to push forwards
            if (filter.heldObject.Value != null && filter.heldObject.Value.QualifiedItemId == heldObject.Value.QualifiedItemId)
            {
                var targetTile = getTileInDirection(dir, filter.TileLocation);
                Object? outputTarget2 = getObjectAtTile(targetTile);
            
                if (outputTarget2 == null)
                {
                    return false;
                }
            
                newTarget = outputTarget2;
                return true;
            }
            else //attempt to push l/r
            {
                int newDir = (int)dir + filter.pushDirection;
                if (newDir < 0)
                {
                    newDir = 4 + newDir;
                }
                
                var targetTile = getTileInDirection((Direction)newDir, filter.TileLocation);
                Object? outputTarget2 = getObjectAtTile(targetTile);
                
                if (outputTarget2 == null)
                {
                    newDir = (int)dir - filter.pushDirection;
                    var targetTile2 = getTileInDirection((Direction)newDir, filter.TileLocation);
                    outputTarget2 = getObjectAtTile(targetTile2);
    
                    if (outputTarget2 == null)
                    {
                        return false;
                    }
                }
                dir = (Direction)newDir;
                filter.pushDirection *= -1; // flip direction
                newTarget = outputTarget2;
                return true;
            }
        }
        
        return false;
    }

    private Object? getObjectAtTile(Vector2 tileLocation)
    {
        Object? outputTarget = null;
        // Try to get push target with BM first
        if (ModEntry.BMApi != null && ModEntry.BMApi.TryGetObjectAt(Location, tileLocation, out var bmObject))
        {
            outputTarget = bmObject;
        }
        if (outputTarget == null && Location.objects.TryGetValue(tileLocation, out var normalObj))
        {
            outputTarget = normalObj;
        }
        return outputTarget;
    }

    protected void UpdateNeighborCurves()
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
}
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using Object = StardewValley.Object;

namespace Stardio;

internal static class MachineStateManager
{
    public static Dictionary<string, Dictionary<Vector2, ModMachineState>?> MachineStates;

    public static ModMachineState? GetState(GameLocation location, Vector2 tile)
    {
        if (MachineStates.TryGetValue(location.Name, out var locStates))
        {
            if (locStates.TryGetValue(tile, out var state))
            {
                return state;
            }
        }
        return null;
    }
    
    public static void CreateState(GameLocation location, Vector2 tile, MachineOutputRule rule, MachineOutputTriggerRule trigger, Object obj)
    {
        var newState = new ModMachineState(rule, trigger, obj);
        if (MachineStates.TryGetValue(location.Name, out var locStates))
        {
            MachineStates[location.Name][tile] = newState;
        }
        else
        {
            MachineStates.Add(location.Name, new Dictionary<Vector2, ModMachineState>());
            MachineStates[location.Name][tile] = newState;
        }
    }
}

public class ModMachineStateSerialized
{
    public MachineOutputRule? outputRule;
    public MachineOutputTriggerRule? outputTrigger;
    public string currentInventory = "";
}

public class ItemListSerialized
{
    public List<Item> currentInventory;
}

public class ModMachineState
{
    public ModMachineState(MachineOutputRule? rule, MachineOutputTriggerRule? trigger)
    {
        outputRule = rule;
        outputTrigger = trigger;
    }
    
    public ModMachineState(MachineOutputRule? rule, MachineOutputTriggerRule? trigger, Object obj)
    {
        outputRule = rule;
        outputTrigger = trigger;
        AddObject(obj);
    }

    public void AddObject(Object obj)
    {
        obj.resetState();
        for (int i = 0; i < currentInventory.Count; i++)
        {
            if (currentInventory[i] != null && currentInventory[i].canStackWith(obj))
            {
                int toRemove = obj.Stack - currentInventory[i].addToStack(obj);
                if (obj.ConsumeStack(toRemove) == null)
                {
                    return;
                }
            }
        }
        currentInventory.Add(obj);
    }

    public static bool IsValid(ModMachineState? state)
    {
        if (state?.outputRule == null || state.outputTrigger == null)
        {
            return false;
        }
        return true;
    }
    
    public int CountItemId(string itemId)
    {
        itemId = ItemRegistry.QualifyItemId(itemId);
        if (itemId == null)
        {
            return 0;
        }

        foreach (var item in currentInventory)
        {
            if (item.QualifiedItemId == itemId)
            {
                return item.Stack;
            }
        }
        return 0;
    }

    public MachineOutputRule? outputRule;
    public MachineOutputTriggerRule? outputTrigger;
    public List<Item> currentInventory = new();
}
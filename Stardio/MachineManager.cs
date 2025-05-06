using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Inventories;
using Object = StardewValley.Object;

namespace Stardio;

internal static class MachineStateManager
{
    // TODO: Save/Load 
    public static Dictionary<GameLocation, Dictionary<Vector2, ModMachineState>?> MachineStates = new();

    public static ModMachineState GetState(GameLocation location, Vector2 tile)
    {
        if (MachineStates.TryGetValue(location, out var locStates))
        {
            if (locStates.TryGetValue(tile, out var state))
            {
                return state;
            }
        }
        else
        {
            MachineStates.Add(location, new Dictionary<Vector2, ModMachineState>());
        }

        var newState = new ModMachineState(null, null);
        MachineStates[location][tile] = newState;
        return newState;
    }
}

public class ModMachineState
{
    public ModMachineState(MachineOutputRule? rule, MachineOutputTriggerRule? trigger)
    {
        outputRule = rule;
        outputTrigger = trigger;
    }

    public void AddObject(Object obj)
    {
        obj.resetState();
        currentInventory.RemoveEmptySlots();
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

    public MachineOutputRule? outputRule;
    public MachineOutputTriggerRule? outputTrigger;
    public Inventory currentInventory = new Inventory();
}
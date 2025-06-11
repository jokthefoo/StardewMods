using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Internal;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace FluidPipes;

internal class FluidData
{
    internal static Regex RequirementTypeKeyRegex = new Regex(@$"Jok\.FluidPipes\.FluidType\.Input\.(\d+)");
    internal static string RequirementVolumeKeyPrefix = $"Jok.FluidPipes.FluidVolume.Input.";
    internal static Regex OutputTypeKeyRegex = new Regex(@$"Jok\.FluidPipes\.FluidType\.Output\.(\d+)");
    internal static string OutputVolumeKeyPrefix = $"Jok.FluidPipes.FluidVolume.Output.";
    
    internal static string BufferEndKey = $".JokFluidBuffer";
    
    internal static string LiquidKey = $"Jok.Liquid";
    
    public class FluidDataEntry {
        public FluidDataEntry(string type, int vol)
        {
            fluidType = type;
            volume = vol;
        }
        public string fluidType;
        public int volume;
    }

    internal struct AvailableFluid
    {
        public AvailableFluid(Object fluid, Chest container, Object machine)
        {
            fluidObject = fluid;
            fluidContainer = container;
            fluidMachine = machine;
        }
        public Object fluidObject;
        public Chest fluidContainer;
        public Object fluidMachine;
    }
    
    struct MachineKey : IEquatable<MachineKey>
    {
        public Vector2 Position;
        public string Location;

        public MachineKey(Vector2 position, string name)
        {
            Position = position;
            Location = name;
        }

        public override int GetHashCode() => HashCode.Combine(Position.X, Position.Y, Location);
        public override bool Equals(object obj) => obj is MachineKey other && Equals(other);
        public bool Equals(MachineKey other) =>
            Position.Equals(other.Position) && string.Equals(Location, other.Location);
    }
    private static readonly HashSet<MachineKey> _machinesRunning = new();
    /// <summary>Get the output item to produce.</summary>
    /// <param name="machine">The machine instance for which to produce output.</param>
    /// <param name="inputItem">The item being dropped into the machine, if applicable.</param>
    /// <param name="probe">Whether the machine is only checking whether the input is valid. If so, the input/machine shouldn't be changed and no animations/sounds should play.</param>
    /// <param name="outputData">The item output data from <c>Data/Machines</c> for which output is being created, if applicable.</param>
    /// <param name="overrideMinutesUntilReady">The in-game minutes until the item will be ready to collect, if set. This overrides the equivalent fields in the machine data if set.</param>
    /// <returns>Returns the item to produce, or <c>null</c> if none should be produced.</returns>
    public static Item? OutputFluid(Object machine, Item inputItem, bool probe, MachineItemOutput outputData, Farmer player, out int? overrideMinutesUntilReady)
    {
        overrideMinutesUntilReady = null;
        if (machine.heldObject.Value != null)
        {
            return null;
        }
        
        var itemInputs = GetFluidInfoFromDict(outputData.CustomData, RequirementTypeKeyRegex, RequirementVolumeKeyPrefix);
        var machineInputs = GetFluidInfoFromDict(machine.GetMachineData().CustomFields, RequirementTypeKeyRegex, RequirementVolumeKeyPrefix);
        var inputs = itemInputs.Concat(machineInputs).ToList();

        if (inputs.Count > 0)
        {
            // check inputs
            var visited = new HashSet<Vector2>();
            var availableFluids = new List<AvailableFluid>();
            GetAvailableFluids(machine.Location, ref visited, machine.TileLocation, ref availableFluids);
            List<FluidDataEntry> satisfyCheck = inputs.Select(x => new FluidDataEntry(x.fluidType, x.volume)).ToList();

            
            // Remove any buffered liquids
            List<FluidDataEntry> bufferedFluids = new List<FluidDataEntry>();
            foreach (var key in machine.modData.Keys)
            {
                if (key.EndsWith(BufferEndKey))
                {
                    bufferedFluids.Add(new FluidDataEntry(key.Substring(0, key.Length - BufferEndKey.Length), int.Parse(machine.modData[key])));
                }
            }

            foreach (var bf in bufferedFluids)
            {
                foreach (var reqInput in inputs)
                {
                    if (bf.fluidType == reqInput.fluidType && reqInput.volume > 0)
                    {
                        reqInput.volume -= bf.volume;
                        machine.modData.Remove(bf.fluidType + BufferEndKey);
                    }
                }
            }
            // End remove buffer liquids
            
            // Grab liquids from available machines
            foreach (var item in availableFluids)
            {
                foreach (var reqInput in inputs)
                {
                    if (item.fluidObject.ItemId == reqInput.fluidType && reqInput.volume > 0)
                    {
                        reqInput.volume -= item.fluidObject.Stack;
                        if (reqInput.volume < 0)
                        {
                            item.fluidObject.Stack = reqInput.volume * -1;
                        }
                        else
                        {
                            // We depleted source
                            item.fluidObject.Stack = 0;
                            if (item.fluidContainer is Chest chest2)
                            {
                                chest2.Items.Remove(item.fluidObject);
                                if (chest2.Items.Count == 0)
                                {
                                    var output = item.fluidObject;
                                    var fluidMachine = item.fluidMachine;
                                    MachineData machineData = fluidMachine.GetMachineData();
                                    MachineDataUtility.UpdateStats(machineData?.StatsToIncrementWhenHarvested, output, output.Stack);
                                    fluidMachine.heldObject.Value = null;
                                    fluidMachine.readyForHarvest.Value = false;
                                    fluidMachine.showNextIndex.Value = false;
                                    fluidMachine.ResetParentSheetIndex();

                                    if (MachineDataUtility.TryGetMachineOutputRule(fluidMachine, machineData, MachineOutputTrigger.OutputCollected, output.getOne(), null, fluidMachine.Location, out var outputCollectedRule, out var _,
                                            out var _, out var _))
                                    {
                                        fluidMachine.OutputMachine(machineData, outputCollectedRule, fluidMachine.lastInputItem.Value, null, fluidMachine.Location, probe: false);
                                    }
                                } else if (chest2.Items.Count == 1)
                                {
                                    var lastItem = chest2.Items[0];
                                    if (lastItem != null)
                                    {
                                        if (lastItem.modData == null || !lastItem.modData.ContainsKey(FluidData.LiquidKey))
                                        {
                                            chest2.Items.Remove(lastItem);
                                            item.fluidMachine.heldObject.Value = (Object)lastItem;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Check if we satisfied all liquid conditions, if not we buffer the ones we consumed
            bool needsMoreLiquid = false;
            foreach (var leftOverReq in inputs)
            {
                if (leftOverReq.volume > 0)
                {
                    int startVol = 0;
                    foreach (var startingReq in satisfyCheck)
                    {
                        if (startingReq.fluidType == leftOverReq.fluidType)
                        {
                            startVol = startingReq.volume;
                            break;
                        }
                    }
                    machine.modData[leftOverReq.fluidType + BufferEndKey] = (startVol - leftOverReq.volume).ToString();
                    needsMoreLiquid = true;
                }
            }

            if (needsMoreLiquid)
            {
                DelayedAction.functionAfterDelay(delegate
                {
                    var key = new MachineKey(machine.TileLocation, machine.Location.NameOrUniqueName);
                    if (!_machinesRunning.Add(key))
                        return;

                    try
                    {
                        var who = Game1.GetPlayer(machine.owner.Value) ?? Game1.player;
                        GameLocation location = machine.Location;
                        MachineData machineData = machine.GetMachineData();

                        if (MachineDataUtility.TryGetMachineOutputRule(machine, machineData, MachineOutputTrigger.MachinePutDown, null, who, location, out var outputRule, out _, out _, out _))
                        {
                            machine.OutputMachine(machineData, outputRule, null, who, location, probe: false);
                        }
                    }
                    finally
                    {
                        _machinesRunning.Remove(key);
                    }
                }, 5000);
                return null;
            }
        }
        
        var itemOutputs = GetFluidInfoFromDict(outputData.CustomData, OutputTypeKeyRegex, OutputVolumeKeyPrefix);
        var machineOutputs = GetFluidInfoFromDict(machine.GetMachineData().CustomFields, OutputTypeKeyRegex, OutputVolumeKeyPrefix);
        var desiredOutputs = itemOutputs.Concat(machineOutputs).ToArray();

        Item? item2 = null;
        if (outputData.ItemId != null)
        {
            ItemQueryContext context = new ItemQueryContext(machine.Location, player, Game1.random, "machine '" + machine.QualifiedItemId + "' > output rules");
            item2 = ItemQueryResolver.TryResolveRandomItem(outputData, context, avoidRepeat: false, null,
                (string id) => MachineDataUtility.FormatOutputId(id, machine, outputData, inputItem, player), inputItem,
                delegate(string query, string error) { ModEntry.Debug($"Machine '{machine.QualifiedItemId}' failed parsing item query '{query}' for output '{outputData.Id}': {error}."); });
        }
        
        if (desiredOutputs.Length == 0 && item2 != null)
        {
            return item2;
        }

        if (desiredOutputs.Length == 0 && item2 == null)
        {
            return null;
        }

        // output liquid
        var chest = new Chest(false);
        Object? outputItem = ItemRegistry.Create<Object>("(O)" + ModEntry.FluidContainerID, 1);
        outputItem.heldObject.Value = chest;
        outputItem.modData[LiquidKey] = "hi";
        for(int i = 0; i < desiredOutputs.Length; i++)
        {
            var item = ItemRegistry.Create<Object>("(O)" + desiredOutputs[i].fluidType, desiredOutputs[i].volume);
            item.modData[LiquidKey] = "hi";
            chest.addItem(item);
        }

        if (item2 != null)
        {
            chest.addItem(item2);
        }
        
        return outputItem;
    }
    
    public static readonly List<Vector2> Directions = new()
    {
        { new Vector2(0, -1) },
        { new Vector2(1, 0) },
        { new Vector2(0, 1) },
        { new Vector2(-1, 0) }
    };
    
    public static void GetAvailableFluids(GameLocation location, ref HashSet<Vector2> visited, Vector2 tilePos, ref List<AvailableFluid> foundFluids)
    {
        foreach (var dir in Directions)
        {
            if (!visited.Add(tilePos + dir))
            {
                continue;
            }
            
            if (!location.objects.TryGetValue(dir + tilePos, out var obj))
            {
                continue;
            }

            if (obj.heldObject.Value != null && obj.heldObject.Value.ItemId == ModEntry.FluidContainerID && obj.heldObject.Value.heldObject.Value is Chest chest)
            {
                foreach (var item in chest.Items)
                {
                    if (item.modData.TryGetValue(LiquidKey, out var liquid))
                    {
                        // we have a liquid
                        foundFluids.Add(new AvailableFluid((Object)item, chest, obj));
                    }
                }
            }
            else if (obj.QualifiedItemId == ModEntry.PipesQID)
            {
                // continue recursive
                GetAvailableFluids(location, ref visited, dir+tilePos, ref foundFluids);
            }
        }
    }
    
    public static List<FluidDataEntry> GetFluidInfoFromDict(Dictionary<string,string>? modData, Regex regexType, string volKey)
    {
        List<FluidDataEntry> fluidInfo = new List<FluidDataEntry>();
        if (modData == null)
        {
            return fluidInfo;
        }

        foreach (var entry in modData)
        {
            var match = regexType.Match(entry.Key);
            if (!match.Success)
            {
                continue;
            }
            
            string volumeKey = volKey + match.Groups[1].Value;
            int volume = 100;
            if (modData.TryGetValue(volumeKey, out var countString) && Int32.TryParse(countString, out int parsedVolume))
            {
                volume = parsedVolume;
            }
            
            fluidInfo.Add( new FluidDataEntry(entry.Value, volume) );
        }
        return fluidInfo;
    }
}
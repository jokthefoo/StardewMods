using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using Object = StardewValley.Object;

namespace BiggerMachines;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    public static Mod Instance;
    public static IMonitor MonitorInst;
    public static IModHelper Helper;
    //MonitorInst.Log($"X value: {x}", LogLevel.Info);

    public static Dictionary<string, Dictionary<Vector2, BiggerMachine>> LocationBigMachines = new();
    public static Dictionary<string, BiggerMachineData> BigMachinesList = new();

    public const string ModDataAlphaKey = "Jok.BiggerMachines.Alpha"; // just used for internal tracking for transparency
    public const string ModDataDimensionsKey = "Jok.BiggerMachines.Dimensions"; // dimensions of object
    public const string ModDataFadeBehindKey = "Jok.BiggerMachines.EnableTransparency"; // transparency when player is behind
    public const string ModDataShadowKey = "Jok.BiggerMachines.DrawShadow"; // draws a shadow similar to building shadow
    public const string ModDataChestKey = "Jok.BiggerMachines.IsChest"; // is a chest

    /*********
     ** Public methods
     *********/
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        Instance = this;
        MonitorInst = Monitor;
        Helper = helper;
        Helper.Events.Content.AssetRequested += OnAssetRequested;
        Helper.Events.World.ObjectListChanged += OnObjectListChanged;
        Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;

        if (!Context.IsMainPlayer)
        {
            Helper.Events.Player.Warped += OnPlayerWarp; // used on clients
        }

        HarmonyPatches.Patch(ModManifest.UniqueID);
    }

    private void OnPlayerWarp(object? sender, WarpedEventArgs e)
    {
        if (!Context.IsMainPlayer)
        {
            e.OldLocation.netObjects.OnValueAdded -= OnObjectAddedFarmhand;
            e.OldLocation.netObjects.OnValueRemoved -= OnObjectRemovedFarmhand;
            AddAllBiggerMachinesToTrackerList(e.NewLocation);
            RemoveDeadMachinesFromList(e.NewLocation);
            e.NewLocation.netObjects.OnValueAdded += OnObjectAddedFarmhand;
            e.NewLocation.netObjects.OnValueRemoved += OnObjectRemovedFarmhand;
        }
    }

    private void OnObjectAddedFarmhand(Vector2 pos, Object obj)
    {
        if (!obj.bigCraftable.Value || !BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData))
        {
            return;
        }

        if (!LocationBigMachines.ContainsKey(obj.Location.Name))
        {
            LocationBigMachines.TryAdd(obj.Location.Name, new Dictionary<Vector2, BiggerMachine>());
        }

        BiggerMachine bm = new BiggerMachine(obj, bigMachineData);
        if (!LocationBigMachines[obj.Location.Name].TryAdd(pos, bm))
        {
            MonitorInst.Log(
                $"Jok.BiggerMachines tried to add machine at: {pos.ToString()}, but machine already at position",
                LogLevel.Error);
        }
    }

    private void OnObjectRemovedFarmhand(Vector2 pos, Object obj)
    {
        if (!obj.bigCraftable.Value || !BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData) ||
            !LocationBigMachines.ContainsKey(obj.Location.Name))
        {
            return;
        }

        if (!LocationBigMachines[obj.Location.Name].Remove(pos))
        {
            MonitorInst.Log(
                $"Jok.BiggerMachines tried to remove machine at: {pos.ToString()}, but machine was not found.",
                LogLevel.Error);
        }
    }

    private void AddAllBiggerMachinesToTrackerList(GameLocation location)
    {
        foreach (var obj in location.Objects.Values)
        {
            if (!obj.bigCraftable.Value || !BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData))
            {
                continue;
            }

            if (!LocationBigMachines.ContainsKey(location.Name))
            {
                LocationBigMachines.TryAdd(location.Name, new Dictionary<Vector2, BiggerMachine>());
            }

            BiggerMachine bm = new BiggerMachine(obj, bigMachineData);
            LocationBigMachines[location.Name].TryAdd(obj.TileLocation, bm);
        }
    }

    private void RemoveDeadMachinesFromList(GameLocation location)
    {
        if (!LocationBigMachines.ContainsKey(location.Name))
        {
            return;
        }

        List<Vector2> toRemove = new List<Vector2>();
        foreach (var obj in LocationBigMachines[location.Name].Values)
        {
            if (!location.Objects.TryGetValue(obj.Object.TileLocation, out var value))
            {
                toRemove.Add(obj.Object.TileLocation);
            }
        }

        foreach (var pos in toRemove)
        {
            LocationBigMachines[location.Name].Remove(pos);
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        LocationBigMachines.Clear();
        Utility.ForEachLocation(location =>
        {
            AddAllBiggerMachinesToTrackerList(location);
            return true;
        });

        if (!Context.IsMainPlayer)
        {
            Game1.player.currentLocation.netObjects.OnValueAdded += OnObjectAddedFarmhand;
            Game1.player.currentLocation.netObjects.OnValueRemoved += OnObjectRemovedFarmhand;
        }
    }

    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        if (!Context.IsMainPlayer)
        {
            return;
        }

        var location = e.Location;

        if (!LocationBigMachines.ContainsKey(location.Name))
        {
            LocationBigMachines.TryAdd(location.Name, new Dictionary<Vector2, BiggerMachine>());
        }

        foreach (var (pos, obj) in e.Added)
        {
            if (!obj.bigCraftable.Value || !BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData) ||
                bigMachineData.IsChest)
            {
                continue;
            }

            BiggerMachine bm = new BiggerMachine(obj, bigMachineData);
            if (!LocationBigMachines[location.Name].TryAdd(pos, bm))
            {
                MonitorInst.Log(
                    $"Jok.BiggerMachines tried to add machine at: {pos.ToString()}, but machine already at position",
                    LogLevel.Warn);
            }
        }

        foreach (var (pos, obj) in e.Removed)
        {
            if (!obj.bigCraftable.Value || !BigMachinesList.TryGetValue(obj.ItemId, out var bigMachineData))
            {
                continue;
            }

            if (!LocationBigMachines[location.Name].Remove(pos))
            {
                MonitorInst.Log(
                    $"Jok.BiggerMachines tried to remove machine at: {pos.ToString()}, but machine was not found.",
                    LogLevel.Error);
            }
        }
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, BigCraftableData>().Data;
                foreach (var (itemid, bc) in data)
                {
                    if (bc.CustomFields == null)
                    {
                        continue;
                    }

                    ParseBigMachineCustomData(bc, itemid);
                }
            }, AssetEditPriority.Late);
        }
    }

    private void ParseBigMachineCustomData(BigCraftableData bigCraftableData, string itemId)
    {
        BigMachinesList.Clear();
        if (bigCraftableData.CustomFields.TryGetValue(ModDataDimensionsKey, out string? value))
        {
            string[] dims = value.Split(",");
            if (Int32.TryParse(dims[0], out var width) && Int32.TryParse(dims[1], out var height))
            {
                bool hasFading = false;
                if (bigCraftableData.CustomFields.TryGetValue(ModDataFadeBehindKey, out string? fadeBehind))
                {
                    if (fadeBehind.ToLower() == "true")
                    {
                        hasFading = true;
                    }
                }

                bool drawShadow = false;
                if (bigCraftableData.CustomFields.TryGetValue(ModDataShadowKey, out string? shadow))
                {
                    if (shadow.ToLower() == "true")
                    {
                        drawShadow = true;
                    }
                }

                bool isChest = false;
                if (bigCraftableData.CustomFields.TryGetValue(ModDataChestKey, out string? chest))
                {
                    if (chest.ToLower() == "true")
                    {
                        isChest = true;
                    }
                }

                BigMachinesList.Add(itemId, new BiggerMachineData(width, height, hasFading, drawShadow, isChest));
            }
        }
    }
}
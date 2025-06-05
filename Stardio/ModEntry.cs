using System.Xml.Serialization;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace Jok.Stardio;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    internal static Mod Instance;
    internal static IMonitor MonitorInst;
    internal static IModHelper Helper;
    internal static StardioConfig Config;
    internal static IExtraMachineConfigApi? EMCApi;
    internal static IBiggerMachinesAPI? BMApi;
    internal static IFurnitureMachineApi? FMApi;

    internal const string MACHINE_STATE_KEY = "Jok.Stardio.MachineState";
    internal const string BUILDING_CHEST_KEY = "Jok.Stardio/BuildingChest";
    internal const string INPUT_CHEST_QID = "(BC)Jok.Stardio.InputChest";
    internal const string OUTPUT_CHEST_QID = "(BC)Jok.Stardio.OutputChest";
    internal const string FILTER_QID = "(Jok.Belt)Jok.Stardio.Filter";
    internal static Texture2D dronepadTexture;
    internal static Texture2D dronesTexture;
    //ModEntry.MonitorInst.Log($"X value: {x}", LogLevel.Info);

    public const string BELT_IGNORE_KEY = "Jok.Stardio.NoGrabby";
    
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
        I18n.Init(Helper.Translation);
        Config = Helper.ReadConfig<StardioConfig>();

        Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        Helper.Events.Content.AssetRequested += OnAssetRequested;
        Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        Helper.Events.Input.ButtonPressed += OnButtonPressed;
        Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        Helper.Events.GameLoop.Saving += OnSaving;
        Helper.Events.World.ObjectListChanged += OnObjectListChanged;

        ItemRegistry.AddTypeDefinition(new BeltItemDataDefinition());
        Helper.ModContent.Load<Texture2D>("assets/belts");
        Helper.ModContent.Load<Texture2D>("assets/otherbelts");
        Helper.ModContent.Load<Texture2D>("assets/belts2");
        Helper.ModContent.Load<Texture2D>("assets/otherbelts2");
        Helper.ModContent.Load<Texture2D>("assets/chest");
        Helper.ModContent.Load<Texture2D>("assets/filter");
        dronesTexture = Helper.ModContent.Load<Texture2D>("assets/drones");
        dronepadTexture = Helper.ModContent.Load<Texture2D>("assets/dronepad");

        HarmonyPatches.Patch(ModManifest.UniqueID);
    }

    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        if (!Context.IsMainPlayer)
        {
            return;
        }

        foreach (var (tileLoc, obj) in e.Removed)
        {
            MachineStateManager.RemoveState(e.Location, tileLoc);

            if (obj is IBeltPushing belt)
            {
                belt.UpdateNeighborCurves(tileLoc);
            }

            if (IsObjectDroneHub(obj))
            {
                if (obj.heldObject.Value is Chest chest)
                {
                    foreach (Item item in chest.Items)
                    {
                        if (item != null)
                        {
                            Game1.createItemDebris(item, Game1.player.getStandingPosition(), Game1.player.FacingDirection);
                        }
                    }
                }
                obj.heldObject.Value = null;

                bool removeKey = true;

                foreach (Object obj2 in obj.Location.Objects.Values)
                {
                    if (IsObjectDroneHub(obj2))
                    {
                        removeKey = false;
                        break;
                    }
                }

                if (removeKey)
                {
                    if (obj.Location.ParentBuilding != null)
                    {
                        obj.Location.ParentBuilding.modData.Remove(BUILDING_CHEST_KEY);
                        UpdateBeltsSurroundingBuilding(obj.Location.ParentBuilding);
                    }
                }
            }
        }

        foreach (var (tileLoc, obj) in e.Added)
        {
            if (obj is BeltItem belt)
            {
                belt.CheckForCurve();
            }

            if (IsObjectDroneHub(obj))
            {
                obj.heldObject.Value = new Chest(false);

                if (obj.Location.ParentBuilding != null && obj.Location.ParentBuilding.modData.TryAdd(BUILDING_CHEST_KEY, "hi"))
                {
                    UpdateBeltsSurroundingBuilding(obj.Location.ParentBuilding);
                }
            }
        }
    }

    private void UpdateBeltsSurroundingBuilding(Building building)
    {
        int leftX = building.tileX.Value - 1;
        int topY = building.tileY.Value - 1;
        int width = building.tilesWide.Value + 1;
        int height = building.tilesHigh.Value + 1;
        GameLocation loc = building.GetParentLocation();

        for (int x = leftX; x <= leftX + width; x++)
        {
            if (loc.objects.TryGetValue(new Vector2(x, topY), out var obj) && obj is BeltItem belt)
            {
                belt.CheckForCurve();
            }

            if (loc.objects.TryGetValue(new Vector2(x, topY + height), out var obj2) && obj2 is BeltItem belt2)
            {
                belt2.CheckForCurve();
            }
        }

        for (int y = topY; y <= topY + height; y++)
        {
            if (loc.objects.TryGetValue(new Vector2(leftX, y), out var obj) && obj is BeltItem belt)
            {
                belt.CheckForCurve();
            }

            if (loc.objects.TryGetValue(new Vector2(leftX + width, y), out var obj2) && obj2 is BeltItem belt2)
            {
                belt2.CheckForCurve();
            }
        }
    }

    private static readonly XmlSerializer ItemSerializer = new(typeof(ItemListSerialized), new[] { typeof(Item) });

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        if (!Context.IsMainPlayer)
        {
            return;
        }
        MonitorInst.Log($"Creating save", LogLevel.Info);

        Dictionary<string, Dictionary<Vector2, ModMachineStateSerialized>?> serialized = new Dictionary<string, Dictionary<Vector2, ModMachineStateSerialized>?>();

        foreach (var (location, locationDict) in MachineStateManager.MachineStates)
        {
            if (locationDict == null)
            {
                continue;
            }
            var innerDict = new Dictionary<Vector2, ModMachineStateSerialized>();

            foreach (var (tileLocation, machineState) in locationDict)
            {
                var writer = new StringWriter();
                ItemListSerialized itemListSerialized = new();
                itemListSerialized.currentInventory = machineState.currentInventory;
                ItemSerializer.Serialize(writer, itemListSerialized);

                ModMachineStateSerialized newStateSerialized = new ModMachineStateSerialized();
                newStateSerialized.outputRule = machineState.outputRule;
                newStateSerialized.outputTrigger = machineState.outputTrigger;
                newStateSerialized.currentInventory = writer.ToString();

                innerDict[tileLocation] = newStateSerialized;
            }
            serialized[location] = innerDict;
        }
        Helper.Data.WriteSaveData(MACHINE_STATE_KEY, serialized);
    }

    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
        if (!Context.IsMainPlayer)
        {
            return;
        }

        var stateData = Helper.Data.ReadSaveData<Dictionary<string, Dictionary<Vector2, ModMachineStateSerialized>?>>(MACHINE_STATE_KEY);

        if (stateData == null)
        {
            MachineStateManager.MachineStates = new Dictionary<string, Dictionary<Vector2, ModMachineState>?>();
            return;
        }

        MachineStateManager.MachineStates = new Dictionary<string, Dictionary<Vector2, ModMachineState>?>();

        foreach (var (location, locationDict) in stateData)
        {
            MachineStateManager.MachineStates.Add(location, new Dictionary<Vector2, ModMachineState>());

            foreach (var (tileLocation, machineStateSerialized) in locationDict)
            {
                using var reader = new StringReader(machineStateSerialized.currentInventory);
                var obj = ItemSerializer.Deserialize(reader) as ItemListSerialized;

                var machineState = new ModMachineState(machineStateSerialized.outputRule, machineStateSerialized.outputTrigger);
                machineState.currentInventory = obj.currentInventory;
                MachineStateManager.MachineStates[location].Add(tileLocation, machineState);
            }
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (Config.RotateKeybind == e.Button)
        {
            if (Game1.player.ActiveItem is BeltItem belt)
            {
                belt.rotate(true);
            }
            else if (Game1.player.currentLocation.objects.TryGetValue(e.Cursor.Tile, out Object obj) && obj is BeltItem belt2)
            {
                belt2.rotate();
            }
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        bool isProcessTick = false;
        BeltItem.beltUpdateCountdown -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;

        if (BeltItem.beltUpdateCountdown <= 0)
        {
            BeltItem.beltUpdateCountdown = Math.Clamp(Config.BeltUpdateMS, 10, Config.BeltUpdateMS) / 2;
            BeltItem.BeltAnim++;

            if (BeltItem.BeltAnim > 3)
            {
                BeltItem.BeltAnim = 0;
                BridgeItem.BridgeAnim += 1;
            }

            if (BridgeItem.BridgeAnim > 3)
            {
                BridgeItem.BridgeAnim = 0;
            }

            isProcessTick = true;
        }

        if (!Context.IsMainPlayer)
        {
            return;
        }

        foreach (GameLocation location in Game1.locations)
        {
            UpdateAllBelts(location, isProcessTick);

            foreach (var building in location.buildings)
            {
                if (building.indoors.Value != null)
                {
                    UpdateAllBelts(building.indoors.Value, isProcessTick);
                }
            }
        }
    }

    private void UpdateAllBelts(GameLocation location, bool isProcessTick)
    {
        foreach (Object obj in location.objects.Values)
        {
            if (obj is BeltItem belt)
            {
                belt.beltUpdate(isProcessTick);
                continue;
            }

            if (obj is SplitterItem splitter)
            {
                splitter.splitterUpdate(isProcessTick);
            }
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var sc = Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
        sc.RegisterSerializerType(typeof(BeltItem));
        sc.RegisterSerializerType(typeof(BridgeItem));
        sc.RegisterSerializerType(typeof(SplitterItem));
        sc.RegisterSerializerType(typeof(FilterItem));
        EMCApi = Helper.ModRegistry.GetApi<IExtraMachineConfigApi>("selph.ExtraMachineConfig");
        FMApi = Helper.ModRegistry.GetApi<IFurnitureMachineApi>("selph.FurnitureMachine");
        BMApi = Helper.ModRegistry.GetApi<IBiggerMachinesAPI>("Jok.BiggerMachines");

        SetupConfigs();
    }

    private void SetupConfigs()
    {
        var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu is null)
            return;

        // register mod
        configMenu.Register(mod: ModManifest, reset: () => Config = new StardioConfig(), save: () => Helper.WriteConfig(Config));

        // add config options
        configMenu.AddKeybind(mod: ModManifest, name: I18n.Config_Rkeybind_Name, tooltip: I18n.Config_Rkeybind_Tooltip, getValue: () => Config.RotateKeybind,
            setValue: value => Config.RotateKeybind = value);

        configMenu.AddNumberOption(mod: ModManifest, name: I18n.Config_Pushplayer_Name, tooltip: I18n.Config_Pushplayer_Tooltip, getValue: () => Config.BeltPushPlayerSpeed,
            setValue: value => Config.BeltPushPlayerSpeed = value, min: 0f, max: 10f, interval: .1f);

        configMenu.AddNumberOption(mod: ModManifest, name: I18n.Config_Speedplayer_Name, tooltip: I18n.Config_Speedplayer_Tooltip, getValue: () => Config.BeltPlayerSpeedBoost,
            setValue: value => Config.BeltPlayerSpeedBoost = value, min: 0f, max: 10f, interval: .1f);

        configMenu.AddBoolOption(mod: ModManifest, name: I18n.Config_Quality_Name, tooltip: I18n.Config_Quality_Tooltip, getValue: () => Config.ShowQualityOnBelts,
            setValue: value => Config.ShowQualityOnBelts = value);

        configMenu.AddNumberOption(mod: ModManifest, name: I18n.Config_Updaterate_Name, tooltip: I18n.Config_Updaterate_Tooltip, getValue: () => Config.BeltUpdateMS,
            setValue: value => Config.BeltUpdateMS = value, min: 10, max: 1000, interval: 5);

        configMenu.AddBoolOption(mod: ModManifest, name: I18n.Config_Brownbelts_Name, tooltip: I18n.Config_Brownbelts_Tooltip, getValue: () => Config.BrownBelts, setValue: value =>
        {
            Config.BrownBelts = value;
            Helper.GameContent.InvalidateCache($"{ModManifest.UniqueID}/belts.png");
            Helper.GameContent.InvalidateCache($"{ModManifest.UniqueID}/otherbelts.png");
        });

        configMenu.AddBoolOption(mod: ModManifest, name: I18n.Config_Dronehub_Name, tooltip: I18n.Config_Dronehub_Tooltip, getValue: () => Config.DroneHub, setValue: value => Config.DroneHub = value);
        configMenu.AddBoolOption(mod: ModManifest, name: I18n.Config_Pullmachines_Name, tooltip: I18n.Config_Pullmachines_Tooltip, getValue: () => Config.PullFromMachines, setValue: value => Config.PullFromMachines = value);
        configMenu.AddBoolOption(mod: ModManifest, name: I18n.Config_Pushmachine_Name, tooltip: I18n.Config_Pushmachine_Tooltip, getValue: () => Config.PushIntoMachines, setValue: value => Config.PushIntoMachines = value);
    }

    private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/FactoryItems"))
        {
            e.LoadFrom(() =>
            {
                var modAssets = Helper.ModContent.Load<Dictionary<string, BeltData>>("assets/belt_data.json");
                Dictionary<string, BeltData> ret = new();

                foreach (string id in modAssets.Keys)
                {
                    var bd = new BeltData()
                    {
                        Texture = modAssets[id].Texture,
                        TextureIndex = modAssets[id].TextureIndex,
                        DisplayName = I18n.GetByKey(modAssets[id].DisplayName),
                        Description = I18n.GetByKey(modAssets[id].Description),
                        Price = modAssets[id].Price
                    };
                    ret.Add(id, bd);
                }

                return ret;
            }, AssetLoadPriority.Exclusive);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/belts.png"))
        {
            if (Config.BrownBelts)
            {
                e.LoadFromModFile<Texture2D>("assets/belts2.png", AssetLoadPriority.Low);
            }
            else
            {
                e.LoadFromModFile<Texture2D>("assets/belts.png", AssetLoadPriority.Low);
            }
        }
        else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/otherbelts.png"))
        {
            if (Config.BrownBelts)
            {
                e.LoadFromModFile<Texture2D>("assets/otherbelts2.png", AssetLoadPriority.Low);
            }
            else
            {
                e.LoadFromModFile<Texture2D>("assets/otherbelts.png", AssetLoadPriority.Low);
            }
        }
        else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/chest.png"))
        {
            e.LoadFromModFile<Texture2D>("assets/chest.png", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/filter.png"))
        {
            e.LoadFromModFile<Texture2D>("assets/filter.png", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/drones.png"))
        {
            e.LoadFromModFile<Texture2D>("assets/drones.png", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/dronepad.png"))
        {
            e.LoadFromModFile<Texture2D>("assets/dronepad.png", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
        {
            e.Edit(asset =>
            {
                var dict = asset.AsDictionary<string, string>().Data;
                // ingredients / unused / yield / big craftable? / unlock conditions /
                dict.Add("(Jok.Belt)Jok.Stardio.Belt", $"335 5 390 25 388 25/what/(Jok.Belt)Jok.Stardio.Belt 5/false/s farming 3/");
                dict.Add("(Jok.Belt)Jok.Stardio.Bridge", $"336 1 (Jok.Belt)Jok.Stardio.Belt 5/what/(Jok.Belt)Jok.Stardio.Bridge 1/false/s farming 5/");
                dict.Add("(Jok.Belt)Jok.Stardio.Splitter", $"336 1 (Jok.Belt)Jok.Stardio.Belt 5/what/(Jok.Belt)Jok.Stardio.Splitter 1/false/s farming 5/");

                dict.Add("Jok.Stardio.InputChest", $"337 1 (Jok.Belt)Jok.Stardio.Bridge 2/what/Jok.Stardio.InputChest 1/true/s farming 10/");
                dict.Add("Jok.Stardio.OutputChest", $"337 1 (Jok.Belt)Jok.Stardio.Splitter 2/what/Jok.Stardio.OutputChest 1/true/s farming 10/");
                
                dict.Add("(Jok.Belt)Jok.Stardio.Filter", $"787 1 (Jok.Belt)Jok.Stardio.Splitter 1 (Jok.Belt)Jok.Stardio.Bridge 1/what/(Jok.Belt)Jok.Stardio.Filter 1/false/s farming 7/");
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
        {
            e.Edit(asset =>
            {
                IDictionary<string, BigCraftableData> data = asset.AsDictionary<string, BigCraftableData>().Data;
                data["Jok.Stardio.InputChest"] = new BigCraftableData()
                {
                    Name = "Jok.Stardio.InputChest",
                    DisplayName = I18n.Inputchest_Name(),
                    Description = I18n.Inputchest_Description(),
                    CanBePlacedOutdoors = true,
                    CanBePlacedIndoors = true,
                    Texture = Helper.ModContent.GetInternalAssetName("assets/chest").Name,
                    SpriteIndex = 0
                };

                data["Jok.Stardio.OutputChest"] = new BigCraftableData()
                {
                    Name = "Jok.Stardio.OutputChest",
                    DisplayName = I18n.Outputchest_Name(),
                    Description = I18n.Outputchest_Description(),
                    CanBePlacedOutdoors = true,
                    CanBePlacedIndoors = true,
                    Texture = Helper.ModContent.GetInternalAssetName("assets/chest").Name,
                    SpriteIndex = 3
                };
            });
        }
        /*
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Machines"))
        {
            e.Edit(asset =>
            { 
                var data = asset.AsDictionary<string, MachineData>().Data;
                
                var triggers = new List<MachineOutputTriggerRule> { new MachineOutputTriggerRule { Id = "DayUpdate", Trigger = MachineOutputTrigger.DayUpdate, RequiredItemId = null} };
                var output = new List<MachineItemOutput> { new MachineItemOutput { Id = "Default", ItemId = null, OutputMethod = "Jok.Stardio.ModEntry, Stardio: OutputNothing"} };
                var outputRules = new List<MachineOutputRule>
                {
                    new MachineOutputRule
                    {
                        Id = "HelloThere", Triggers = triggers, OutputItem = output, DaysUntilReady = Int32.MaxValue,
                    }
                };
                var timepass = new List<MachineTimeBlockers> { MachineTimeBlockers.Always };
                
                var machineData = new MachineData
                {
                    OutputRules = outputRules,
                    PreventTimePass = timepass,
                    AllowFairyDust = false,
                    WobbleWhileWorking = false,
                };
                
                data.Add("(BC)Jok.Stardio.InputChest", machineData);
                data.Add("(BC)Jok.Stardio.OutputChest", machineData);
            });
        }*/
    }

    public static bool IsObjectDroneHub(Object obj)
    {
        return obj.QualifiedItemId == OUTPUT_CHEST_QID || obj.QualifiedItemId == INPUT_CHEST_QID;
    }
    
    
    /// <summary>Get the output item to produce.</summary>
    /// <param name="machine">The machine instance for which to produce output.</param>
    /// <param name="inputItem">The item being dropped into the machine, if applicable.</param>
    /// <param name="probe">Whether the machine is only checking whether the input is valid. If so, the input/machine shouldn't be changed and no animations/sounds should play.</param>
    /// <param name="outputData">The item output data from <c>Data/Machines</c> for which output is being created, if applicable.</param>
    /// <param name="overrideMinutesUntilReady">The in-game minutes until the item will be ready to collect, if set. This overrides the equivalent fields in the machine data if set.</param>
    /// <returns>Returns the item to produce, or <c>null</c> if none should be produced.</returns>
    public static Item? OutputNothing(Object machine, Item inputItem, bool probe, MachineItemOutput outputData, Farmer player, out int? overrideMinutesUntilReady)
    {
        overrideMinutesUntilReady = null;
        return null;
    }
}

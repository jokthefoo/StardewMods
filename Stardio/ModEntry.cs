using System.Xml.Serialization;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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

    private const string MACHINE_STATE_KEY = "Jok.Stardio.MachineState";
    //ModEntry.MonitorInst.Log($"X value: {x}", LogLevel.Info);

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

        HarmonyPatches.Patch(ModManifest.UniqueID);
    }
    
    // TODO splitter
    // TODO junction, insta tele to other side if space available else wait

    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        foreach (var (tileLoc, obj) in e.Removed)
        {
            MachineStateManager.RemoveState(e.Location, tileLoc);

            if (obj is BeltItem belt)
            {
                belt.UpdateNeighborCurves(tileLoc);
            }
        }
        
        foreach (var (tileLoc, obj) in e.Added)
        {
            if (obj is BeltItem belt)
            {
                belt.CheckForCurve();
            }
        }
    }

    // TODO joja cola competitor

    private static readonly XmlSerializer ItemSerializer = new(typeof(ItemListSerialized), new []
        {typeof(Item)});
    private void OnSaving(object? sender, SavingEventArgs e)
    {
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

        foreach (GameLocation location in Game1.locations)
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
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var sc = Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
        sc.RegisterSerializerType(typeof(BeltItem));
        sc.RegisterSerializerType(typeof(BridgeItem));
        sc.RegisterSerializerType(typeof(SplitterItem));
        EMCApi = Helper.ModRegistry.GetApi<IExtraMachineConfigApi>("selph.ExtraMachineConfig");
        BMApi = Helper.ModRegistry.GetApi<IBiggerMachinesAPI>("Jok.BiggerMachines");

        SetupConfigs();
    }

    private void SetupConfigs()
    {
        var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu is null)
            return;
        
        // register mod
        configMenu.Register(
            mod: ModManifest,
            reset: () => Config = new StardioConfig(),
            save: () => Helper.WriteConfig(Config)
        );

        // add config options
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: I18n.Config_Quality_Name,
            tooltip: I18n.Config_Quality_Tooltip,
            getValue: () => Config.ShowQualityOnBelts,
            setValue: value => Config.ShowQualityOnBelts = value
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: I18n.Config_Updaterate_Name,
            tooltip: I18n.Config_Updaterate_Tooltip,
            getValue: () => Config.BeltUpdateMS,
            setValue: value => Config.BeltUpdateMS = value,
            min: 10,
            max: 1000,
            interval: 5
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: I18n.Config_Pushplayer_Name,
            tooltip: I18n.Config_Pushplayer_Tooltip,
            getValue: () => Config.BeltPushPlayerSpeed,
            setValue: value => Config.BeltPushPlayerSpeed = value,
            min: 0f,
            max: 10f,
            interval: .1f
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: I18n.Config_Speedplayer_Name,
            tooltip: I18n.Config_Speedplayer_Tooltip,
            getValue: () => Config.BeltPlayerSpeedBoost,
            setValue: value => Config.BeltPlayerSpeedBoost = value,
            min: 0f,
            max: 10f,
            interval: .1f
        );
        
        configMenu.AddKeybind(
            mod: ModManifest,
            name: I18n.Config_Rkeybind_Name,
            tooltip: I18n.Config_Rkeybind_Tooltip,
            getValue: () => Config.RotateKeybind,
            setValue: value => Config.RotateKeybind = value
        );
        
        /*
        // add config options
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: I18n.Config_Greybelts_Name,
            tooltip: I18n.Config_Greybelts_Tooltip,
            getValue: () => Config.GreyBelts,
            setValue: value => Config.GreyBelts = value
        );*/
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
            e.LoadFromModFile<Texture2D>("assets/belts.png", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/otherbelts.png"))
        {
            e.LoadFromModFile<Texture2D>("assets/otherbelts.png", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
        {
            e.Edit(asset =>
            {
                var dict = asset.AsDictionary<string, string>().Data;
                // ingredients / unused / yield / big craftable? / unlock conditions /
                dict.Add("(Jok.Belt)Jok.Stardio.Belt", $"335 5 390 25 388 25/what/(Jok.Belt)Jok.Stardio.Belt 5/false/s farming 3/");
                dict.Add("(Jok.Belt)Jok.Stardio.Bridge", $"336 1 (Jok.Belt)Jok.Stardio.Belt 5/what/(Jok.Belt)Jok.Stardio.Bridge 1/false/s farming 5/");
            });
        }
    }
}
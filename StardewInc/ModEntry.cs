using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewInc;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Object = StardewValley.Object;

namespace Jok.StardewInc;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    internal static Mod Instance;
    internal static IMonitor MonitorInst;
    internal static IModHelper Helper;
    internal static StardioConfig Config;
    internal static IBiggerMachinesAPI? BMApi;

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

        //ItemRegistry.AddTypeDefinition(new BeltItemDataDefinition());
        //Helper.ModContent.Load<Texture2D>("assets/belts");

        HarmonyPatches.Patch(ModManifest.UniqueID);
    }

    // TODO miner actually mines
    // TODO miner/forge crafting recipe
    // TODO add bauxite to forge machine data (EMC?)
    // TODO forge + bauxite output
    
    // TODO add bauxite stone to mines -- probably skull or volcano both?
    // TODO new location with mine-able ground ores (probably spots for all (most) types, area unlocks progressively)
    
    // TODO more bauxite->alum processing
    
    // TODO farmer cola
    
    // TODO make soda
    // TODO soda->can
    
    // TODO progression system
    
    // TODO electric furnace?
    
    // TODO belt filter/sorter?
    // TODO belt cross-map transport?
    
    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
    }
    
    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        //var sc = Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
        //sc.RegisterSerializerType(typeof(BeltItem));
        //EMCApi = Helper.ModRegistry.GetApi<IExtraMachineConfigApi>("selph.ExtraMachineConfig");
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
    }

    private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
    {
    }
}
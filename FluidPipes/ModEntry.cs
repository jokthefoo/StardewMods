﻿using Jok.Stardio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Objects;
using Object = StardewValley.Object;

namespace FluidPipes;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    internal static Mod Instance;
    internal static IMonitor MonitorInst;
    internal static IModHelper Helper;
    internal static IBiggerMachinesAPI? BMApi;
    //MonitorInst.Log($"X value: {x}", LogLevel.Info);

    //internal static IFluidPipesAPI FPApi = null!;
    internal static string WeedsKey = "Jok.FluidWeeds";
    internal static string PipesQID = "(O)Jok.FluidPipes.Pipe";
    internal static string PipesID = "Jok.FluidPipes.Pipe";
    internal static string FluidContainerID = "Jok.FluidPipes.FluidContainer";
    internal static string PumpKey = "Jok.FluidPipes.Pump";

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
        //FPApi = new FluidPipesAPI();
        Helper.Events.Content.AssetRequested += OnAssetRequested;
        Helper.Events.World.ObjectListChanged += OnObjectListChanged;
        Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        Helper.Events.GameLoop.GameLaunched += OnGameLaunched;

        HarmonyPatches.Patch(ModManifest.UniqueID);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var sc = Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
        sc.RegisterSerializerType(typeof(FluidPipe));
        BMApi = Helper.ModRegistry.GetApi<IBiggerMachinesAPI>("Jok.BiggerMachines");
    }

    /*
    public override object GetApi() {
        return FPApi;
    }*/
    
    // TODO fluids probably good to go --- make machines that use them in StardewInc
    // TODO pipe/belt bridge?
    
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
    }

    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        if (!Context.IsMainPlayer)
        {
            return;
        }

        foreach (var obj in e.Added)
        {
            FluidPipe.CheckForNeighborCurves(obj.Key, e.Location);
            if (obj.Value is FluidPipe pipe)
            {
                pipe.CheckForCurve();
            }
        }
        
        foreach (var obj in e.Removed)
        {
            FluidPipe.CheckForNeighborCurves(obj.Key, e.Location);
        }
    }

    public static void Debug(string str)
    {
        MonitorInst.Log($"Debug: {str}", LogLevel.Warn);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, ObjectData>().Data;
                data[PipesID] = new ObjectData()
                {
                    Name = PipesID,
                    DisplayName = I18n.Pipe_Name(),
                    Description = I18n.Pipe_Description(),
                    Type = "Crafting",
                    Category = -8, // crafting
                    Texture = Helper.ModContent.GetInternalAssetName("assets/pipes").Name,
                    SpriteIndex = 4,
                    CanBeGivenAsGift = false,
                };
                
                data["Jok.FluidPipes.Water"] = new ObjectData()
                {
                    Name = "Jok.FluidPipes.Water",
                    DisplayName = I18n.Water_Name(),
                    Description = I18n.Water_Description(),
                    Type = "Crafting",
                    Category = -29, // equipment
                    Texture = Helper.ModContent.GetInternalAssetName("assets/pipes").Name,
                    SpriteIndex = 1,
                    CanBeGivenAsGift = false,
                    CustomFields = new Dictionary<string, string>()
                    {
                        {FluidData.LiquidKey, "wat"}
                    }
                    
                };
                data["Jok.FluidPipes.Oil"] = new ObjectData()
                {
                    Name = "Jok.FluidPipes.Oil",
                    DisplayName = I18n.Oil_Name(),
                    Description = I18n.Oil_Description(),
                    Type = "Crafting",
                    Category = -29, // equipment
                    Texture = Helper.ModContent.GetInternalAssetName("assets/pipes").Name,
                    SpriteIndex = 2,
                    CanBeGivenAsGift = false,
                    CustomFields = new Dictionary<string, string>()
                    {
                        {FluidData.LiquidKey, "wat"}
                    }
                };
                
                data["Jok.FluidPipes.FluidContainer"] = new ObjectData()
                {
                    Name = "Jok.FluidPipes.FluidContainer",
                    DisplayName = "Fluid Container",
                    Description = "You shouldn't get this",
                    Type = "Crafting",
                    Category = -29, // equipment
                    Texture = Helper.ModContent.GetInternalAssetName("assets/pipes").Name,
                    SpriteIndex = 1,
                    CanBeGivenAsGift = false,
                    CustomFields = new Dictionary<string, string>()
                    {
                        {FluidData.LiquidKey, "wat"}
                    }
                    
                };
                
                foreach (var (itemid, objdata) in data)
                {
                    if (objdata != null && objdata.CustomFields != null && objdata.CustomFields.ContainsKey(FluidData.LiquidKey))
                    {
                        objdata.CustomFields.TryAdd("Jok.Stardio.NoGrabby", "");
                    }
                }
            });
        }
    }
}
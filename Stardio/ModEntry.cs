using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using Object = StardewValley.Object;

namespace Stardio;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    public static Mod Instance;
    public static IMonitor MonitorInst;
    public static IModHelper Helper;

    public static int BeltAnim = 0;
    private static int beltUpdateCountdown = 0;
    public static int TotalBeltTime = 100; // TODO config with lower bound
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

        Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        Helper.Events.Content.AssetRequested += OnAssetRequested;
        Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        Helper.Events.Input.ButtonPressed += OnButtonPressed;

        ItemRegistry.AddTypeDefinition(new BeltItemDataDefinition());
        Helper.ModContent.Load<Texture2D>("assets/belts");

        HarmonyPatches.Patch(ModManifest.UniqueID);
    }
    
    //TODO extract some beltitem update logic into functions
    //TODO crafting recipe/unlock
    //TODO bigger machines

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button == SButton.R) // TODO keybind config
        {
            if (Game1.player.ActiveItem is BeltItem belt)
            {
                belt.rotate();
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
        beltUpdateCountdown -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;

        if (beltUpdateCountdown <= 0)
        {
            beltUpdateCountdown = TotalBeltTime / 2;
            BeltAnim++;

            if (BeltAnim > 3)
            {
                BeltAnim = 0;
            }

            isProcessTick = true;
        }

        if (Game1.currentLocation == null)
        {
            return;
        }

        foreach (Object obj in Game1.player.currentLocation.objects.Values)
        {
            if (obj is BeltItem belt)
            {
                belt.beltUpdate(isProcessTick);
            }
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var sc = Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
        sc.RegisterSerializerType(typeof(BeltItem));
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
    }
}
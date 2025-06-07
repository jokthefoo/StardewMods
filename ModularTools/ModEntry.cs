using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using GenericModConfigMenu;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Enchantments;
using StardewValley.GameData.Tools;
using StardewValley.GameData.WildTrees;
using StardewValley.Internal;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Object = StardewValley.Object;

namespace ModularTools
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        public static Mod Instance;
        public static IMonitor MonitorInst;
        //  debug logging:  MonitorInst.Log($"X value: {x}", LogLevel.Info);
        public static IModHelper Helper;
        public static Texture2D UpgradeTextures;
        private static string shouldWaterDirtKey = "Jok.ModularTools.WaterDirt";
        private static bool PrismaticTools = false;
        
        public static void Debug(string str)
        {
            MonitorInst.Log($"Debug: {str}", LogLevel.Warn);
        }
        
        public override void Entry(IModHelper helper)
        {
            Instance = this;
            MonitorInst = Monitor;
            Helper = helper;
            I18n.Init(Helper.Translation);
            
            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            Helper.Events.Content.AssetRequested += OnAssetRequested;
            Helper.Events.GameLoop.DayStarted += OnDayStarted;
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.ConsoleCommands.Add("modulartools_attachment_slots_reset", "Resets all tools to their default attachment slot count for the mod (1 per upgrade).\n\nUsage: modulartools_attachment_slots_reset", UpdateAttachmentSlotsForTools);
            Helper.ConsoleCommands.Add("modulartools_remove_all_attachments", "WARNING Removes all attachment slots (and attached upgrades) from all tools. Should only be used when removing mod mid-playthrough.\n\nUsage: modulartools_remove_all_attachments", RemoveAllAttachmentsFromTools);

            var def = new ModularUpgradeDefinition();
            ItemRegistry.ItemTypes.Add(def);
            Helper.Reflection.GetField<Dictionary<string, IItemDataDefinition>>(typeof(ItemRegistry), "IdentifierLookup").GetValue()[def.Identifier] = def;
            
            UpgradeTextures = Helper.ModContent.Load<Texture2D>("Assets/modularupgrades.png");
            Config = helper.ReadConfig<ModConfig>();
            HarmonyPatches();
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (!Context.IsMainPlayer)
            {
                return;
            }
            
            if (!Game1.player.mailReceived.Contains("Jok.ModularTools.Started"))
            {
                UpdateAttachmentSlotsForTools("", new []{""});
                Game1.player.mailReceived.Add("Jok.ModularTools.Started");
            }
        }
        
        private void RemoveAllAttachmentsFromTools(string command, string[] args)
        {
            Utility.ForEachItem(item =>
            {
                try
                {
                    if (item is Tool tool && IsAllowedTool(tool))
                    {
                        tool.AttachmentSlotsCount = 0;
                    }
                }
                catch (Exception e)
                {
                    Monitor.Log("Removing all tool attachments in multiplayer!", LogLevel.Warn);
                }
                return true;
            });
        }

        private void UpdateAttachmentSlotsForTools(string command, string[] args)
        {
            Utility.ForEachItemContext((in ForEachItemContext context) =>
            {
                if (context.Item is not null)
                {
                    if (context.Item is Tool tool && IsAllowedTool(tool))
                    {
                        Tool newTool = (Tool)ItemRegistry.Create(context.Item.ItemId);
                        newTool.CopyEnchantments(tool, newTool);
                        context.ReplaceItemWith(newTool);
                    }
                }
                return true;
            });
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            foreach (var location in Game1.locations)
            {
                location.ForEachDirt(delegate(HoeDirt dirt)
                {
                    if (dirt.modData.TryGetValue(shouldWaterDirtKey, out string value))
                    {
                        if (dirt.Pot != null)
                        {
                            dirt.Pot.Water();
                        }
                        else
                        {
                            dirt.state.Value = 1;
                        }
                        dirt.modData.Remove(shouldWaterDirtKey);
                    }
                    return true;
                });
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var sc = Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
            sc.RegisterSerializerType(typeof(ModularUpgradeItem));
            
            PrismaticTools = Helper.ModRegistry.IsLoaded("iargue.PrismaticToolsContinued");
            
            SetupModConfigs();
        }
         private void SetupModConfigs()
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            // add config options
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => I18n.Modularupgrade_Config_Clint_Name(),
                tooltip: () => I18n.Modularupgrade_Config_Clint_Description(),
                getValue: () => Config.ClintDiscount,
                setValue: value => Config.ClintDiscount = value
            );
        }
        public static ModConfig Config { get; set; }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/ModularUpgrades"))
            {
                e.LoadFrom(() =>
                {
                    var modAssets =
                        Helper.ModContent.Load<Dictionary<string, ModularUpgradeData>>(
                            "assets/modularupgrade_data.json");
                    Dictionary<string, ModularUpgradeData> ret = new();
                    foreach (string upgrade in modAssets.Keys)
                    {
                        ret.Add(upgrade,
                            new ModularUpgradeData()
                            {
                                TextureIndex = modAssets[upgrade].TextureIndex,
                                DisplayName = I18n.GetByKey(modAssets[upgrade].DisplayName),
                                Description = I18n.GetByKey(modAssets[upgrade].Description),
                                Price = modAssets[upgrade].Price,
                                AllowedTools = modAssets[upgrade].AllowedTools
                            });
                    }

                    return ret;
                }, AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo($"{ModManifest.UniqueID}/modularupgrades.png"))
            {
                e.LoadFromModFile<Texture2D>("assets/modularupgrades.png", AssetLoadPriority.Low);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("spacechase0.SpaceCore/ObjectExtensionData"))
            {
                e.Edit((asset) =>
                {
                    var modAssets =
                        Helper.ModContent.Load<Dictionary<string, ModularUpgradeData>>(
                            "assets/modularupgrade_data.json");
                    var data = asset.AsDictionary<string, SpaceCore.VanillaAssetExpansion.ObjectExtensionData>().Data;
                    foreach (string upgrade in modAssets.Keys)
                    {
                        data.Add(upgrade, new()
                        {
                            MaxStackSizeOverride = 1,
                            CategoryTextOverride = I18n.Modularupgrade_Category_Text(),
                            CategoryColorOverride = Color.LimeGreen
                        });
                    }
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit(asset =>
                {
                    var dict = asset.AsDictionary<string, string>().Data;
                    
                    // ingredients / unused / yield / big craftable? / unlock conditions /
                    dict.Add(MUQIds.Width, $"334 2 388 15/what/{MUQIds.Width}/false/s Farming 2/");
                    dict.Add(MUQIds.Length, $"334 2 388 15/what/{MUQIds.Length}/false/s Farming 2/");
                    dict.Add(MUQIds.Capacity, $"334 2 390 15/what/{MUQIds.Capacity}/false/s Farming 2/");
                    dict.Add(MUQIds.Power, $"334 2 390 15/what/{MUQIds.Power}/false/s Mining 2/");

                    dict.Add(MUQIds.Water, $"336 2 371 5/what/{MUQIds.Water}/false/s Farming 7/");
                    dict.Add(MUQIds.Speed, $"335 2 60 1/what/{MUQIds.Speed}/false/s Mining 4/");
                    dict.Add(MUQIds.Luck, $"335 2 CaveJelly 1/what/{MUQIds.Luck}/false/s Fishing 4/");
                    dict.Add(MUQIds.Air, $"337 2 253 5/what/{MUQIds.Air}/false/s Combat 5/");
                    dict.Add(MUQIds.Fire, $"336 2 382 15 82 1/what/{MUQIds.Fire}/false/s Foraging 3/");
                    dict.Add(MUQIds.Earth, $"335 2 368 15/what/{MUQIds.Earth}/false/s Farming 4/");
                    
                    dict.Add(MUQIds.WidthHeight, $"337 5 {MUQIds.Width} 1 {MUQIds.Length} 1/what/{MUQIds.WidthHeight}/false/s Farming 9/");
                    dict.Add(MUQIds.Power2, $"337 5 {MUQIds.Power} 2/what/{MUQIds.Power2}/false/s Foraging 9/");
                    dict.Add(MUQIds.Air2, $"74 1 {MUQIds.Air} 2/what/{MUQIds.Air2}/false/s Combat 9/");
                    dict.Add(MUQIds.Speed2, $"337 5 {MUQIds.Speed} 2/what/{MUQIds.Speed2}/false/s Mining 9/");
                    dict.Add(MUQIds.Luck2, $"337 5 {MUQIds.Luck} 2/what/{MUQIds.Luck2}/false/s Fishing 9/");
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Tools"))
            {
                e.Edit(asset =>
                {
                    var dict = asset.AsDictionary<string, ToolData>().Data;
                    foreach (var (key, toolData) in dict)
                    {
                        if (toolData.ClassName is "Pickaxe" or "Hoe" or "WateringCan" or "Axe")
                        {
                            int count = toolData.UpgradeLevel > 5 ? 5 : toolData.UpgradeLevel;
                            toolData.AttachmentSlots = count;
                        }
                    }
                }, priority: AssetEditPriority.Late + 1337);
            }
        }

        private void HarmonyPatches()
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            
            // Attaching
            Type[] types = { typeof(int), typeof(SpriteBatch), typeof(int), typeof(int) };
            var originalToolsMethod = typeof(Tool).GetMethod("DrawAttachmentSlot",
                BindingFlags.Instance | BindingFlags.NonPublic, null, types, null);
            harmony.Patch(
                original: originalToolsMethod,
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DrawAttachmentSlot_prefix)));

            Type[] drawHoverTypes =
            {
                typeof(SpriteBatch), typeof(StringBuilder), typeof(SpriteFont), typeof(int), typeof(int), typeof(int),
                typeof(string), typeof(int), typeof(string[]), typeof(Item), typeof(int), typeof(string), typeof(int),
                typeof(int), typeof(int), typeof(float), typeof(CraftingRecipe), typeof(IList<Item>), typeof(Texture2D),
                typeof(Rectangle), typeof(Color), typeof(Color), typeof(float), typeof(int), typeof(int) };
            harmony.Patch(
                original: AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.drawHoverText),
                    drawHoverTypes),
                transpiler: new HarmonyMethod(typeof(ModEntry),
                    nameof(IClickableMenu_drawHoverTextTranspiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.canThisBeAttached),
                    new Type[] { typeof(Object), typeof(int) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(canThisBeAttached_postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.attach), new Type[] { typeof(Object) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(attachOrDetach_postfix)));
            
            // Upgrade tool
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.actionWhenPurchased), new Type[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ToolActionWhenPurchased_prefix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.actionWhenPurchased), new Type[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ToolActionWhenPurchased_postfix)));
            
            var GetOneCopyFromTool = typeof(Tool).GetMethod("GetOneCopyFrom",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Item) }, null);
            harmony.Patch(
                original: GetOneCopyFromTool,
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ToolGetOneCopyFrom_postfix)));

            if (Helper.ModRegistry.IsLoaded("Digus.MailServicesMod"))
            {
                try { 
                    var IMailServices = AccessTools.TypeByName("MailServicesMod.ToolUpgradeController");
                    harmony.Patch(
                        original: IMailServices.GetMethod("TryToSendTool",  BindingFlags.Static | BindingFlags.NonPublic),
                        prefix: new HarmonyMethod(typeof(ModEntry), nameof(MailServicesMod_prefix)));
                    harmony.Patch(
                        original: IMailServices.GetMethod("TryToSendTool",  BindingFlags.Static | BindingFlags.NonPublic),
                        postfix: new HarmonyMethod(typeof(ModEntry), nameof(MailServicesMod_postfix)));
                } catch { }
            }
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.draw)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ToolDraw_prefix))
            );
            
            // Luck, and Speed Upgrades
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.DoFunction), new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModEntry), nameof(ToolDoFunction_prefix))));

            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModEntry), nameof(ToolDoFunction_postfix))));
            
            // Power upgrades
            harmony.Patch(
                original: AccessTools.Method(typeof(Pickaxe), nameof(Pickaxe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(PickaxeDoFunction_prefix)));
            harmony.Patch(
                original: AccessTools.Method(typeof(Pickaxe), nameof(Pickaxe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(PickaxeDoFunction_postfix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Axe), nameof(Axe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(AxeDoFunction_prefix)));
            harmony.Patch(
                original: AccessTools.Method(typeof(Axe), nameof(Axe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(AxeDoFunction_postfix)));
            
            // Range upgrade
            Type[] tileAffectedTypes = { typeof(Vector2), typeof(int), typeof(Farmer) };
            var originalTilesAffected = typeof(Tool).GetMethod("tilesAffected",
                BindingFlags.Instance | BindingFlags.NonPublic, null, tileAffectedTypes, null);
            harmony.Patch(
                original: originalTilesAffected,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModEntry), nameof(Post_tilesAffected)), priority: Priority.Last));

            // Water upgrade
            harmony.Patch(
                original: AccessTools.Method(typeof(HoeDirt), nameof(HoeDirt.performToolAction)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(HoeDirt_performToolAction_postfix))
            );
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Hoe), nameof(Hoe.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(Hoe_DoFunctionTranspiler)));
            
            // Fire Upgrade
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.OnStoneDestroyed),
                    new Type[] { typeof(string), typeof(int), typeof(int), typeof(Farmer) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(OnStoneDestroyed_postfix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Tree), nameof(Tree.tickUpdate),
                    new Type[] { typeof(GameTime) }),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(Tree_tickUpdateTranspiler)));
            
            // Earth for axe
            Type[] performTreeFallTypes = { typeof(Tool), typeof(int), typeof(Vector2) };
            var originalTreeFall = typeof(Tree).GetMethod("performTreeFall",
                BindingFlags.Instance | BindingFlags.NonPublic, null, performTreeFallTypes, null);
            harmony.Patch(
                original: originalTreeFall,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModEntry), nameof(Tree_PerformTreeFall_postfix))));
            
            // Tool prices
            harmony.Patch(
                original: AccessTools.Method(typeof(ShopBuilder), nameof(ShopBuilder.GetToolUpgradeData),
                    new Type[] { typeof(ToolData), typeof(Farmer)}),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(GetToolUpgradeDataTranspiler)));
            
            // Tool forge 
            harmony.Patch(
                original: AccessTools.Method(typeof(BaseEnchantment), nameof(BaseEnchantment.GetEnchantmentFromItem),
                    new Type[] { typeof(Item), typeof(Item)}),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetEnchantmentFromItem_postfix)));
        }

        public static void ToolGetOneCopyFrom_postfix(Tool __instance, Item source)
        {
            if (!IsAllowedTool(source))
            {
                return;
            }

            if (source is Tool fromTool)
            {
                foreach (var o in fromTool.attachments)
                {
                    if (o is not null)
                    {
                        __instance.attach((Object)o.getOne());
                    }
                }
            }
        }
        
        public static void GetEnchantmentFromItem_postfix(BaseEnchantment __instance, ref BaseEnchantment __result, Item base_item, Item item)
        {
            if (IsAllowedTool(base_item) && item?.QualifiedItemId == "(O)896" && base_item is Tool tool && !tool.hasEnchantmentOfType<ModularToolsGalaxyEnchantment>())
            {
                __result = new ModularToolsGalaxyEnchantment();
            }
        }
        
        public static IEnumerable<CodeInstruction> GetToolUpgradeDataTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            CodeMatcher matcher = new(instructions, generator);
            PropertyInfo tradeItemAmount = AccessTools.Property(typeof(ToolUpgradeData), nameof(ToolUpgradeData.TradeItemAmount));
            MethodInfo clintUpgradeAmount = AccessTools.Method(typeof(ModEntry), nameof(ClintUpgradeBarAmount));

            try
            {
                return matcher.MatchStartForward(
                        new CodeMatch(OpCodes.Ldc_I4_5),
                        new CodeMatch(OpCodes.Stfld, tradeItemAmount),
                        new CodeMatch(OpCodes.Stelem_Ref),
                        new CodeMatch(OpCodes.Stloc_1),
                        new CodeMatch(OpCodes.Ldloc_0),
                        new CodeMatch(OpCodes.Brfalse_S)
                    )
                    .ThrowIfNotMatch($"Could not find tool entry point for {nameof(Tree_tickUpdateTranspiler)}")
                    .RemoveInstruction() // remove 5
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Call, clintUpgradeAmount) // load bar amount
                    ) 
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                MonitorInst.Log($"Failed in {nameof(GetToolUpgradeDataTranspiler)}:\n{ex}", LogLevel.Error);
                return matcher.InstructionEnumeration(); // run original logic
            }
        }
        
        public static int ClintUpgradeBarAmount()
        {
            if (Config.ClintDiscount)
            {
                return 3;
            }
            return 5; // default bar amount
        }
        
        public static IEnumerable<CodeInstruction> Tree_tickUpdateTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            CodeMatcher matcher = new(instructions, generator);
            MethodInfo typeToDrop = AccessTools.Method(typeof(ModEntry), nameof(AxeFireUpgradeDropType));
            MethodInfo amountToDrop = AccessTools.Method(typeof(ModEntry), nameof(AxeFireUpgradeDropAmount));
            
            try
            {
                return matcher.MatchStartForward(
                        new CodeMatch(OpCodes.Add),
                        new CodeMatch(OpCodes.Ldloc_1),
                        new CodeMatch(OpCodes.Ldfld),
                        new CodeMatch(OpCodes.Conv_I4),
                        new CodeMatch(OpCodes.Ldloc_S), // num to drop
                        new CodeMatch(OpCodes.Ldc_I4_1), // true for resource
                        new CodeMatch(OpCodes.Ldc_I4_M1),
                        new CodeMatch(OpCodes.Ldc_I4_0),
                        new CodeMatch(OpCodes.Ldloca_S)
                    )
                    .ThrowIfNotMatch($"Could not find tool entry point for {nameof(Tree_tickUpdateTranspiler)}")
                    .Advance(-11) // right before we load "12" for wood
                    .RemoveInstruction() // remove 12
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, 9), // load farmer for our call
                        new CodeInstruction(OpCodes.Call, typeToDrop) // load type of dropped resource
                    ).Advance(15)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, 9), // load farmer for our call, also stealing original amount
                        new CodeInstruction(OpCodes.Call, amountToDrop) // load amount of dropped resource
                    )
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                MonitorInst.Log($"Failed in {nameof(Tree_tickUpdateTranspiler)}:\n{ex}", LogLevel.Error);
                return matcher.InstructionEnumeration(); // run original logic
            }
        }
        
        public static int AxeFireUpgradeDropAmount(int amount, Farmer farmer)
        {
            if (!IsAllowedTool(farmer.CurrentTool))
            {
                return amount;
            }

            if (GetHasAttachmentQualifiedItemID(farmer.CurrentTool, MUQIds.Fire))
            {
                int coalAmount = amount / 10;
                return coalAmount == 0 ? 1 : coalAmount;
            }
            return amount;
        }
        
        public static int AxeFireUpgradeDropType(Farmer farmer)
        {
            const int wood = 12;
            const int coal = 4;
            if (!IsAllowedTool(farmer.CurrentTool))
            {
                return wood;
            }

            if (GetHasAttachmentQualifiedItemID(farmer.CurrentTool, MUQIds.Fire))
            {
                return coal;
            }
            return wood;
        }

        public static void Tree_PerformTreeFall_postfix(Tree __instance, Tool t, int explosion, Vector2 tileLocation)
        {
            if (__instance.stump.Value && __instance.health.Value < -99f && t != null && t.getLastFarmerToUse() != null && GetHasAttachmentQualifiedItemID(t,MUQIds.Earth))
            {
                TryToPlantTreeSeed(t.getLastFarmerToUse(), __instance, tileLocation);
            }
        }
        
        private static bool TryToPlantTreeSeed(Farmer who, Tree tree, Vector2 tileLocation)
        {
            Item? treeSeed = null;
            WildTreeData data = tree.GetData();
            foreach (var item in who.Items)
            {
                if (item is null)
                {
                    continue;
                }
                
                if (item.QualifiedItemId == data.SeedItemId || item.ItemId == data.SeedItemId)
                {
                    treeSeed = item;
                    break;
                }
            }

            if (treeSeed == null)
            {
                return false;
            }

            GameLocation environment = who.currentLocation;
            if (!environment.IsNoSpawnTile(tileLocation, "Tree") && environment.isTileLocationOpen(tileLocation))
            {
                treeSeed.Stack -= 1;
                if (treeSeed.Stack <= 0)
                {
                    who.removeItemFromInventory(treeSeed);
                }
                Game1.stats.Increment("wildtreesplanted");
                environment.terrainFeatures.Remove(tileLocation);
                environment.terrainFeatures.Add(tileLocation, new Tree(tree.treeType.Value, 0));
                environment.playSound("dirtyHit", null, null);
                return true;
            }
            return false;
        }

        public static void OnStoneDestroyed_postfix(GameLocation __instance, string stoneId, int x, int y, Farmer who)
        {
            Tool t = who.CurrentTool;
            if (!IsAllowedTool(t))
            {
                return;
            }

            if(GetAttachmentQualifiedItemID(t, MUQIds.Fire) is ModularUpgradeItem upgrade)
            {
                upgrade.fireCounter++;
                if (upgrade.fireCounter == 10)
                {
                    upgrade.fireCounter = 0;
                    int amount = 1;
                    if (Game1.random.NextDouble() < 0.2)
                    {
                        amount++;
                    }
                    Game1.createMultipleObjectDebris("(O)382", x, y, amount, who.UniqueMultiplayerID, __instance);
                }
            }
        }
        
        public static void HoeDirt_performToolAction_postfix(HoeDirt __instance, Tool t, int damage, Vector2 tileLocation)
        {
            if (!IsAllowedTool(t))
            {
                return;
            }
            
            if(t is WateringCan wateringCan && GetHasAttachmentQualifiedItemID(t, MUQIds.Water))
            {
                __instance.modData[shouldWaterDirtKey] = "Water";
                if (wateringCan.getLastFarmerToUse() != null)
                {
                    if (!wateringCan.IsBottomless)
                    {
                        wateringCan.WaterLeft -= 1;
                    }
                }
            }
        }
        
        public static void PickaxeDoFunction_prefix(Pickaxe __instance, out int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            __state = __instance.UpgradeLevel;
            __instance.UpgradeLevel = GetToolStrength(__instance);
        }
        
        public static void PickaxeDoFunction_postfix(Pickaxe __instance, int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            __instance.UpgradeLevel = __state;
        }

        public static void AxeDoFunction_prefix(Axe __instance, out int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            __state = __instance.UpgradeLevel;
            __instance.UpgradeLevel = GetToolStrength(__instance);
        }
        public static void AxeDoFunction_postfix(Axe __instance, int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            __instance.UpgradeLevel = __state;
        }
        
        public static void ToolDoFunction_prefix(Tool __instance, GameLocation location, int x, int y, int power, Farmer who)
        {
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            who.luckLevel.Value += Utility.getStringCountInList(GetAttachmentQualifiedItemIDs(__instance), MUQIds.Luck);
            who.luckLevel.Value += Utility.getStringCountInList(GetAttachmentQualifiedItemIDs(__instance), MUQIds.Luck2) * 2;
        }
        
        public static void ToolDoFunction_postfix(Tool __instance, GameLocation location, int x, int y, int power, Farmer who)
        {
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            who.luckLevel.Value -= Utility.getStringCountInList(GetAttachmentQualifiedItemIDs(__instance), MUQIds.Luck);
            who.luckLevel.Value -= Utility.getStringCountInList(GetAttachmentQualifiedItemIDs(__instance), MUQIds.Luck2) * 2;
            ApplyAirBuff(__instance, who);
        }

        private static void ApplyAirBuff(Tool tool, Farmer who)
        {
            int duration = 0;
            int speed = 2;
            if (GetHasAttachmentQualifiedItemID(tool, MUQIds.Air))
            {
                duration = 1000;
            } 
            
            if (GetHasAttachmentQualifiedItemID(tool, MUQIds.Air2))
            {
                duration = 1200;
                speed = 3;
            }

            if (duration != 0)
            {
                Buff buff = new Buff(
                    id: "Jok.ModularTools.AirSpeed",
                    displayName: I18n.Modularupgrade_Buff_Display_Name(),
                    iconTexture: Game1.buffsIcons,
                    iconSheetIndex: 9,
                    duration: duration, // milliseconds
                    effects: new BuffEffects()
                    {
                        Speed = { speed }
                    }
                );
                who.applyBuff(buff);
            }
            
        }
        
        private static int GetToolStrength(Tool tool)
        {
            int strength = 0;
            foreach (Object o in tool.attachments)
            {
                if (o is not null && o.QualifiedItemId == MUQIds.Power)
                {
                    strength++;
                } else if (o is not null && o.QualifiedItemId == MUQIds.Power2)
                {
                    strength += 2;
                }
            }
            
            if (strength >= 5) // super boost past iridium
            {
                strength = 25;
            }

            return strength;
        }
        
        public static Object GetAttachmentQualifiedItemID(Tool tool, string id)
        {
            foreach (Object o in tool.attachments)
            {
                if (o != null && o.QualifiedItemId == id)
                {
                    return o;
                }
            }
            return null;
        }
        
        public static bool GetHasAttachmentQualifiedItemID(Tool tool, string id)
        {
            foreach (Object o in tool.attachments)
            {
                if (o != null && o.QualifiedItemId == id)
                {
                    return true;
                }
            }
            return false;
        }
        
        public static List<string> GetAttachmentQualifiedItemIDs(Tool tool)
        {
	        List<string> ids = new List<string>();
	        foreach (Object o in tool.attachments)
	        {
		        if (o != null)
		        {
			        ids.Add(o.QualifiedItemId);
		        }
	        }
	        return ids;
        }
        
        public static bool ToolDraw_prefix(Tool __instance, SpriteBatch b)
        {
            if (!IsAllowedTool(__instance))
            {
                return true;
            }

            if (!GetHasAttachmentQualifiedItemID(__instance,MUQIds.Water))
            {
                return true;
            }

            Farmer lastUser = __instance.lastUser;
            if (lastUser == null || !__instance.lastUser.canReleaseTool || !__instance.lastUser.IsLocalPlayer)
            {
                return true;
            }

            List<Vector2> tilesAffected = Helper.Reflection.GetMethod(__instance, "tilesAffected").Invoke<List<Vector2>>(__instance.lastUser.GetToolLocation() / 64f,
                __instance.lastUser.toolPower.Value, __instance.lastUser);
            foreach (var vector2 in tilesAffected)
                b.Draw(UpgradeTextures, Game1.GlobalToLocal(new Vector2((int)vector2.X * 64, (int)vector2.Y * 64)),
                    new Rectangle(48, 48, 16, 16), Color.White, 0.0f, Vector2.Zero, 4f, SpriteEffects.None, 0.01f);

            return false;
        }

        public static void Post_tilesAffected(Tool __instance, ref List<Vector2> __result, Vector2 tileLocation, int power, Farmer who)
        {
	        if (__instance == null)
	        {
		        return;
	        }

            if (!IsAllowedTool(__instance))
            {
                return;
            }
            
            List<Vector2> tileLocations = new List<Vector2>();
            tileLocations.Add(tileLocation);
            
            List<string> attachments = GetAttachmentQualifiedItemIDs(__instance);
            if (power == 1)
            {
                return;
            }

	        int widthAttachCount = Utility.getStringCountInList(attachments, MUQIds.Width);
            int heightAttachCount = Utility.getStringCountInList(attachments, MUQIds.Length);
            int aoeAttachCount = Utility.getStringCountInList(attachments, MUQIds.WidthHeight);

            if (__instance.hasEnchantmentOfType<ReachingToolEnchantment>())
            {
                aoeAttachCount++;
            }

	        if (widthAttachCount + heightAttachCount + aoeAttachCount == 0)
            {
                __result = tileLocations;
		        return;
	        }
            
	        Vector2 offset = Vector2.Zero;
	        switch (who.FacingDirection)
	        {
		        case 0:
			        offset = new Vector2(0f, -1f);
			        break;
		        case 1:
			        offset = new Vector2(1f, 0f);
			        break;
		        case 2:
			        offset = new Vector2(0f, 1f);
			        break;
		        case 3:
			        offset = new Vector2(-1f, 0f);
			        break;
	        }
            
            Vector2 left = new Vector2(offset.Y, -offset.X);
            Vector2 right = new Vector2(-offset.Y, offset.X);
            
            int wCount = 0;
            int hCount = 0;
            int aoeCount = 0;
            foreach (string s in attachments)
            {
                if (s == MUQIds.Width)
                {
                    wCount++;
                } 
                else if (s == MUQIds.Length)
                {
                    hCount++;
                } 
                else if (s == MUQIds.WidthHeight)
                {
                    aoeCount++;
                }
                
                if (aoeCount + wCount + hCount >= power-1)
                {
                    break;
                }
            }

            if (__instance.hasEnchantmentOfType<ReachingToolEnchantment>() && power == 6)
            {
                aoeCount++;
            }

            wCount += aoeCount;
            hCount += aoeCount;

            int multiplier = 1;
            if (wCount == 0)
            {
                multiplier = hCount;
            }
            if (hCount == 0)
            {
                multiplier = wCount;
            }
            
            for (int i = 1; i <= wCount * multiplier; i++)
            {
                tileLocations.Add(tileLocation + left * i);
                tileLocations.Add(tileLocation + right * i);
            }

            int tilesCount = tileLocations.Count - 1;
            for (int i = 1; i <= hCount * 2 * multiplier; i++)
            {
                for (int i2 = 0; i2 <= tilesCount; i2++)
                {
                    tileLocations.Add(tileLocations[i2] + offset * i);
                }
            }
            
	        __result = tileLocations;
        }

        public static bool IsAllowedTool(Item tool)
        {
            return tool is WateringCan or Hoe or Pickaxe or Axe;
        }
        
        public static void MailServicesMod_prefix(out Tool __state)
        {
            __state = null;
            if (!IsAllowedTool(Game1.player.CurrentTool))
            {
                return;
            }
            __state = Game1.player.CurrentTool;
        }
        
        public static void MailServicesMod_postfix(Tool __state)
        {
            Tool tool = Game1.player.toolBeingUpgraded.Value;
            if (!IsAllowedTool(__state) || tool == null)
            {
                return;
            }

            if (tool is WateringCan wateringCan)
            {
                wateringCan.waterCanMax = 40;
                wateringCan.WaterLeft = 40;
            }
            //tool.AttachmentSlotsCount = __state.AttachmentSlotsCount + 1;
            foreach (Object o in __state.attachments)
            {
                if (o is not null)
                {
                    tool.attach((Object)o.getOne());
                }
            }
        }
        
        public static void ToolActionWhenPurchased_prefix(Tool __instance, out Tool __state, string shopId)
        {
            __state = null;
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            
            string previousToolId = ShopBuilder.GetToolUpgradeData(__instance.GetToolData(), Game1.player)?.RequireToolId;
            if (previousToolId != null)
            {
                if (Game1.player.Items.GetById(previousToolId).FirstOrDefault() is Tool tool)
                {
                    __state = tool;
                }
            }
        }

        public static void ToolActionWhenPurchased_postfix(Tool __instance, Tool __state, string shopId)
        {
            if (!IsAllowedTool(__state))
            {
                return;
            }

            Tool tool = Game1.player.toolBeingUpgraded.Value;

            if (tool is WateringCan wateringCan)
            {
                wateringCan.waterCanMax = 40;
                wateringCan.WaterLeft = 40;
            }
            //tool.AttachmentSlotsCount = __state.AttachmentSlotsCount + 1;
            foreach (Object o in __state.attachments)
            {
                if (o is not null)
                {
                    tool.attach((Object)o.getOne());
                }
            }
        }
        
        private static int GetWateringCanCapacity(Tool tool)
        {
            const int wateringCapacityIncrease = 40;
            int Capacity = 40;
            foreach (Object o in tool.attachments)
            {
                if (o is not null && o.QualifiedItemId == MUQIds.Capacity)
                {
                    Capacity += wateringCapacityIncrease;
                }
            }
            return Capacity;
        }
        
        private static void SetToolSpeed(Tool tool)
        {

            float speed = 1;
            if (tool.hasEnchantmentOfType<SwiftToolEnchantment>())
            {
                speed = 0.66f;
            }
            
            const float speedStrength = .9f;
            const float speed2Strength = .8f;
            foreach (Object o in tool.attachments)
            {
                if (o is not null && o.QualifiedItemId == MUQIds.Speed)
                {
                    speed *= speedStrength;
                } 
                else if (o is not null && o.QualifiedItemId == MUQIds.Speed2)
                {
                    speed *= speed2Strength;
                }
            }
            tool.AnimationSpeedModifier = speed;
        }
        
        public static void attachOrDetach_postfix(Tool __instance, ref Object __result, Object o)
        {
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            
            if (__instance is WateringCan wateringCan && !__instance.hasEnchantmentOfType<BottomlessEnchantment>())
            {
                wateringCan.waterCanMax = GetWateringCanCapacity(__instance);
                if (wateringCan.WaterLeft > wateringCan.waterCanMax) // reduce water if we have a new lower capacity
                {
                    wateringCan.WaterLeft = wateringCan.waterCanMax;
                }
            }
            SetToolSpeed(__instance);
        }

        public static void canThisBeAttached_postfix(Tool __instance, ref bool __result, Object o, int slot)
        {
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            
            if(o is ModularUpgradeItem moditem)
            {
                __result = moditem.CanThisBeAttached(__instance);
            }
            else
            {
                __result = false;
            }
        }
        
        
        private const int slotsInRow = 2;
        public static int AdjustHoverMenuHeight(Item hoveredItem)
        {
            int slots = hoveredItem.attachmentSlots();
            if (IsAllowedTool(hoveredItem))
            {
                if (slots > 0)
                {
                    int enchantInc = 4;
                    if (((Tool)hoveredItem).enchantments.Count > 0)
                    {
                        enchantInc += 4;
                    }

                    if (slots % slotsInRow == 0)
                    {
                        return 68 * slots / slotsInRow + enchantInc;
                    }
                    return 68 * (slots + slotsInRow - slots % slotsInRow) / slotsInRow + enchantInc;
                }
            }
            return 68 * slots;
        }
        
        public static IEnumerable<CodeInstruction> IClickableMenu_drawHoverTextTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            CodeMatcher matcher = new(instructions, generator);
            MethodInfo attachmentSlots = AccessTools.PropertyGetter(typeof(Item), nameof(Item.attachmentSlots));
            MethodInfo adjustHeight = AccessTools.Method(typeof(ModEntry), nameof(AdjustHoverMenuHeight));
            
            try
            {
                // Transpile height for tools
                matcher.MatchStartForward(
                        new CodeMatch(OpCodes.Ldloc_2), // load height
                        new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)68), // 68 
                        new CodeMatch(OpCodes.Ldarg_S, (byte)9), // hover item
                        new CodeMatch(OpCodes.Callvirt, attachmentSlots), // get hover item slot count
                        new CodeMatch(OpCodes.Mul), // 68 * slot count
                        new CodeMatch(OpCodes.Add), // add to height
                        new CodeMatch(OpCodes.Stloc_2) // store into height
                    )
                    .ThrowIfNotMatch($"Could not find tool entry point for {nameof(IClickableMenu_drawHoverTextTranspiler)}")
                    .Advance(1)
                    .RemoveInstruction() // remove 68
                    .Advance(1)
                    .RemoveInstruction() // remove get attach slots
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Call, adjustHeight) // returns int height
                    ).RemoveInstruction(); // remove mul
            }
            catch (Exception ex)
            {
                MonitorInst.Log($"Failed in {nameof(IClickableMenu_drawHoverTextTranspiler)}:\n{ex}", LogLevel.Error);
            }

            /*
            MethodInfo getToolForgeLevels = AccessTools.Method(typeof(Tool), nameof(Tool.GetTotalForgeLevels));
            var myJump = generator.DefineLabel();
            // Transpile height for weapons
            matcher.MatchStartForward(
                new CodeMatch(OpCodes.Ldloc_S), // grab the weapon, (byte)19
                new CodeMatch(OpCodes.Ldc_I4_0), // push a 0
                new CodeMatch(OpCodes.Callvirt, getToolForgeLevels), // , getToolForgeLevels
                new CodeMatch(OpCodes.Ldc_I4_0), // push a 0
                new CodeMatch(OpCodes.Ble_S) // if forge <= 0 skip next section
            )
            .ThrowIfNotMatch($"Could not find weapon entry point for {nameof(IClickableMenu_drawHoverTextTranspiler)}")
            .Advance(1)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_S, (byte)9), // load hover item
                new CodeInstruction(OpCodes.Call, adjustHeight), // returns int height
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Add),
                new CodeInstruction(OpCodes.Stloc_2)
            );*/

            return matcher.InstructionEnumeration();
        }
        
        public static void HoeWateringAndFertilizer(Farmer who, Vector2 tileLocation)
        {
            if (!IsAllowedTool(who.CurrentTool))
            {
                return;
            }

            if (who.currentLocation.terrainFeatures.TryGetValue(tileLocation, out TerrainFeature terrainFeature) && terrainFeature is HoeDirt dirt)
            {
                if (GetHasAttachmentQualifiedItemID(who.CurrentTool, MUQIds.Water))
                {
                    dirt.state.Value = 1;
                    if (!who.CurrentTool.isEfficient.Value)
                    {
                        who.Stamina -= 1 - who.FarmingLevel * 0.1f;
                    }
                }
                
                if (GetHasAttachmentQualifiedItemID(who.CurrentTool, MUQIds.Earth))
                {
                    if (TryToApplyFertilizer(who, dirt) && !who.CurrentTool.isEfficient.Value)
                    {
                        who.Stamina -= 1 - who.FarmingLevel * 0.1f;
                    }
                }
            }
        }

        private static bool TryToApplyFertilizer(Farmer who, HoeDirt dirt)
        {
            Item? fertilizer = null;
            foreach (var item in who.Items)
            {
                if (item is null)
                {
                    continue;
                }
                
                if (item.Category == Object.fertilizerCategory)
                {
                    fertilizer = item;
                    break;
                }
            }

            if (fertilizer == null || !dirt.CanApplyFertilizer(fertilizer.QualifiedItemId))
            {
                return false;
            }
            
            if (dirt.plant(fertilizer.ItemId, who, isFertilizer: true))
            {
                fertilizer.Stack -= 1;

                if (fertilizer.Stack <= 0)
                {
                    who.removeItemFromInventory(fertilizer);
                }
                return true;
            }
            return false;
        }
        
        public static IEnumerable<CodeInstruction> Hoe_DoFunctionTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            CodeMatcher matcher = new(instructions, generator);
            MethodInfo tilePassable = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isTilePassable),  new[] { typeof(xTile.Dimensions.Location), typeof(xTile.Dimensions.Rectangle) });
            MethodInfo makeHoeDirt = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.makeHoeDirt));
            MethodInfo hoeWatering = AccessTools.Method(typeof(ModEntry), nameof(HoeWateringAndFertilizer));

            try
            {
                return matcher.MatchStartForward(
                        new CodeMatch(OpCodes.Callvirt, tilePassable),
                        new CodeMatch(OpCodes.Brfalse),
                        new CodeMatch(OpCodes.Ldarg_1),
                        new CodeMatch(OpCodes.Ldloc_S), // tileLocation
                        new CodeMatch(OpCodes.Ldc_I4_0),
                        new CodeMatch(OpCodes.Callvirt, makeHoeDirt),
                        new CodeMatch(OpCodes.Brfalse)
                    )
                    .ThrowIfNotMatch($"Could not find tool entry point for {nameof(Hoe_DoFunctionTranspiler)}")
                    .Advance(7)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_S, (byte)5), // Farmer
                        new CodeInstruction(OpCodes.Ldloc_S, (byte)4), // tileLocation
                        new CodeInstruction(OpCodes.Call, hoeWatering)
                    ).InstructionEnumeration();
            }
            catch (Exception ex)
            {
                MonitorInst.Log($"Failed in {nameof(Hoe_DoFunctionTranspiler)}:\n{ex}", LogLevel.Error);
                return matcher.InstructionEnumeration(); // run original logic
            }
        }
        
        internal static bool DrawAttachmentSlot_prefix(Tool __instance, int slot, SpriteBatch b, int x, int y)
        {
            if (!IsAllowedTool(__instance))
            {
                return true;
            }
            
            y -= slot * 68;
            
            x += slot % slotsInRow * 68;
            y += slot / slotsInRow * 68;
            
            Vector2 pixel = new Vector2(x, y);
            Texture2D texture = Game1.menuTexture;
            Rectangle sourceRect = Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 10);
            b.Draw(texture, pixel, sourceRect, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.86f);

            if (__instance.attachments.Length > slot)
            {
                __instance.attachments[slot]?.drawInMenu(b, pixel, 1f);
            }
            
            return false;
        }
    }
}
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceShared.APIs;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Enchantments;
using StardewValley.GameData.Objects;
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
        
        public override void Entry(IModHelper helper)
        {
            Instance = this;
            MonitorInst = Monitor;
            Helper = helper;
            I18n.Init(Helper.Translation);
            
            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            Helper.Events.Content.AssetRequested += OnAssetRequested;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.GameLoop.DayStarted += OnDayStarted;
            
            var def = new ModularUpgradeDefinition();
            ItemRegistry.ItemTypes.Add(def);
            Helper.Reflection.GetField<Dictionary<string, IItemDataDefinition>>(typeof(ItemRegistry), "IdentifierLookup").GetValue()[def.Identifier] = def;
            
            UpgradeTextures = Helper.ModContent.Load<Texture2D>("Assets/modularupgrades.png");
            
            HarmonyPatches();
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
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button == SButton.C)
            {
                if (Game1.player.CurrentTool is not null)
                {
                    Game1.player.CurrentTool.AttachmentSlotsCount += 1;
                }
            }
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
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
                    
                    
                    dict.Add(MUQIds.WidthHeight, $"337 5 {MUQIds.Width} 1 {MUQIds.Length} 1/what/{MUQIds.WidthHeight}/false/s Farming 9/");
                    dict.Add(MUQIds.Power2, $"337 5 {MUQIds.Power} 2/what/{MUQIds.Power2}/false/s Foraging 9/");
                    dict.Add(MUQIds.Air2, $"74 1 {MUQIds.Air} 2/what/{MUQIds.Air2}/false/s Combat 9/");
                    dict.Add(MUQIds.Speed2, $"337 5 {MUQIds.Speed} 2/what/{MUQIds.Speed2}/false/s Mining 9/");
                    dict.Add(MUQIds.Luck2, $"337 5 {MUQIds.Luck} 2/what/{MUQIds.Luck2}/false/s Fishing 9/");
                });
            }
        }

        // TODO dont need to patch for this but it is interesting for magic mod: drawPlacementBounds --- isPlaceable
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
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.actionWhenPurchased), new Type[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ToolActionWhenPurchased_prefix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.actionWhenPurchased), new Type[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ToolActionWhenPurchased_postfix)));
            
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.draw)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ToolDraw_prefix))
            );
            
            // Luck, Power, and Speed Upgrades
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ToolDoFunction_prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.DoFunction),
                    new Type[] { typeof(GameLocation), typeof(int), typeof(int), typeof(int), typeof(Farmer) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ToolDoFunction_postfix)));
            
            // Range upgrade
            Type[] tileAffectedTypes = { typeof(Vector2), typeof(int), typeof(Farmer) };
            var originalTilesAffected = typeof(Tool).GetMethod("tilesAffected",
                BindingFlags.Instance | BindingFlags.NonPublic, null, tileAffectedTypes, null);
            harmony.Patch(
                original: originalTilesAffected,
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Post_tilesAffected)));

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
        }
        
        public static IEnumerable<CodeInstruction> Tree_tickUpdateTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            CodeMatcher matcher = new(instructions, generator);
            MethodInfo typeToDrop = AccessTools.Method(typeof(ModEntry), nameof(AxeFireUpgradeDropType));
            MethodInfo amountToDrop = AccessTools.Method(typeof(ModEntry), nameof(AxeFireUpgradeDropAmount));

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
                    wateringCan.getLastFarmerToUse().stamina -= 1 - wateringCan.getLastFarmerToUse().FarmingLevel * 0.1f;
                    if (!wateringCan.IsBottomless)
                    {
                        wateringCan.WaterLeft -= 1;
                    }
                }
            }
        }
        
        public static void ToolDoFunction_prefix(Tool __instance, out int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            __state = __instance.UpgradeLevel;
            if (!IsAllowedTool(__instance))
            {
                return;
            }

            if (GetHasAttachmentQualifiedItemID(__instance, MUQIds.Power) || GetHasAttachmentQualifiedItemID(__instance, MUQIds.Power2))
            {
                __instance.UpgradeLevel = GetToolStrength(__instance);
            }
            who.luckLevel.Value += Utility.getStringCountInList(GetAttachmentQualifiedItemIDs(__instance), MUQIds.Luck);
            who.luckLevel.Value += Utility.getStringCountInList(GetAttachmentQualifiedItemIDs(__instance), MUQIds.Luck2) * 2;
        }
        
        public static void ToolDoFunction_postfix(Tool __instance, int __state, GameLocation location, int x, int y, int power, Farmer who)
        {
            if (!IsAllowedTool(__instance))
            {
                return;
            }
            __instance.UpgradeLevel = __state;
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
                duration = 1000;
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

        private static bool IsAllowedTool(Item tool)
        {
            // TODO Config
            return tool is WateringCan or Hoe or Pickaxe or Axe or MeleeWeapon;
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
            tool.AttachmentSlotsCount = tool.UpgradeLevel;

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
            
            if (__instance is WateringCan wateringCan)
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
                    if (slots % 2 == 0)
                    {
                        return 68 * slots / 2 + enchantInc;
                    }
                    return 68 * (slots + 1) / 2 + enchantInc;
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
            );

            return matcher.InstructionEnumeration();
        }
        
        public static void HoeWatering(Farmer who, Vector2 tileLocation)
        {
            if (!IsAllowedTool(who.CurrentTool))
            {
                return;
            }

            if (who.currentLocation.terrainFeatures.TryGetValue(tileLocation, out TerrainFeature terrainFeature) && terrainFeature is HoeDirt dirt)
            {
                if (GetHasAttachmentQualifiedItemID(who.CurrentTool, MUQIds.Water))
                {
                    //dirt.modData[shouldWaterDirtKey] = "Water";
                    dirt.state.Value = 1;
                    if (!who.CurrentTool.isEfficient.Value)
                    {
                        who.Stamina -= 2 * who.toolPower.Value - who.FarmingLevel * 0.1f;
                    }
                }
            }
        }
        
        public static IEnumerable<CodeInstruction> Hoe_DoFunctionTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            CodeMatcher matcher = new(instructions, generator);
            MethodInfo tilePassable = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isTilePassable),  new[] { typeof(xTile.Dimensions.Location), typeof(xTile.Dimensions.Rectangle) });
            MethodInfo makeHoeDirt = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.makeHoeDirt));
            MethodInfo hoeWatering = AccessTools.Method(typeof(ModEntry), nameof(HoeWatering));

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
        
        internal static bool DrawAttachmentSlot_prefix(Tool __instance, int slot, SpriteBatch b, int x, int y)
        {
            if (!IsAllowedTool(__instance))
            {
                return true;
            }
            
            y -= slot * 68;
            
            x += slot % 2 * 68;
            y += slot / 2 * 68;
            
            Vector2 pixel = new Vector2(x, y);
            Texture2D texture = Game1.menuTexture;
            Rectangle sourceRect = Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 10);
            b.Draw(texture, pixel, sourceRect, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.86f);
            __instance.attachments[slot]?.drawInMenu(b, pixel, 1f);
            return false;
        }
    }
}
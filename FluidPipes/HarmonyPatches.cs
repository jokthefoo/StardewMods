using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Machines;
using Object = StardewValley.Object;

namespace FluidPipes;

internal static class HarmonyPatches
{
    internal static string MachineFluidInputPrefix = $"Jok.FluidPipes.FluidType.Input.";
    internal static string MachineFluidOutputPrefix = $"Jok.FluidPipes.FluidType.Output.";
    public static void Patch(string modId)
    {
        var harmony = new Harmony(modId);
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), "CheckForActionOnMachine"),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_CheckForActionOnMachine_prefix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.performToolAction)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_performToolAction_prefix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.maximumStackSize)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_maximumStackSize_postfix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.canGrabSomethingFromHere)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Utility_canGrabSomethingFromHere_postfix)));
        
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Object), nameof(Object.minutesElapsed)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_minutesElapsed_postfix)));
        
        /*
        harmony.Patch(
            original: AccessTools.Method(
                typeof(MachineDataUtility),
                nameof(MachineDataUtility.GetOutputData),
                new Type[] { typeof(Object), typeof(MachineData), typeof(MachineOutputRule),
                    typeof(Item), typeof(Farmer), typeof(GameLocation) }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(MachineDataUtility_GetOutputData_prefix)));
        
        harmony.Patch(
            original: AccessTools.Method(typeof(MachineDataUtility),
                nameof(MachineDataUtility.GetOutputItem)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(MachineDataUtility_GetOutputItem_postfix)));
        
        harmony.Patch(
            original: AccessTools.Method(typeof(Object),
                nameof(Object.OutputMachine)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_OutputMachine_postfix)));
        
        // Drawing
        harmony.Patch(AccessTools.Method(typeof(Object), nameof(Object.draw), new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Object_draw_postfix)));*/
    }

    public static void Object_minutesElapsed_postfix(Object __instance, int minutes)
    {
        /*
        var machineData = __instance.GetMachineData();
        if (machineData == null || __instance.heldObject.Value == null || !__instance.readyForHarvest.Value)
        {
            return;
        }

        foreach (var rule in machineData.OutputRules)
        {
            bool hasOutputCollected = false;
            foreach (var trigger in rule.Triggers)
            {
                if (trigger.Trigger.HasFlag(MachineOutputTrigger.OutputCollected))
                {
                    hasOutputCollected = true;
                }
            }
            
            MachineItemOutput? fluidItem = null;
            foreach (var item in rule.OutputItem)
            {
                if (item.OutputMethod == "FluidPipes.FluidData, FluidPipes: OutputFluid")
                {
                    fluidItem = item;
                }
            }

            if (hasOutputCollected && fluidItem != null)
            {
                var fluidOutputs = FluidData.GetFluidInfoFromDict(fluidItem.CustomData, FluidData.OutputTypeKeyRegex, FluidData.OutputVolumeKeyPrefix);
                foreach (var fluidData in fluidOutputs)
                {
                    if (__instance.heldObject.Value.ItemId == fluidData.fluidType)
                    {
                        
                    }
                }
            }
        }*/
    }

    public static void Utility_canGrabSomethingFromHere_postfix(ref bool __result, int x, int y, Farmer who)
    {
        Vector2 tileLocation = new Vector2(x / 64, y / 64);
        if (Game1.currentLocation.Objects.TryGetValue(tileLocation, out var obj))
        {
            if (obj.readyForHarvest.Value && obj.heldObject.Value != null && obj.heldObject.Value.modData.TryGetValue(FluidData.LiquidKey, out var liquid))
            {
                Game1.mouseCursor = Game1.cursor_default;
                Game1.mouseCursorTransparency = 1f;
                __result = false;
            }
        }
    }

    public static void Object_maximumStackSize_postfix(Object __instance, ref int __result)
    {
        if (__instance.modData.TryGetValue(FluidData.LiquidKey, out var liquid))
        {
            __result = int.MaxValue;
        }
    }

    public static bool Object_CheckForActionOnMachine_prefix(Object __instance, Farmer who, bool justCheckingForActivity = false)
    {
        if (!__instance.readyForHarvest.Value)
        {
            return true;
        }

        if (__instance.heldObject.Value.modData.TryGetValue(FluidData.LiquidKey, out var liquid))
        {
            return false;
        }
        return true;
    }
    
    public static bool Object_performToolAction_prefix(Object __instance, ref bool __result, Tool t)
    {
        if (__instance != null && __instance.heldObject.Value != null && __instance.heldObject.Value.modData.TryGetValue(FluidData.LiquidKey, out var liquid))
        {
            __result = true;
            return false;
        }
        return true;
    }

    private static void Object_OutputMachine_postfix(Object __instance, ref bool __result, MachineData machine, MachineOutputRule outputRule, Item inputItem, Farmer who, GameLocation location, bool probe, bool heldObjectOnly = false)
    {
        if (!__result)
        {
            return;
        }

        if (__instance.heldObject.Value.QualifiedItemId == "(O)0" && __instance.heldObject.Value.modData.ContainsKey(ModEntry.WeedsKey))
        {
            __instance.heldObject.Value = null;
        }
    }

    private static void Object_draw_postfix(Object __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
    {
        if (__instance.modData == null || __instance.modData.Length == 0)
        {
            return;
        }

        int waterVolume = 0;
        int oilVolume = 0;
        foreach (var modDataKey in __instance.modData.Keys)
        {
            var match = FluidData.MachineFluidOutputRegex.Match(modDataKey);
            if (!match.Success)
            {
                continue;
            }

            if (match.Groups[1].Value == "water" && Int32.TryParse(__instance.modData[modDataKey], out waterVolume))
            {

            }
            if (match.Groups[1].Value == "oil" && Int32.TryParse(__instance.modData[modDataKey], out oilVolume))
            {

            }
        }

        if (waterVolume == 0 && oilVolume == 0)
        {
            return;
        }

        if (waterVolume > 0)
        {
            int width = 2;
            int height = 16 * waterVolume / 100;
            
            var position = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 + 64 - 4*height));
            var draw_layer = Math.Max(0f, ((y + 1) * 64 - 24) / 10000f) + x * 1E-05f;

            var rect = new Rectangle(0, 2000, width, height);
            var bgcolor = Color.White;
            bgcolor.A = 255;
            spriteBatch.Draw(Game1.mouseCursors, position, rect, bgcolor, 0f, Vector2.Zero, 4f, SpriteEffects.None, draw_layer);
        
            draw_layer += 1E-05f;
            var color = __instance.Location.waterColor.Value;
            color.A = 255;
            spriteBatch.Draw(Game1.mouseCursors, position, new Rectangle(__instance.Location.waterAnimationIndex * 64, 2064 + ((x + y) % 2 != 0 ? 128 : 0), width * 4, height * 4), color,
                0f, Vector2.Zero, 1f, SpriteEffects.None, draw_layer);
        }
        
        if (oilVolume > 0)
        {
            int width = 2;
            int height = 16 * oilVolume / 100;
            
            var position = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 64, y * 64 + 64 - 4*height));
            var draw_layer = Math.Max(0f, ((y + 1) * 64 - 24) / 10000f) + x * 1E-05f;

            var rect = new Rectangle(0, 2000, width, height);
            var bgcolor = Color.Black;
            bgcolor.A = 255;
            spriteBatch.Draw(Game1.mouseCursors, position, rect, bgcolor, 0f, Vector2.Zero, 4f, SpriteEffects.None, draw_layer);
        
            draw_layer += 1E-05f;
            var color = __instance.Location.waterColor.Value;
            color.A = 255;
            spriteBatch.Draw(Game1.mouseCursors, position, new Rectangle(__instance.Location.waterAnimationIndex * 64, 2064 + ((x + y) % 2 != 0 ? 128 : 0), width * 4, height * 4), color,
                0f, Vector2.Zero, 1f, SpriteEffects.None, draw_layer);
        }

        //SpriteText.drawSmallTextBubble(spriteBatch, text, position, 256, 0.98f + __instance.TileLocation.X * 0.0001f + __instance.TileLocation.Y * 1E-06f);
    }

    private static bool MachineDataUtility_GetOutputData_prefix(ref MachineItemOutput __result, Object machine, MachineData machineData, MachineOutputRule outputRule, Item inputItem, Farmer who, GameLocation location)
    {
        if (outputRule == null && !MachineDataUtility.TryGetMachineOutputRule(machine, machineData, MachineOutputTrigger.ItemPlacedInMachine, inputItem, who, location, out outputRule, out var _, out var _, out var _))
        {
            return false;
        }
        
        List<MachineItemOutput> validOutputs = new List<MachineItemOutput>();
        foreach (MachineItemOutput possibleOutput in outputRule.OutputItem)
        {
            if (possibleOutput.CustomData == null) {
                validOutputs.Add(possibleOutput);
                continue;
            }
            if (FluidData.CheckFluidRequirements(possibleOutput, machine)) {
                validOutputs.Add(possibleOutput);
            }
        }
        
        __result = MachineDataUtility.GetOutputData(validOutputs, outputRule.UseFirstValidOutput, inputItem, who, location);
        return false;
    }


    private static void MachineDataUtility_GetOutputItem_postfix(ref Item __result, Object machine, MachineItemOutput outputData, Item inputItem, Farmer who, bool probe,
        ref int? overrideMinutesUntilReady)
    {
        if (__result == null || outputData == null || probe)
        {
            return;
        }

        // Take the input fluids
        var consumedFluids = FluidData.GetFluidInfoFromDict(outputData.CustomData, FluidData.RequirementTypeKeyRegex, FluidData.RequirementVolumeKeyPrefix);
        foreach (var fluid in consumedFluids)
        {
            int newVolume = 0;
            string fluidKey = MachineFluidInputPrefix + fluid.fluidType;
            if (machine.modData.TryGetValue(fluidKey, out var countString) && Int32.TryParse(countString, out int parsedVolume))
            {
                newVolume = parsedVolume - fluid.volume;
            }
            machine.modData[fluidKey] = newVolume.ToString();
        }

        // Generate the output fluids
        var outputFluids = FluidData.GetOutputFluids(outputData, machine.GetMachineData());
        foreach (var fluid in outputFluids)
        {
            int newVolume = fluid.volume;
            string fluidKey = MachineFluidOutputPrefix + fluid.fluidType;
            if (machine.modData.TryGetValue(fluidKey, out var countString) && Int32.TryParse(countString, out int parsedVolume))
            {
                newVolume += parsedVolume;
            }
            machine.modData[fluidKey] = newVolume.ToString();
        }
    }
}
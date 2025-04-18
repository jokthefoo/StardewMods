using Microsoft.Xna.Framework;
using StardewValley;
using Object = System.Object;

namespace FishMod;

public class AnimalFishing
{
    public static bool Post_CheckForAction(Object __instance, Farmer who, bool justCheckingForActivity)
    {
        if (((Item)__instance).QualifiedItemId == "(BC){{ModId}}.AnimalMachine")
        {
            if (!justCheckingForActivity)
            {
                Game1.activeClickableMenu = new TreeBobberBar(Game1.currentLocation, false, 1, Game1.player.CurrentTool, Vector2.One);
            }
        }
        return true;
    }
}
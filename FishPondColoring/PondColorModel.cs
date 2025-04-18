using System.ComponentModel;
using HarmonyLib;
using Microsoft.Xna.Framework;
using PropertyChanged.SourceGenerator;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace FishPondColoring
{
    internal partial class PondColorModel : INotifyPropertyChanged
    {
        private static Color defaultPondColor = new Color(12,126,150);
        [Notify] private Color pondColor;
        private FishPond fishPond;
        public IReadOnlyList<WaterBoxModel> AllWater { get; }
        
        public PondColorModel(FishPond pond)
        {
            fishPond = pond;
            pondColor = fishPond.overrideWaterColor.Value == Color.White ? defaultPondColor : fishPond.overrideWaterColor.Value;
            List<WaterBoxModel> waterBoxes = new List<WaterBoxModel>();
            for (int x = 0; x < 20; x++)
            {
                waterBoxes.Add(new WaterBoxModel());
            }
            AllWater = waterBoxes;
        }
        
        private int timerMills = 200;
        public void Update(TimeSpan elapsed)
        {
            timerMills -= elapsed.Milliseconds;
            if (timerMills <= 0)
            {
                foreach (WaterBoxModel waterBox in AllWater)
                {
                    waterBox.UpdateSprite();
                }
                timerMills = 200;
            }
        }
        
        public void Close(bool save)
        {
            var menu = Game1.activeClickableMenu;
            while (menu.GetChildMenu() is IClickableMenu childMenu)
            {
                menu = childMenu;
            }
            if (!menu.readyToClose())
            {
                return;
            }
            Game1.playSound("bigDeSelect");
            if (menu.GetParentMenu() is IClickableMenu parentMenu)
            {
                parentMenu.SetChildMenu(null);
            }
            else
            {
                if (save)
                {
                    if (pondColor != defaultPondColor)
                    {
                        fishPond.modData["Jok.FishPondColor"] = pondColor.PackedValue.ToString();
                        ModEntry.Pond_doFishSpecificWaterColoring_postfix(fishPond);
                    }
                }
                Game1.exitActiveMenu();
            }
        }
        
        public void ResetPondColor()
        {
            fishPond.modData.Remove("Jok.FishPondColor");
            ModEntry.Helper.Reflection.GetMethod(fishPond, "doFishSpecificWaterColoring").Invoke();
            PondColor = fishPond.overrideWaterColor.Value;
            pondColor = pondColor == Color.White ? defaultPondColor : pondColor;
        }
    }
}
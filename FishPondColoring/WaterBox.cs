using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PropertyChanged.SourceGenerator;
using StardewValley;
namespace FishPondColoring;

internal partial record WaterBoxModel
    : INotifyPropertyChanged
{
    [Notify]
    private Tuple<Texture2D, Rectangle> waterTile = null!;

    public WaterBoxModel()
    {
        WaterTile = new Tuple<Texture2D, Rectangle>( Game1.mouseCursors,
        new Rectangle(Game1.currentLocation.waterAnimationIndex * 64, 2064, 64, 64));
    }

    public void UpdateSprite()
    {
        WaterTile = new Tuple<Texture2D, Rectangle>( Game1.mouseCursors,
            new Rectangle(Game1.currentLocation.waterAnimationIndex * 64, 2064, 64, 64));
    }
}
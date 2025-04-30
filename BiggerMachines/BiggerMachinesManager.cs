using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace BiggerMachines;

internal class BiggerMachinesList
{
    public List<BiggerMachineData> BiggerMachines { get; set; } = new();
}

internal class BiggerMachineData
{
    public BiggerMachineData(int w, int h, bool fade = false, bool drawShadow = false)
    {
        Width = w;
        Height = h;
        Fade = fade;
        DrawShadow = drawShadow;
    }

    public int Width { get; set; }
    public int Height { get; set; }
    public bool Fade { get; set; }    
    public bool DrawShadow { get; set; }
}

internal class BiggerMachine
{
    public BiggerMachine(StardewValley.Object obj, BiggerMachineData data)
    {
        Object = obj;
        BMData = data;
    }
    public BiggerMachineData BMData { get;}
    public StardewValley.Object Object { get; }
    
    public bool 
        IntersectsForCollision(Rectangle rect)
    {
        return GetBoundingBox().Intersects(rect);
    }
    
    public Rectangle GetBoundingBox()
    {
        Rectangle bounds = Object.boundingBox.Value;
        bounds.X = (int)Object.TileLocation.X * 64;
        bounds.Y = (int)Object.TileLocation.Y * 64;
        bounds.Height = 64 * BMData.Height;
        bounds.Width = 64 * BMData.Width;
        if (Object.boundingBox.Value != bounds)
        {
            Object.boundingBox.Value = bounds;
        }
        return bounds;
    }
}
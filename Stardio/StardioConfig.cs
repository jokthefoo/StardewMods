using StardewModdingAPI;

namespace Jok.Stardio;

public sealed class StardioConfig
{
    public bool ShowQualityOnBelts { get; set; } = true;
    public int BeltUpdateMS { get; set; } = 100;
    public float BeltPushPlayerSpeed { get; set; } = 1.5f;
    public float BeltPlayerSpeedBoost { get; set; } = 2f;
    public SButton RotateKeybind { get; set; } = SButton.R;
}
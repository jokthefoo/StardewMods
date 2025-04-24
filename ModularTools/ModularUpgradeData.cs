namespace ModularTools;

public class ModularUpgradeData
{
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Texture { get; set; } = "Jok.ModularTools/modularupgrades.png";
    public int TextureIndex { get; set; } = 0;
    public int Price { get; set; } = 0;
    public List<string> AllowedTools { get; set; }
}
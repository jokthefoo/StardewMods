using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Tools;

namespace ModularTools;

public class ModularToolsGalaxyEnchantment : BaseEnchantment
{
    public override string GetName()
    {
        return "Galaxy Upgrade";
    }
    
    public override bool IsSecondaryEnchantment()
    {
        return true;
    }

    public override bool ShouldBeDisplayed()
    {
        return true;
    }
    
    public override bool CanApplyTo(Item item)
    {
        return false;
    }
    
    protected override void _ApplyTo(Item item)
    {
        base._ApplyTo(item);
        if (item is Tool tool && ModEntry.IsAllowedTool(tool))
        {
            tool.AttachmentSlotsCount++;
        }
    }

    protected override void _UnapplyTo(Item item)
    {
        base._UnapplyTo(item);
        if (item is Tool tool && ModEntry.IsAllowedTool(tool))
        {
            tool.AttachmentSlotsCount--;
        }
    }
}
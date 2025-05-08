using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Inventories;

namespace Jok.Stardio;

public interface IExtraMachineConfigApi
{  
    IList<(string, int)> GetExtraRequirements(MachineItemOutput outputData);
    IList<(string, int)> GetExtraTagsRequirements(MachineItemOutput outputData);
}
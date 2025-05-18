using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Inventories;

namespace Jok.StardewInc;

public interface IExtraMachineConfigApi
{  
    IList<(string, int)> GetExtraRequirements(MachineItemOutput outputData);
    IList<(string, int)> GetExtraTagsRequirements(MachineItemOutput outputData);
}
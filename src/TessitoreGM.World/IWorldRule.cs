using TessitoreGM.Events;

namespace TessitoreGM.World;

public interface IWorldRule
{
    IWorldEvent? ProposeNext(WorldSnapshot world, DateTimeOffset until);
}

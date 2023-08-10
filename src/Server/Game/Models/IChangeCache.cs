using System.Threading.Tasks;

namespace Imlight.Server.Game.Models;

public interface IChangeCache
{
    void EnqueueChange(object change);
    Task FlushChangesAsync();
}
using System.Threading.Tasks;

namespace TimeToFish;

public interface ITimerJob
{
    Task<HandlerResult> Handler(TimeEvent message);
}
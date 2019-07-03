using System.Threading.Tasks;

namespace TimeToFish
{
    public abstract class TimerJob
    {
        public JobTypes JobType { get; }

        public enum JobTypes { Minute, Hour, Day, Month, Year }
        public abstract Task<HandlerResult> Handler(TimeEvent message);

        public TimerJob(JobTypes jobType)
        {
            JobType = jobType;
        }
    }
}
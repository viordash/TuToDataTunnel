namespace TuToProxy.Core.Services {
    public interface IDateTimeService {
        public DateTime Now { get; }
        public TimeSpan RequestTimeout { get; }
    }

    public class DateTimeService : IDateTimeService {
        public DateTime Now {
            get => DateTime.Now;

        }

        public TimeSpan RequestTimeout {
            get => TimeSpan.FromSeconds(10);
        }
    }
}

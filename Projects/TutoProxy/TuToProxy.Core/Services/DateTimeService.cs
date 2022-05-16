namespace TuToProxy.Core.Services {
    public interface IDateTimeService {
        public DateTime Now { get; }
    }

    public class DateTimeService : IDateTimeService {
        public DateTime Now {
            get {
                return DateTime.Now;
            }
        }
    }
}

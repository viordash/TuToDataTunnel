namespace TuToProxy.Core {
    public class UdpSocketParams {
        public static TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(30_000);
        public static int LogUpdatePeriod = 1;
    }
}

namespace TuToProxy.Core {
    public class TcpSocketParams {
        public static int ReceiveBufferSize = 8192;
        public static TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(10_000);
        public static int LogUpdatePeriod = 1;
        public static int QueueMaxSize = 1000;
    }
}

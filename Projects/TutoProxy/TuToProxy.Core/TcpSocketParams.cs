namespace TuToProxy.Core {
    public class TcpSocketParams {
        public static int ReceiveBufferSize = 8192;
        public static TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(120_000);
        public static int LogUpdatePeriod = 1;
    }
}

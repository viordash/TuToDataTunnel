namespace TuToProxy.Core {
    public class TcpSocketParams {
        public static int ReceiveBufferSize = 65536 * 4;
        public static TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(10_000);
        public static int LogUpdatePeriod = 1;
        public static bool TrafficMonitoring = true;

        // Pipeline parameters
        public static int MaxInflightRequests = 16;
        public static int ChannelCapacity = 32;
        public static int PipePauseThreshold = 131072;   // 128KB
        public static int PipeResumeThreshold = 65536;   // 64KB
    }
}

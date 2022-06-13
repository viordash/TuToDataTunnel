﻿namespace TuToProxy.Core {
    public class TcpSocketParams {
        public static int ReceiveBufferSize = 16384;
        public static TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(30_000);
        public static int LogUpdatePeriod = 1;
    }
}

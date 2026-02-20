using TuToProxy.Core.Models;

namespace TuToProxy.Core {
    /// <summary>
    /// Typed hub interface for server-to-client method calls.
    /// Note: Methods returning values from client (InvokeAsync pattern)
    /// are not supported by typed hubs and use ClientMethods constants instead.
    /// </summary>
    public interface IClientHub {
        Task Errors(string message);
        Task UdpRequest(UdpDataRequestModel request);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered);
    }

    /// <summary>
    /// Method name constants for InvokeAsync calls (client results pattern).
    /// Typed hubs don't support methods with return values from client.
    /// </summary>
    public static class ClientMethods {
        public const string ConnectTcp = "ConnectTcp";
        public const string TcpRequest = "TcpRequest";
        public const string DisconnectTcp = "DisconnectTcp";
    }
}

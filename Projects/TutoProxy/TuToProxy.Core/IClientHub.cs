using System.Net.Sockets;
using TuToProxy.Core.Models;

namespace TuToProxy.Core {
    /// <summary>
    /// Typed hub interface for server-to-client method calls (fire-and-forget).
    /// Used by server's Hub&lt;IClientHub&gt;.
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

    /// <summary>
    /// Interface for client-to-server hub method calls.
    /// Used by TypedSignalR.Client source generator on client side.
    /// </summary>
    public interface IServerHub {
        Task UdpResponse(UdpDataResponseModel response);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered);
        Task<int> TcpResponse(TcpDataResponseModel response);
        Task<bool> DisconnectTcp(SocketAddressModel socketAddress);
    }

    /// <summary>
    /// Interface for server-to-client method calls (receiver).
    /// Used by TypedSignalR.Client source generator on client side.
    /// Includes all methods including those with return values (client results).
    /// </summary>
    public interface IClientReceiver {
        Task Errors(string message);
        Task UdpRequest(UdpDataRequestModel request);
        Task DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered);
        Task<SocketError> ConnectTcp(SocketAddressModel socketAddress);
        Task<int> TcpRequest(TcpDataRequestModel request);
        Task<bool> DisconnectTcp(SocketAddressModel socketAddress);
    }
}

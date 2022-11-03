namespace TuToProxy.Core.Exceptions {
    public class HubConnectionException : TuToException {
        public HubConnectionException(string? connectionId) : base($"hub-client ({connectionId}) disconnected") {
        }
    }
}

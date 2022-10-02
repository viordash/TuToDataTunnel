namespace TuToProxy.Core.Exceptions {
    public class ClientConnectionException : TuToException {
        public ClientConnectionException(string connectionId, string message) : base($"Client :{connectionId}, '{message}'") {
        }
        public ClientConnectionException(IEnumerable<string> clientIds, string connectionId, string message) : base($"Client [{clientIds.FirstOrDefault()}]:{connectionId}, '{message}'") {
        }
    }
}

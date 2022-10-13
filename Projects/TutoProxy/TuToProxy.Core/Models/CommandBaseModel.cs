namespace TuToProxy.Core.Models {
    public enum SocketCommand {
        Empty,
        Disconnect
    }

    public abstract class CommandBaseModel {
        public int Port { get; set; }
        public int OriginPort { get; set; }
        public SocketCommand Command { get; set; }

        public CommandBaseModel(int port, int originPort, SocketCommand command) {
            Port = port;
            OriginPort = originPort;
            Command = command;
        }

        public override string ToString() {
            return $"port:{Port}, o-port:{OriginPort}, cmd:{Command}";
        }
    }
}

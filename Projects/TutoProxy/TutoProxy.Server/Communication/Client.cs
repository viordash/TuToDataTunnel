using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace TutoProxy.Server.Communication {
    public class Client {
        public IClientProxy ClientProxy { get; private set; }
        public List<int>? TcpPorts { get; private set; }
        public List<int>? UdpPorts { get; private set; }
        public Client(IClientProxy clientProxy, List<int>? tcpPorts, List<int>? udpPorts) {
            ClientProxy = clientProxy;
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;
        }
    }
}

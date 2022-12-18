﻿using Terminal.Gui;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Windows;

namespace TutoProxy.Server.Services {
    public interface IProcessMonitor {
        void ConnectHubClient(string connectionId, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts);
        void DisconnectHubClient(string connectionId, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts);

        void ConnectTcpClient(BaseClient client);
        void DisconnectTcpClient(BaseClient client);
        void TcpClientData(BaseClient client, Int64 transmitted, Int64 received);
    }


    public class ProcessMonitor : IProcessMonitor {
        public void ConnectHubClient(string connectionId, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts) {
            var mainWindow = Application.Top.Focused as MainWindow;
            mainWindow?.HubClientConnected(connectionId, tcpPorts, udpPorts);
        }
        public void DisconnectHubClient(string connectionId, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts) {
            var mainWindow = Application.Top.Focused as MainWindow;
            mainWindow?.HubClientDisconnected(connectionId, tcpPorts, udpPorts);
        }

        public void ConnectTcpClient(BaseClient client) {
            var mainWindow = Application.Top.Focused as MainWindow;
            mainWindow?.AddTcpClient(client);
        }

        public void DisconnectTcpClient(BaseClient client) {
            var mainWindow = Application.Top.Focused as MainWindow;
            mainWindow?.RemoveTcpClient(client);
        }

        public void TcpClientData(BaseClient client, Int64 transmitted, Int64 received) {
            var mainWindow = Application.Top.Focused as MainWindow;
            mainWindow?.TcpClientData(client, transmitted, received);
        }
    }
}

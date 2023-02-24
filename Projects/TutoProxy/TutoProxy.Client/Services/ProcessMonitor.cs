using Terminal.Gui;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Windows;

namespace TutoProxy.Client.Services {
    public interface IProcessMonitor {
        void ConnectTcpClient(BaseClient client);
        void DisconnectTcpClient(BaseClient client);
        void TcpClientData(BaseClient client, Int64 transmitted, Int64 received);

        void ConnectUdpClient(BaseClient client);
        void DisconnectUdpClient(BaseClient client);
        void UdpClientData(BaseClient client, Int64 transmitted, Int64 received);
    }


    public class ProcessMonitor : IProcessMonitor {
        public void ConnectTcpClient(BaseClient client) {
            var mainWindow = Application.Top?.Focused as MainWindow;
            mainWindow?.AddTcpClient(client);
        }

        public void DisconnectTcpClient(BaseClient client) {
            var mainWindow = Application.Top?.Focused as MainWindow;
            mainWindow?.RemoveTcpClient(client);
        }

        public void TcpClientData(BaseClient client, Int64 transmitted, Int64 received) {
            var mainWindow = Application.Top?.Focused as MainWindow;
            mainWindow?.TcpClientData(client, transmitted, received);
        }

        public void ConnectUdpClient(BaseClient client) {
            var mainWindow = Application.Top?.Focused as MainWindow;
            mainWindow?.AddUdpClient(client);
        }

        public void DisconnectUdpClient(BaseClient client) {
            var mainWindow = Application.Top?.Focused as MainWindow;
            mainWindow?.RemoveUdpClient(client);
        }
        public void UdpClientData(BaseClient client, long transmitted, long received) {
            var mainWindow = Application.Top?.Focused as MainWindow;
            mainWindow?.UdpClientData(client, transmitted, received);
        }
    }
}

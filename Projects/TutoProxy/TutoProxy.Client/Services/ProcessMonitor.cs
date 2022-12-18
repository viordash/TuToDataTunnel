using Terminal.Gui;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Windows;

namespace TutoProxy.Client.Services {
    public interface IProcessMonitor {
        void ConnectTcpClient(BaseClient client);
        void DisconnectTcpClient(BaseClient client);
        void TcpClientData(BaseClient client, Int64 transmitted, Int64 received);
    }


    public class ProcessMonitor : IProcessMonitor {
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

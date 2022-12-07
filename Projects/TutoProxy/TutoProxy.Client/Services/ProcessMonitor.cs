using Terminal.Gui;
using TutoProxy.Client.Windows;

namespace TutoProxy.Client.Services {
    public interface IProcessMonitor {
        void ConnectTcpClient(int port, int originPort);
        void DisconnectTcpClient(int port, int originPort);
        void RequestToTcpClient(int port, int originPort, int bytes);
        void ResponseFromTcpClient(int port, int originPort, int bytes);
    }


    public class ProcessMonitor : IProcessMonitor {
        public void ConnectTcpClient(int port, int originPort) {
            var mainWindow = Application.Top.Focused as MainWindow;
            throw new NotImplementedException();
        }

        public void DisconnectTcpClient(int port, int originPort) {
            throw new NotImplementedException();
        }

        public void RequestToTcpClient(int port, int originPort, int bytes) {
            throw new NotImplementedException();
        }

        public void ResponseFromTcpClient(int port, int originPort, int bytes) {
            throw new NotImplementedException();
        }
    }
}

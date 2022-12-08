using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.Trees;
using TutoProxy.Client.Communication;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Windows {
    internal class MainWindow : Window {
        TreeView treeViewClients;
        TreeNode tcpClients;
        TreeNode udpClients;

        public MainWindow(string title) : base(title) {
            X = 0;
            Y = 0;
            Width = Dim.Fill();
            Height = Dim.Fill();

            treeViewClients = new TreeView() {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            Add(treeViewClients);

            tcpClients = new TreeNode();
            RefreshTcpClientsTitle();

            udpClients = new TreeNode();
            RefreshUdpClientsTitle();

            treeViewClients.AddObject(tcpClients);
            treeViewClients.AddObject(udpClients);
            treeViewClients.ExpandAll();
            SetupScrollBar();
        }

        void RefreshTcpClientsTitle() {
            tcpClients.Text = $"TCP clients ({tcpClients.Children.Count})";
        }

        void RefreshUdpClientsTitle() {
            udpClients.Text = $"UDP clients ({udpClients.Children.Count})";
        }

        public void AddTcpClient(BaseClient client) {
            Application.MainLoop.Invoke(() => {
                if(tcpClients.Children.Any(x => x.Tag == client)) {
                    throw new TuToException($"{client}, already connected");
                }
                tcpClients.Children.Add(new TreeNode(client.ToString()) { Tag = client });
                RefreshTcpClientsTitle();
                treeViewClients.RefreshObject(tcpClients);
            });
        }

        public void RemoveTcpClient(BaseClient client) {
            Application.MainLoop.Invoke(() => {
                var node = tcpClients.Children.FirstOrDefault(x => x.Tag == client);
                tcpClients.Children.Remove(node);
                RefreshTcpClientsTitle();
                treeViewClients.RefreshObject(tcpClients);
            });
        }

        public void TcpClientData(BaseClient client, Int64 transmitted, Int64 received) {
            Application.MainLoop.Invoke(() => {
                var node = tcpClients.Children.FirstOrDefault(x => x.Tag == client);
                if(node != null) {
                    node.Text = $"{client}, tx:{transmitted,10}, rx:{received,10}";
                    treeViewClients.RefreshObject(node);
                }
            });
        }

        void SetupScrollBar() {
            treeViewClients.Style.LeaveLastRow = true;

            var scrollBar = new ScrollBarView(treeViewClients, true);

            scrollBar.ChangedPosition += () => {
                treeViewClients.ScrollOffsetVertical = scrollBar.Position;
                if(treeViewClients.ScrollOffsetVertical != scrollBar.Position) {
                    scrollBar.Position = treeViewClients.ScrollOffsetVertical;
                }
                treeViewClients.SetNeedsDisplay();
            };

            scrollBar.OtherScrollBarView.ChangedPosition += () => {
                treeViewClients.ScrollOffsetHorizontal = scrollBar.OtherScrollBarView.Position;
                if(treeViewClients.ScrollOffsetHorizontal != scrollBar.OtherScrollBarView.Position) {
                    scrollBar.OtherScrollBarView.Position = treeViewClients.ScrollOffsetHorizontal;
                }
                treeViewClients.SetNeedsDisplay();
            };

            treeViewClients.DrawContent += (e) => {
                scrollBar.Size = treeViewClients.ContentHeight;
                scrollBar.Position = treeViewClients.ScrollOffsetVertical;
                scrollBar.OtherScrollBarView.Size = treeViewClients.GetContentWidth(true);
                scrollBar.OtherScrollBarView.Position = treeViewClients.ScrollOffsetHorizontal;
                scrollBar.Refresh();
            };
        }
    }
}

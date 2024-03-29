﻿using Terminal.Gui;
using Terminal.Gui.Trees;
using TutoProxy.Client.Communication;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Windows {
    internal class MainWindow : Window {
        TreeView treeViewClients;
        TreeNode tcpClients;
        TreeNode udpClients;

        Dictionary<int, TreeNode> tcpPortsNodes = new();
        Dictionary<int, TreeNode> udpPortsNodes = new();


        public MainWindow(string title, List<int>? tcpPorts, List<int>? udpPorts) : base(title) {
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

            udpClients = new TreeNode();

            treeViewClients.AddObject(tcpClients);
            treeViewClients.AddObject(udpClients);

            if(tcpPorts != null) {
                foreach(var port in tcpPorts) {
                    var node = new TreeNode() { Tag = port };
                    tcpPortsNodes[port] = node;
                    tcpClients.Children.Add(node);
                }
            }

            if(udpPorts != null) {
                foreach(var port in udpPorts) {
                    var node = new TreeNode() { Tag = port };
                    udpPortsNodes[port] = node;
                    udpClients.Children.Add(node);
                }
            }

            treeViewClients.ExpandAll();
            RefreshTcpClientsTitle();
            RefreshUdpClientsTitle();
            SetupScrollBar();
        }

        void RefreshTcpClientsTitle() {
            var count = 0;
            foreach(var portNode in tcpClients.Children) {
                count += portNode.Children.Count;
                portNode.Text = $"port:{portNode.Tag,5} ({portNode.Children.Count})";
            }
            tcpClients.Text = $"TCP clients ({count})";
        }

        void RefreshUdpClientsTitle() {
            var count = 0;
            foreach(var portNode in udpClients.Children) {
                count += portNode.Children.Count;
                portNode.Text = $"port:{portNode.Tag,5} ({portNode.Children.Count})";
            }
            udpClients.Text = $"UDP clients ({count})";
        }

        public void AddTcpClient(BaseClient client) {
            Application.MainLoop.Invoke(() => {
                var selectedPortNode = tcpPortsNodes[client.Port];

                if(selectedPortNode.Children.Any(x => x.Tag == client)) {
                    throw new TuToException($"{client}, already connected");
                }

                selectedPortNode.Children.Add(new TreeNode(client.ToString()) { Tag = client });
                treeViewClients.Expand(selectedPortNode);
                RefreshTcpClientsTitle();
                treeViewClients.RefreshObject(selectedPortNode);
            });
        }

        public void RemoveTcpClient(BaseClient client) {
            Application.MainLoop.Invoke(() => {
                var selectedPortNode = tcpPortsNodes[client.Port];
                var node = selectedPortNode.Children.FirstOrDefault(x => x.Tag == client);
                selectedPortNode.Children.Remove(node);
                RefreshTcpClientsTitle();
                treeViewClients.RefreshObject(selectedPortNode);
            });
        }

        public void TcpClientData(BaseClient client, Int64 transmitted, Int64 received) {
            Application.MainLoop.Invoke(() => {
                var selectedPortNode = tcpPortsNodes[client.Port];
                var node = selectedPortNode.Children.FirstOrDefault(x => x.Tag == client);
                if(node != null) {
                    node.Text = $"{client}, tx:{transmitted,10}, rx:{received,10}";
                    treeViewClients.RefreshObject(node);
                }
            });
        }

        public void AddUdpClient(BaseClient client) {
            Application.MainLoop.Invoke(() => {
                var selectedPortNode = udpPortsNodes[client.Port];

                if(selectedPortNode.Children.Any(x => x.Tag == client)) {
                    throw new TuToException($"{client}, already connected");
                }

                selectedPortNode.Children.Add(new TreeNode(client.ToString()) { Tag = client });
                treeViewClients.Expand(selectedPortNode);
                RefreshUdpClientsTitle();
                treeViewClients.RefreshObject(selectedPortNode);
            });
        }

        public void RemoveUdpClient(BaseClient client) {
            Application.MainLoop.Invoke(() => {
                var selectedPortNode = udpPortsNodes[client.Port];
                var node = selectedPortNode.Children.FirstOrDefault(x => x.Tag == client);
                selectedPortNode.Children.Remove(node);
                RefreshUdpClientsTitle();
                treeViewClients.RefreshObject(selectedPortNode);
            });
        }

        public void UdpClientData(BaseClient client, Int64 transmitted, Int64 received) {
            Application.MainLoop.Invoke(() => {
                var selectedPortNode = udpPortsNodes[client.Port];
                var node = selectedPortNode.Children.FirstOrDefault(x => x.Tag == client);
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

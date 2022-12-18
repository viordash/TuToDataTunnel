using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Terminal.Gui;

namespace TutoProxy.Client.Windows {
    internal class MainMenu : MenuBar {

        public MainMenu(string version) : base() {
            Menus = new MenuBarItem[] {
                new MenuBarItem ("_File", new MenuItem [] {
                    new MenuItem ("_Quit", "", () => {
                        Application.RequestStop ();
                    })
                }),
                new MenuBarItem ("_About", "", () => {
                        var okButton = new Button ("Ok");
                        okButton.Clicked += () => {
                            Application.RequestStop ();
                        };
                        var dialog = new Dialog ("About", 60, 20, okButton);
                        var txtVersion = new Label () {
                            X = 1,
                            Y = 1,
                            Width = Dim.Fill(),
                            Height = 1,
                            Text = version
                        };

                        var mainWindow = Application.Top.Focused as MainWindow;
                        var txtTitle = new Label () {
                            X = 1,
                            Y = 3,
                            Width = Dim.Fill(),
                            Height = Dim.Fill() - 3,
                            Text = mainWindow?.Title,
                            AutoSize = false
                        };

                        dialog.Add (txtVersion, txtTitle);
                        Application.Run (dialog);
                    })
            };
        }
    }
}

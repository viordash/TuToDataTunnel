using Terminal.Gui;

namespace TutoProxy.Server.Windows {
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
                        var dialog = new Dialog ("About", 60, 10, okButton);
                        var txtVersion = new TextField () {
                            X = 1,
                            Y = 1,
                            Width = Dim.Fill (),
                            Height = 1,
                            Text = version
                        };
                        dialog.Add (txtVersion);
                        Application.Run (dialog);
                    })
            };
        }
    }
}

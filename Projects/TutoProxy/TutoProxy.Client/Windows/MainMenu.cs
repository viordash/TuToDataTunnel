using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace TutoProxy.Client.Windows {
    internal class MainMenu : MenuBar {

        public MainMenu() : base() {
            Menus = new MenuBarItem[] {
                new MenuBarItem ("_File", new MenuItem [] {
                    new MenuItem ("_Quit", "", () => {
                        Application.RequestStop ();
                    })
                }),
                new MenuBarItem ("_About", "", () => {
                        Application.RequestStop ();
                    })
            };
        }
    }
}

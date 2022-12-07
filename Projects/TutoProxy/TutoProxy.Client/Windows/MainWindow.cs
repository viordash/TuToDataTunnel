using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace TutoProxy.Client.Windows {
    internal class MainWindow : Window {


        public MainWindow(string title) : base(title) {
            X = 0;
            Y = 1;
            Width = Dim.Fill();
            Height = Dim.Fill() - 1;
        }


    }
}

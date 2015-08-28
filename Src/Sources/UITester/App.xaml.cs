using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace UITester {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        //Add this method override
        protected override void OnStartup(StartupEventArgs e) {
            ParameterController.Parse(e.Args);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Aderant.Build;
using Aderant.Build.Packaging;
using Aderant.PresentationFramework.Input;

namespace PaketArtifactTool.ViewModels {

    public class MainViewModel : ViewModel {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel() {
            RootFolder = @"C:\Source\ExpertSuite\Framework";
        }

        private string rootFolder;

        /// <summary>
        /// Gets or sets the solution folder.
        /// </summary>
        public string RootFolder {
            get { return rootFolder; }
            set {
                if (rootFolder != value) {
                    rootFolder = value;
                    OnPropertyChanged(nameof(RootFolder));
                }
            }
        }

        private string status = "";

        public string Status {
            get { return status; }
            set {
                if (status != value) {
                    status = value;
                    OnPropertyChanged("Status");
                }
            }
        }
        
        private ICommand inspectCommand;

        /// <summary>
        /// Command for opening a solution path.
        /// </summary>
        public ICommand InspectCommand => inspectCommand ?? (inspectCommand = new DelegateCommand(OnInspect));

        private ICommand closeCommand;

        public ICommand CloseCommand {
            get { return closeCommand ?? (closeCommand = new DelegateCommand(OnClose)); }
        }

        private void OnClose() {
            Application.Current.Shutdown();
        }

        private void OnInspect() {

            Status = "Processing...";

            Application.Current.Dispatcher.BeginInvoke(
                new Action(
                    () => {
                        Process();
                        Status = "Done!";
                    }),
                DispatcherPriority.Input);
            
        }

        private void Process() {
            var logger = new ConsoleLogger();
            var fs = new PhysicalFileSystem(RootFolder);
            var packager = new Packager(fs, logger);

            var files = packager.GetTemplateFiles();

            foreach (var file in files) {
                var templateFile = new TemplateFileUpgrader(fs, file, logger);
                templateFile.Write();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UITester.Annotations;

namespace UITester.Pages {
    public class DatabaseManagementViewModel : INotifyPropertyChanged, IDisposable {
        public DatabaseManagementViewModel(PageController pageController) {
            this.pageController = pageController;
            parameters = ParameterController.Singleton;
            backupFileName = @"C:\Temp\AutomationDbBackup.bak";
            DatabasePassword = "Ad3rant0";
            DatabaseUser = "SA";
            if (this.pageController != null) {
                this.pageController.Invoker.PropertyChanged += InvokerOnPropertyChanged;
            }
        }

        private void InvokerOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            if (propertyChangedEventArgs != null && propertyChangedEventArgs.PropertyName == "IsTaskRunning") {
                OnPropertyChanged("CanStart");
            }
        }

        [NotNull]
        private readonly ParameterController parameters;
        private readonly PageController pageController;

        private string databaseUser;

        public string DatabaseUser {
            get { return databaseUser; }
            set {
                databaseUser = value;
                OnPropertyChanged("DatabaseUser");
            }
        }
        private string databasePassword;
        public string DatabasePassword {
            get { return databasePassword; }
            set {
                databasePassword = value;
                OnPropertyChanged("DatabasePassword");
            }
        }
        
        private string backupFileName;
        public string BackupFileName {
            get { return backupFileName; }
            set {
                backupFileName = value;
                OnPropertyChanged("BackupFileName");
            }
        }

        public string EnvironmentManifestPath {
            get { return parameters.EnvironmentManifestPath; }
            set {
                parameters.EnvironmentManifestPath = value;
                OnPropertyChanged("EnvironmentManifestPath");
            }
        }

        public TabLogger DatabaseManagementLog { get { return pageController != null && pageController.Logger != null ? pageController.Logger.DatabaseManagementTabLogger : null; } }
        
        public bool CanStart {
            get { return pageController != null && !pageController.Invoker.IsTaskRunning; }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var handler = PropertyChanged;
            if (handler != null) {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion INotifyPropertyChanged

        #region Implementation of IDisposable
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            if (this.pageController != null) {
                this.pageController.Invoker.PropertyChanged -= InvokerOnPropertyChanged;
            }
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UITester.Annotations;

namespace UITester.Pages {
    public class TestSetupViewModel : INotifyPropertyChanged, IDisposable {
        public TestSetupViewModel(PageController pageController) {
            parameters = ParameterController.Singleton;
            this.pageController = pageController;
            if (this.pageController != null) {
                this.pageController.Invoker.PropertyChanged += InvokerOnPropertyChanged;
            }
            UpdateDllList();
        }

        private void InvokerOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            if (propertyChangedEventArgs != null && propertyChangedEventArgs.PropertyName == "IsTaskRunning") {
                OnPropertyChanged("CanStart");
            }
        }
        

        [NotNull]
        private readonly ParameterController parameters;
        private readonly PageController pageController;
        
        public string EnvironmentManifestPath {
            get { return parameters.EnvironmentManifestPath; }
            set {
                parameters.EnvironmentManifestPath = value;
                OnPropertyChanged("EnvironmentManifestPath");
            }
        }
        [NotNull]
        private readonly ObservableCollection<SelectableTestDll> testDllCollection = new ObservableCollection<SelectableTestDll>();
        [NotNull]
        public ObservableCollection<SelectableTestDll> TestDllCollection { get { return testDllCollection; } }

        private void UpdateDllList() {
            Task<string> getSourcePath = pageController.Invoker.WorkOutTestSourcePath(!useDropDirectory);
            getSourcePath.ContinueWith(
                (x) => {
                    string testSourcePath = x.Result;
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() => { TestDllCollection.Clear(); }));
                    if (!string.IsNullOrWhiteSpace(testSourcePath) && Directory.Exists(testSourcePath)) {
                        string[] allDlls = Directory.GetFiles(testSourcePath, "Tests*.dll");
                        foreach (string item in allDlls) {
                            Application.Current.Dispatcher.BeginInvoke(
                                new Action<string>(
                                    (actionItem) => {
                                        TestDllCollection.Add(new SelectableTestDll(actionItem));
                                    }),
                                DispatcherPriority.Normal,
                                item);
                        }
                    }
                });
        }

        public string RemoteMachineName {
            get { return parameters != null ? parameters.RemoteMachineName : null; }
            set {
                if (parameters == null) {
                    return;
                }
                parameters.RemoteMachineName = value;
                OnPropertyChanged("RemoteMachineName");
            }
        }

        public bool CanStart {
            get { return pageController != null && !pageController.Invoker.IsTaskRunning; }
        }

        public bool IsRemoteMachineEditable { get { return runRemote; } }

        private readonly SolidColorBrush unselectedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
        private readonly SolidColorBrush selectedBrush = new SolidColorBrush(Colors.DodgerBlue);

        public Brush LocalTargetButtonBrush { get { return runRemote ? unselectedBrush : selectedBrush; } set { return; } }
        public Brush RemoteTargetButtonBrush { get { return runRemote ? selectedBrush : unselectedBrush; } set { return; } }
        public Brush RemoteSourceButtonBrush { get { return useDropDirectory ? selectedBrush : unselectedBrush; } set { return; } }
        public Brush LocalSourceButtonBrush { get { return useDropDirectory ? unselectedBrush : selectedBrush; } set { return; } }

        private bool runRemote = false;
        private bool useDropDirectory = true;

        #region Commands
        private ICommand setLocalCommand;

        public ICommand SetLocalCommand {
            get { return setLocalCommand ?? (setLocalCommand = new ActionCommand(SetLocalAction)); }
        }
        private void SetLocalAction() {
            runRemote = false;
            EnvironmentManifestPath = TestInvoker.GuessEnvironmentManifestPath(EnvironmentManifestPath, runRemote, RemoteMachineName);
            OnPropertyChanged("LocalTargetButtonBrush");
            OnPropertyChanged("RemoteTargetButtonBrush");
            OnPropertyChanged("IsRemoteMachineEditable");
        }
        private ICommand setRemoteCommand;
        public ICommand SetRemoteCommand {
            get { return setRemoteCommand ?? (setRemoteCommand = new ActionCommand(SetRemoteAction)); }
        }
        private void SetRemoteAction() {
            runRemote = true;
            EnvironmentManifestPath = TestInvoker.GuessEnvironmentManifestPath(EnvironmentManifestPath, runRemote, RemoteMachineName);
            OnPropertyChanged("LocalTargetButtonBrush");
            OnPropertyChanged("RemoteTargetButtonBrush");
            OnPropertyChanged("IsRemoteMachineEditable");
        }
        private ICommand useDropDirectoryCommand;
        public ICommand UseDropDirectoryCommand {
            get { return useDropDirectoryCommand ?? (useDropDirectoryCommand = new ActionCommand(UseDropDirectoryAction)); }
        }
        private void UseDropDirectoryAction() {
            useDropDirectory = true;
            UpdateDllList();
            OnPropertyChanged("RemoteSourceButtonBrush");
            OnPropertyChanged("LocalSourceButtonBrush");
        }
        private ICommand useLocalDirectoryCommand;
        public ICommand UseLocalDirectoryCommand {
            get { return useLocalDirectoryCommand ?? (useLocalDirectoryCommand = new ActionCommand(UseLocalDirectoryAction)); }
        }
        private void UseLocalDirectoryAction() {
            useDropDirectory = false;
            UpdateDllList();
            OnPropertyChanged("RemoteSourceButtonBrush");
            OnPropertyChanged("LocalSourceButtonBrush");
        }
        private bool enableBackup = true;
        public bool EnableBackup {
            get { return enableBackup; }
            set {
                enableBackup = value;
                OnPropertyChanged("EnableBackup");
            }
        }
        private ICommand runTestsCommand;
        [NotNull]
        public ICommand RunTestsCommand {
            get { return runTestsCommand ?? (runTestsCommand = new ActionCommand(RunTestsAction)); }
        }
        private void RunTestsAction() {
            if (string.IsNullOrWhiteSpace(EnvironmentManifestPath)) {
                pageController.Logger.AppendTestRunLog("Please enter an environment manifest.");
                return;
            }
            IEnumerable<string> dllNames = TestDllCollection.Where(i => i != null && i.IsSelected).Select(i => i.DllName);
            pageController.PickTestResultsPage();
            pageController.Invoker.Setup(EnvironmentManifestPath, !useDropDirectory, dllNames, EnableBackup, runRemote, RemoteMachineName);
        }

        

        #endregion Commands

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
            if (pageController != null) {
                pageController.Invoker.PropertyChanged -= InvokerOnPropertyChanged;
            }
        }
        #endregion
    }
}

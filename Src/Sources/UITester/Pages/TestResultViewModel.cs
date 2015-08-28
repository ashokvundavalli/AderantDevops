using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using UITester.Annotations;

namespace UITester.Pages {
    public class TestResultViewModel : INotifyPropertyChanged, IDisposable {
        public TestResultViewModel(PageController pageController) {
            this.pageController = pageController;
            SelectedTab = LogTab.TestRunLog;
            pageController.Logger.PropertyChanged += LoggerOnPropertyChanged;
        }

        private void LoggerOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            if (propertyChangedEventArgs != null && pageController != null && pageController.Logger != null) {
                switch (SelectedTab) {
                    case LogTab.GetFrameworkLog:
                        if (propertyChangedEventArgs.PropertyName == "GetFrameworkLog") {
                            DisplayLog = pageController.Logger.GetFrameworkLog;
                        }
                        break;
                    case LogTab.GetTestsLog:
                        if (propertyChangedEventArgs.PropertyName == "GetTestsLog") {
                            DisplayLog = pageController.Logger.GetTestsLog;
                        }
                        break;
                    case LogTab.SqlLog:
                        if (propertyChangedEventArgs.PropertyName == "SqlLog") {
                            DisplayLog = pageController.Logger.SqlLog;
                        }
                        break;
                    case LogTab.TestRunLog:
                        if (propertyChangedEventArgs.PropertyName == "TestRunLog") {
                            DisplayLog = pageController.Logger.TestRunLog;
                        }
                        break;
                    case LogTab.DatabaseManagementLog:
                        if (propertyChangedEventArgs.PropertyName == "DatabaseManagementLog") {
                            DisplayLog = pageController.Logger.DatabaseManagementLog;
                        }
                        break;
                }
            }
        }

        private LogTab selectedTab;
        public LogTab SelectedTab {
            get { return selectedTab; }
            set {
                selectedTab = value;
                if (pageController != null && pageController.Logger != null) {
                    switch (selectedTab) {
                        case LogTab.GetFrameworkLog:
                            DisplayLog = pageController.Logger.GetFrameworkLog;
                            break;
                        case LogTab.GetTestsLog:
                            DisplayLog = pageController.Logger.GetTestsLog;
                            break;
                        case LogTab.SqlLog:
                            DisplayLog = pageController.Logger.SqlLog;
                            break;
                        case LogTab.TestRunLog:
                            DisplayLog = pageController.Logger.TestRunLog;
                            break;
                        case LogTab.DatabaseManagementLog:
                            DisplayLog = pageController.Logger.DatabaseManagementLog;
                            break;
                        default:
                            DisplayLog = string.Empty;
                            break;
                    }
                } else {
                    DisplayLog = "ERROR: unable to retrieve logs. Null Reference Exception.";
                }
                OnPropertyChanged("SelectedTab");
                OnPropertyChanged("SqlBackground");
                OnPropertyChanged("GetFrameworkBackground");
                OnPropertyChanged("GetTestsBackground");
                OnPropertyChanged("RunTestsBackground");
                OnPropertyChanged("DatabaseManagementBackground");
            }
        }

        private readonly PageController pageController;
        private string displayLog;
        public string DisplayLog {
            get { return displayLog; }
            private set {
                displayLog = value;
                OnPropertyChanged("DisplayLog");
            }
        }

        private readonly SolidColorBrush runningBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454548"));
        private readonly SolidColorBrush errorBackground = new SolidColorBrush(Colors.LightSteelBlue);
        private readonly SolidColorBrush successBackground = new SolidColorBrush(Colors.MediumSpringGreen);
        private readonly SolidColorBrush selectedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF050508"));

        public SolidColorBrush SqlBackground { get { return SelectedTab == LogTab.SqlLog ? selectedBackground : runningBackground; } }
        public SolidColorBrush GetFrameworkBackground { get { return SelectedTab == LogTab.GetFrameworkLog ? selectedBackground : runningBackground; } }
        public SolidColorBrush GetTestsBackground { get { return SelectedTab == LogTab.GetTestsLog ? selectedBackground : runningBackground; } }
        public SolidColorBrush RunTestsBackground { get { return SelectedTab == LogTab.TestRunLog ? selectedBackground : runningBackground; } }
        public SolidColorBrush DatabaseManagementBackground { get { return SelectedTab == LogTab.DatabaseManagementLog ? selectedBackground : runningBackground; } }
        
        #region Commands
        private ICommand pickSqlLogCommand;
        public ICommand PickSqlLogCommand {
            get { return pickSqlLogCommand ?? (pickSqlLogCommand = new ActionCommand(() => SelectedTab = LogTab.SqlLog)); }
        }

        private ICommand pickGetFrameworkLogCommand;
        public ICommand PickGetFrameworkLogCommand {
            get { return pickGetFrameworkLogCommand ?? (pickGetFrameworkLogCommand = new ActionCommand(() => SelectedTab = LogTab.GetFrameworkLog)); }
        }

        private ICommand pickGetTestsLogCommand;
        public ICommand PickGetTestsLogCommand {
            get { return pickGetTestsLogCommand ?? (pickGetTestsLogCommand = new ActionCommand(() => SelectedTab = LogTab.GetTestsLog)); }
        }

        private ICommand pickTestRunLogCommand;
        public ICommand PickTestRunLogCommand {
            get { return pickTestRunLogCommand ?? (pickTestRunLogCommand = new ActionCommand(() => SelectedTab = LogTab.TestRunLog)); }
        }

        private ICommand pickDatabaseManagementLogCommand;
        public ICommand PickDatabaseManagementLogCommand {
            get { return pickDatabaseManagementLogCommand ?? (pickDatabaseManagementLogCommand = new ActionCommand(() => SelectedTab = LogTab.DatabaseManagementLog)); }
        }
        #endregion Commands

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChangedEventHandler handler = PropertyChanged;
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
            pageController.Logger.PropertyChanged -= LoggerOnPropertyChanged;
        }
        #endregion
    }
}

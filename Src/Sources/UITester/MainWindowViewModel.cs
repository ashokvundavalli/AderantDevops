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
using UITester.Pages;

namespace UITester {
    public class MainWindowViewModel : INotifyPropertyChanged, IDisposable {
        public MainWindowViewModel() {
            pageController = new PageController(this);
            pageController.PickTestSetupPage();
            SetupButtonText = "Setup";
            CancelButtonText = "Cancel";
            pageController.Invoker.PropertyChanged += InvokerOnPropertyChanged;
            
        }

        private void InvokerOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            if (propertyChangedEventArgs != null && propertyChangedEventArgs.PropertyName == "IsTaskRunning") {
                OnPropertyChanged("CanCancel");
                OnPropertyChanged("ActionButtonText");
            }
        }

        [NotNull]
        private readonly PageController pageController;

        private SelectedPage selectedPage;
        public SelectedPage SelectedPage {
            get { return selectedPage; }
            set {
                selectedPage = value;
                OnPropertyChanged("SelectedPage");
                OnPropertyChanged("SetupBackground");
                OnPropertyChanged("LogBackground");
                OnPropertyChanged("CancelBackground");
                OnPropertyChanged("DbManagementBackground");
            }
        }

        private string setupButtonText;
        public string SetupButtonText {
            get { return setupButtonText; }
            set {
                setupButtonText = value;
                OnPropertyChanged("SetupButtonText");
            }
        }
        private ICommand pickSetupCommand;
        public ICommand PickSetupCommand {
            get { return pickSetupCommand ?? (pickSetupCommand = new ActionCommand(PickSetupAction)); }
        }
        private void PickSetupAction() {
            pageController.PickTestSetupPage();
        }

        private ICommand pickLogButtonCommand;
        public ICommand PickLogButtonCommand {
            get { return pickLogButtonCommand ?? (pickLogButtonCommand = new ActionCommand(PickLogButtonAction)); }
        }
        private void PickLogButtonAction() {
            pageController.PickTestResultsPage();
        }

        private ICommand pickDbManagementCommand;
        public ICommand PickDbManagementCommand {
            get { return pickDbManagementCommand ?? (pickDbManagementCommand = new ActionCommand(PickDbManagementAction)); }
        }
        private void PickDbManagementAction() {
            pageController.PickDbManagementPage();
        }

        private string cancelButtonText;
        public string CancelButtonText {
            get { return cancelButtonText; }
            set {
                cancelButtonText = value;
                OnPropertyChanged("CancelButtonText");
            }
        }
        private ICommand cancelButtonCommand;
        public ICommand CancelButtonCommand {
            get { return cancelButtonCommand ?? (cancelButtonCommand = new ActionCommand(CancelButtonAction)); }
        }
        private void CancelButtonAction() {
            pageController.Invoker.CancelAllTasks();
        }

        public bool CanCancel {
            get { return pageController.Invoker.IsTaskRunning; }
        }

        private readonly SolidColorBrush unselectedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF454548"));
        private readonly SolidColorBrush errorBackground = new SolidColorBrush(Colors.LightSteelBlue);
        private readonly SolidColorBrush successBackground = new SolidColorBrush(Colors.MediumSpringGreen);
        private readonly SolidColorBrush selectedBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF101012"));

        public SolidColorBrush SetupBackground { get { return SelectedPage == SelectedPage.TestSetup ? selectedBackground : unselectedBackground; } }
        public SolidColorBrush LogBackground { get { return SelectedPage == SelectedPage.TestResult ? selectedBackground : unselectedBackground; } }
        public SolidColorBrush DbManagementBackground { get { return SelectedPage == SelectedPage.DatabaseManagement ? selectedBackground : unselectedBackground; } }
        public SolidColorBrush CancelBackground { get { return unselectedBackground; } }
        
        private object displayContent;
        public object DisplayContent {
            get { return displayContent; }
            set {
                displayContent = value;
                OnPropertyChanged("DisplayContent");
            }
        }
        
        public string Title {
            get { return "UI Test Runner"; }
        }

        public string ActionButtonText {
            get { return pageController.Invoker.IsTaskRunning ? "CANCEL" : "RUN TESTS"; }
        }
        private ICommand actionButtonCommand;
        public ICommand ActionButtonCommand {
            get { return actionButtonCommand ?? (actionButtonCommand = new ActionCommand(ActionButtonAction)); }
        }
        private void ActionButtonAction() {
            if (pageController.Invoker.IsTaskRunning) {
                pageController.Invoker.CancelAllTasks();
            } else {
                pageController.StartTests();
            }
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
        //Arguably this isn't needed bacause both the PageController and the Invoker are readonly and will never be destroyed until process exit.
        public void Dispose() {
            pageController.Invoker.PropertyChanged -= InvokerOnPropertyChanged;
        }
        #endregion
    }
}

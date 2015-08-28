using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UITester.Annotations;

namespace UITester.Pages {
    public class PageController {
        private readonly MainWindowViewModel mainViewModel;
        public PageController(MainWindowViewModel mainWindowViewModel) {
            mainViewModel = mainWindowViewModel;
            logger = new SegregatedLogger();
            invoker = new TestInvoker(logger);
            testResultPage = new TestResultViewModel(this);
        }

        private SegregatedLogger logger;
        public SegregatedLogger Logger { get { return logger; } }
        private TestSetupViewModel testSetupPage;
        private TestResultViewModel testResultPage;
        private DatabaseManagementViewModel databaseManagementPage;

        [NotNull]
        private readonly TestInvoker invoker;
        [NotNull]
        internal TestInvoker Invoker { get { return invoker; } }
        
        public void PickTestSetupPage() {
            mainViewModel.DisplayContent = testSetupPage ?? (testSetupPage = new TestSetupViewModel(this));
            mainViewModel.SelectedPage = SelectedPage.TestSetup;
        }

        public void PickDbManagementPage() {
            mainViewModel.DisplayContent = databaseManagementPage ?? (databaseManagementPage = new DatabaseManagementViewModel(this));
            mainViewModel.SelectedPage = SelectedPage.DatabaseManagement;
        }

        public void PickTestResultsPage() {
            mainViewModel.DisplayContent = testResultPage ?? (testResultPage = new TestResultViewModel(this));
            mainViewModel.SelectedPage = SelectedPage.TestResult;
            testResultPage.SelectedTab = LogTab.TestRunLog;
        }

        public void StartTests() {
            if (testSetupPage != null) {
                testSetupPage.RunTestsCommand.Execute(null);
            }
        }
    }
}

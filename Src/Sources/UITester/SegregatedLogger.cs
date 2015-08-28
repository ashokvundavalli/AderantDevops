using System.ComponentModel;
using System.Runtime.CompilerServices;
using UITester.Annotations;

namespace UITester {
    public class SegregatedLogger : INotifyPropertyChanged {

        public SegregatedLogger() {
            getFrameworkLog = new TabLogger("GetFramework");
            getTestsLog = new TabLogger("GetTests");
            sqlLog = new TabLogger("Sql");
            testRunLog = new TabLogger("TestRun");
            databaseManagementLog = new TabLogger("DatabaseManagement");
        }

        private TabLogger getFrameworkLog;
        private TabLogger getTestsLog;
        private TabLogger sqlLog;
        private TabLogger testRunLog;
        private TabLogger databaseManagementLog;

        public string GetTestsLog { get { return getTestsLog != null ? getTestsLog.Log : null; } }
        public string TestRunLog { get { return testRunLog != null ? testRunLog.Log : null; } }
        public string GetFrameworkLog { get { return getFrameworkLog != null ? getFrameworkLog.Log : null; } }
        public string SqlLog { get { return sqlLog != null ? sqlLog.Log : null; } }
        public string DatabaseManagementLog { get { return databaseManagementLog != null ? databaseManagementLog.Log : null; } }
        public TabLogger DatabaseManagementTabLogger { get { return databaseManagementLog; } }

        public void AppendGetFrameworkLog(string lines) {
            if (getFrameworkLog == null) {
                getFrameworkLog = new TabLogger("GetFramework");
            }
            getFrameworkLog.AppendLog(lines);
            OnPropertyChanged("GetFrameworkLog");
        }

        public void AppendSqlLog(string lines) {
            if (sqlLog == null) {
                sqlLog = new TabLogger("Sql");
            }
            sqlLog.AppendLog(lines);
            OnPropertyChanged("SqlLog");
        }

        public void AppendTestRunLog(string lines) {
            if (testRunLog == null) {
                testRunLog = new TabLogger("TestRun");
            }
            testRunLog.AppendLog(lines);
            OnPropertyChanged("TestRunLog");
        }

        public void AppendGetTestsLog(string lines) {
            if (getTestsLog == null) {
                getTestsLog = new TabLogger("GetTests");
            }
            getTestsLog.AppendLog(lines);
            OnPropertyChanged("GetTestsLog");
        }

        public void AppendDatabaseManagementLog(string lines) {
            if (databaseManagementLog == null) {
                databaseManagementLog = new TabLogger("DatabaseManagement");
            }
            databaseManagementLog.AppendLog(lines);
            OnPropertyChanged("DatabaseManagementLog");
        }

        public void ClearGetFrameworkLog() {
            if (getFrameworkLog != null) {
                getFrameworkLog.ClearLog();
                OnPropertyChanged("GetFrameworkLog");
            }
        }
        public void ClearGetTestsLog() {
            if (getTestsLog != null) {
                getTestsLog.ClearLog();
                OnPropertyChanged("GetTestsLog");
            }
        }
        public void ClearSqlLog() {
            if (sqlLog != null) {
                sqlLog.ClearLog();
                OnPropertyChanged("SqlLog");
            }
        }
        public void ClearTestRunLog() {
            if (testRunLog != null) {
                testRunLog.ClearLog();
                OnPropertyChanged("TestRunLog");
            }
        }
        public void ClearDatabaseManagementLog() {
            if (databaseManagementLog != null) {
                databaseManagementLog.ClearLog();
                OnPropertyChanged("DatabaseManagementLog");
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
    }
}

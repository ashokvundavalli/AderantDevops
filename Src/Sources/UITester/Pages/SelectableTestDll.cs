using System.ComponentModel;
using System.Runtime.CompilerServices;
using UITester.Annotations;
using System.IO;

namespace UITester.Pages {
    public class SelectableTestDll : INotifyPropertyChanged {

        public SelectableTestDll(string path) {
            isSelected = false;
            dllPath = path;
            dllName = Path.GetFileName(path);
        }

        private string dllName;
        private string dllPath;
        private bool isSelected;

        public string DllName {
            get { return dllName; }
            private set {
                dllName = value;
                OnPropertyChanged("DllName");
            }
        }

        public string DllPath {
            get { return dllPath; }
            private set {
                dllPath = value;
                OnPropertyChanged("DllPath");
            }
        }

        public bool IsSelected {
            get { return isSelected; }
            set {
                isSelected = value;
                OnPropertyChanged("IsSelected");
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

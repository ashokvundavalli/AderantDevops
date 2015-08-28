using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UITester.Annotations;

namespace UITester {
    public class TabLogger : INotifyPropertyChanged {
        public TabLogger(string name) {
            logName = name;
        }

        private readonly string logName;
        public string LogName {
            get { return logName; }
        }

        [NotNull]
        private readonly object syncLock = new object();
        private StringBuilder stringBuilder;
        private string buffer;
        private bool isBufferCurrent;

        public string Log {
            get {
                lock (syncLock) {
                    if (stringBuilder == null) {
                        return null;
                    }
                    if (isBufferCurrent) {
                        return buffer;
                    }
                    isBufferCurrent = true;
                    return (buffer = stringBuilder.ToString());
                }
                
            }
        }
        public void AppendLog(string lines) {
            lock (syncLock) {
                if (stringBuilder == null) {
                    stringBuilder = new StringBuilder();
                }
                isBufferCurrent = false;
                stringBuilder.AppendLine(lines);
            }
            OnPropertyChanged("Log");
        }
        public void ClearLog() {
            lock (syncLock) {
                if (stringBuilder != null) {
                    stringBuilder.Clear();
                }
            }
            OnPropertyChanged("Log");
        }

        #region INofityPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var handler = PropertyChanged;
            if (handler != null) {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion INofityPropertyChanged
    }
}

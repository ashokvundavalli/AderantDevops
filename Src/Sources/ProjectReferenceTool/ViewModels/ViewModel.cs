using System.ComponentModel;

namespace ProjectReferenceTool.ViewModels {

    /// <summary>
    /// Base View Model
    /// </summary>
    public class ViewModel : INotifyPropertyChanged {

        #region INotifyPropertyChanged implementation

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies the property changed.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        protected void OnPropertyChanged(string propertyName) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion	

    }
}
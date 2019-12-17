using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UpdateDependencyReferences;

namespace ProjectReferenceTool.ViewModels {
    /// <summary>
    /// Reference View Model
    /// </summary>
    public class ReferenceViewModel : ViewModel {

        private string project;

        /// <summary>
        /// Gets or sets the project.
        /// </summary>
        public string Project {
            get { return project; }
            set {
                if (project != value) {
                    project = value;
                    OnPropertyChanged(nameof(Project));
                }
            }
        }

        /// <summary>
        /// Gets the name of the project.
        /// </summary>
        public string ProjectName {
            get { return Path.GetFileName(Project); }
        }

        private string assembly;

        /// <summary>
        /// Gets or sets the assembly.
        /// </summary>
        public string Assembly {
            get { return assembly; }
            set {
                if (assembly != value) {
                    assembly = value;
                    OnPropertyChanged(nameof(Assembly));
                }
            }
        }

        private string currentValue;

        /// <summary>
        /// Gets or sets the current value.
        /// </summary>
        public string CurrentValue {
            get { return currentValue; }
            set {
                if (currentValue != value) {
                    currentValue = value;
                    OnPropertyChanged(nameof(CurrentValue));
                }
            }
        }

        private bool update;

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ReferenceViewModel"/> is update.
        /// </summary>
        public bool Update {
            get { return update; }
            set {
                if (update != value) {
                    update = value;

                    // reset new value
                    if (!update) {
                        Reset();
                    }

                    OnPropertyChanged(nameof(Update));
                }
            }
        }

        private bool isSaved;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is saved.
        /// </summary>
        public bool IsSaved {
            get { return isSaved; }
            set {
                if (isSaved != value) {
                    isSaved = value;
                    OnPropertyChanged(nameof(IsSaved));
                }
            }
        }
       
        private string newValue;

        /// <summary>
        /// Gets or sets the new value.
        /// </summary>
        public string NewValue {
            get { return newValue; }
            set {
                if (newValue != value) {
                    newValue = value;

                    Update = newValue != CurrentValue;

                    OnPropertyChanged(nameof(NewValue));
                }
            }
        }

        private ObservableCollection<PackageAssemblyInfo> options;

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public ObservableCollection<PackageAssemblyInfo> Options {
            get { return options; }
            set {
                if (options != value) {
                    options = value;
                    OnPropertyChanged(nameof(Options));
                }
            }
        }


        /// <summary>
        /// Selects the best match.
        /// </summary>
        public void BestMatch() {

            var match = Options
                        .FirstOrDefault(v => String.Equals(Assembly, v.FileName, StringComparison.CurrentCultureIgnoreCase));

            if (match != null) {
                if (match.RelativeFileName != CurrentValue) {
                    Update = true;
                }
                NewValue = match.RelativeFileName;
            }

        }

        /// <summary>
        /// Resets the new value to the current value.
        /// </summary>
        private void Reset() {
            NewValue = CurrentValue;
        }

    }
}

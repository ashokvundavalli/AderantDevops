using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Aderant.PresentationFramework.Input;
using UpdateDependencyReferences;

namespace ProjectReferenceTool.ViewModels {

    /// <summary>
    /// Main View Model
    /// </summary>
    /// <seealso cref="ProjectReferenceTool.ViewModels.ViewModel" />
    public class MainViewModel : ViewModel {

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel() {
            SolutionFolder = @"C:\Source\Time";
        }

        private string solutionFolder;

        /// <summary>
        /// Gets or sets the solution folder.
        /// </summary>
        public string SolutionFolder {
            get { return solutionFolder; }
            set {
                if (solutionFolder != value) {
                    solutionFolder = value;
                    OnPropertyChanged(nameof(SolutionFolder));
                }
            }
        }

        private ICommand openSolutionCommand;

        /// <summary>
        /// Command for opening a solution path.
        /// </summary>
        public ICommand OpenSolutionCommand {
            get { return openSolutionCommand ?? (openSolutionCommand = new DelegateCommand(OnOpenSolution)); }
        }

        private ObservableCollection<ReferenceViewModel> references;

        /// <summary>
        /// Gets or sets the references.
        /// </summary>
        public ObservableCollection<ReferenceViewModel> References {
            get { return references; }
            set {
                if (references != value) {
                    references = value;
                    OnPropertyChanged(nameof(References));
                }
            }
        }
        
        private async void OnOpenSolution() {
            References = await Task.Run(() => new ObservableCollection<ReferenceViewModel>(Resolver.GetReferences(SolutionFolder).ToList()));
        }

        private ICommand applyChangesCommand;

        /// <summary>
        /// Gets the command for appling changes.
        /// </summary>
        public ICommand ApplyChangesCommand {
            get { return applyChangesCommand ?? (applyChangesCommand = new DelegateCommand(OnApplyChanges)); }
        }

        private void OnApplyChanges() {
            Resolver.ApplyChanges(References);
            RemoveSaved();
        }

        private ICommand applyToAllCommand;

        /// <summary>
        /// Gets the command to apply all changes.
        /// </summary>
        public ICommand ApplyToAllCommand {
            get { return applyToAllCommand ?? (applyToAllCommand = new DelegateCommand<ReferenceViewModel>(OnApplyToAll)); }
        }

        private void OnApplyToAll(ReferenceViewModel referenceTemplate) {
            foreach (var reference in References) {
                if (reference.Assembly == referenceTemplate.Assembly) {
                    reference.CurrentValue = referenceTemplate.CurrentValue;
                    reference.Update = referenceTemplate.Update;
                }
            }
        }

        private ICommand applyCommand;

        /// <summary>
        /// Gets the apply command.
        /// </summary>
        public ICommand ApplyCommand {
            get { return applyCommand ?? (applyCommand = new DelegateCommand<object>(OnApply)); }
        }

        private void OnApply(object items) {

            ReadOnlyObservableCollection<object> collection = items as ReadOnlyObservableCollection<object>;
            if (collection == null) {
                return;
            }
            
            Resolver.ApplyChanges(collection.OfType<ReferenceViewModel>());
            RemoveSaved();
        }

        private void RemoveSaved() {
            foreach (var reference in References.Where(v => v.IsSaved).ToList()) {
                References.Remove(reference);
            }
        }
    }
}

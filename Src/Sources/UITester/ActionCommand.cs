using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace UITester {

    public class ActionCommand : ICommand {

        private readonly Func<bool> canExecuteCallback;
        private readonly Action executeCallback;
        public event EventHandler CanExecuteChanged;
        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        public void Execute(object parameter) {
            this.executeCallback();
        }
        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <returns>
        /// true if this command can be executed; otherwise, false.
        /// </returns>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        public bool CanExecute(object parameter) {
            return this.canExecuteCallback == null || this.canExecuteCallback();
        }

        public ActionCommand(Action executeCallback) {
            if (executeCallback == null) throw new ArgumentNullException("executeCallback");
            this.executeCallback = executeCallback;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionCommand"/> class.
        /// </summary>
        /// <param name="executeCallback">The <see cref="T:System.Action"/> to call when the command is executed.</param><param name="canExecuteCallback">The function to call when the command needs to evaluate whether it can execute.</param>
        public ActionCommand(Action executeCallback, Func<bool> canExecuteCallback) : this(executeCallback) {
            if (canExecuteCallback == null) throw new ArgumentNullException("canExecuteCallback");
            this.canExecuteCallback = canExecuteCallback;
        }
    }
}

using System;
using System.Windows.Input;

namespace Aderant.PresentationFramework.Input {
    
    /// <summary>
    /// Defines an <see cref="ICommand"/> that executes callbacks (delegates) for both execute and can execute functionality.
    /// </summary>
    public sealed class DelegateCommand : ICommand {
        private readonly Func<bool> canExecuteCallback;
        private readonly Action executeCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="executeCallback">The <see cref="Action"/> to call when the command is executed.</param>
        public DelegateCommand(Action executeCallback) {
            if (executeCallback == null) {
                throw new ArgumentNullException("executeCallback");
            }

            this.executeCallback = executeCallback;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="executeCallback">The <see cref="Action"/> to call when the command is executed.</param>
        /// <param name="canExecuteCallback">The function to call when the command needs to evaluate whether it can execute.</param>
        public DelegateCommand(Action executeCallback, Func<bool> canExecuteCallback)
            : this(executeCallback) {
            if (canExecuteCallback == null) {
                throw new ArgumentNullException("canExecuteCallback");
            }

            this.canExecuteCallback = canExecuteCallback;
        }

        /// <summary>
        /// Determines whether this command can execute.
        /// </summary>
        /// <returns>
        /// 	<c>true</c> if this command can execute; otherwise, <c>false</c>.
        /// </returns>
        public bool CanExecute() {
            return this.canExecuteCallback != null ? this.canExecuteCallback() : true;
        }

        bool ICommand.CanExecute(object parameter) {
            return CanExecute();
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        public void Execute() {
            this.executeCallback();
        }

        void ICommand.Execute(object parameter) {
            Execute();
        }

        event EventHandler ICommand.CanExecuteChanged
        {
            add
            {
                if (canExecuteCallback != null) {
                    CommandManager.RequerySuggested += value;
                }
            }

            remove
            {
                if (canExecuteCallback != null) {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }
    }

}


namespace Aderant.PresentationFramework.Input {
    /// <summary>
    /// Defines an <see cref="ICommand"/> that executes callbacks (delegates) for both execute and can execute functionality.
    /// </summary>
    /// <typeparam name="T">The type of parameter passed in the execute and can execute callbacks.</typeparam>
    public sealed class DelegateCommand<T> : ICommand {
        private readonly Predicate<T> canExecuteCallback;
        private readonly Action<T> executeCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand{T}"/> class.
        /// </summary>
        /// <param name="executeCallback">The <see cref="Action"/> to call when the command is executed.</param>
        public DelegateCommand(Action<T> executeCallback) {
            if (executeCallback == null) {
                throw new ArgumentNullException("executeCallback");
            }

            this.executeCallback = executeCallback;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand{T}"/> class.
        /// </summary>
        /// <param name="executeCallback">The <see cref="Action"/> to call when the command is executed.</param>
        /// <param name="canExecuteCallback">The function to call when the command needs to evaluate whether it can execute.</param>
        public DelegateCommand(Action<T> executeCallback, Predicate<T> canExecuteCallback)
            : this(executeCallback) {
            if (canExecuteCallback == null) {
                throw new ArgumentNullException("canExecuteCallback");
            }

            this.canExecuteCallback = canExecuteCallback;
        }

        /// <summary>
        /// Determines whether this command can execute.
        /// </summary>
        /// <returns>
        /// 	<c>true</c> if this command can execute; otherwise, <c>false</c>.
        /// </returns>
        public bool CanExecute(T parameter) {
            return this.canExecuteCallback != null ? this.canExecuteCallback(parameter) : true;
        }

        bool ICommand.CanExecute(object parameter) {
            if (this.canExecuteCallback == null) {
                return true;
            }

            if (parameter != null && !(typeof(T).IsAssignableFrom(parameter.GetType()))) {
                throw new ArgumentException("Command parameter must be of type " + typeof(T).FullName);
            }

            return CanExecute(parameter == null ? default(T) : (T)parameter);
        }

        /// <summary>
        /// Executes the command with the specified parameter.
        /// </summary>
        /// <param name="parameter">The parameter.</param>
        public void Execute(T parameter) {
            this.executeCallback(parameter);
        }

        void ICommand.Execute(object parameter) {
            if (parameter != null && !(typeof(T).IsAssignableFrom(parameter.GetType()))) {
                throw new ArgumentException("Command parameter must be of type " + typeof(T).FullName);
            }

            Execute(parameter == null ? default(T) : (T)parameter);
        }

        event EventHandler ICommand.CanExecuteChanged
        {
            add
            {
                if (canExecuteCallback != null) {
                    CommandManager.RequerySuggested += value;
                }
            }

            remove
            {
                if (canExecuteCallback != null) {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }
    }
}


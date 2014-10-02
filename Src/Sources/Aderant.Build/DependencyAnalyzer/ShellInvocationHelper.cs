using System.Collections.ObjectModel;
using System.Management.Automation;

namespace Aderant.Build.DependencyAnalyzer {
    internal static class ShellInvocationHelper {

        public static Collection<PSObject> InvokeCommand(PSCmdlet cmd, string function) {
            CommandInfo command = cmd.InvokeCommand.GetCommand(function, CommandTypes.Function);

            if (command != null) {
                return cmd.InvokeCommand.InvokeScript(command.Name);
            }

            return null;
        }

        public static Collection<PSObject> InvokeCommand(PSCmdlet cmd, string function, params object[] args) {
            CommandInfo command = cmd.InvokeCommand.GetCommand(function, CommandTypes.Function);

            if (command != null) {
                return cmd.InvokeCommand.InvokeScript(command.Name, args);
            }

            return null;
        }
    }
}
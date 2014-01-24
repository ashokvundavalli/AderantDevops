﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;

namespace DependencyAnalyzer {
    internal static class ShellInvocationHelper {

        public static Collection<PSObject> InvokeCommand(PSCmdlet cmd, string function) {
            CommandInfo command = cmd.InvokeCommand.GetCommand(function, CommandTypes.Function);

            if (command != null) {
                return cmd.InvokeCommand.InvokeScript(command.Name);
            }

            return null;
        }
    }
}
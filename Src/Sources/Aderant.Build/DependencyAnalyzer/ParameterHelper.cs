using System;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Provides helper methods for retrieving variables from the host PowerShell session
    /// </summary>
    public static class ParameterHelper {
        public static string GetBranchPath(string branchPathParameter, SessionState sessionState) {
            string branchPath = branchPathParameter ??
                                sessionState.PSVariable.GetValue("BranchLocalDirectory", string.Empty).ToString();

            if (branchPath.EndsWith("Modules")) {
                branchPath = branchPath.Replace("Modules", string.Empty);
            }

            if (string.IsNullOrEmpty(branchPath)) {
                throw new ArgumentException("BranchPath must be specified or there must be a variable $branchPath");
            }
            return branchPath;
        }

        /// <summary>
        /// Gets the current module path from the provided argument or the current host session.
        /// </summary>
        /// <param name="currentModule">The current module.</param>
        /// <param name="sessionState">State of the session.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">BranchPath must be specified or there must be a variable $CurrentModulePath</exception>
        public static string GetCurrentModulePath(string currentModule, SessionState sessionState) {
            string modulePath = currentModule ??
                                sessionState.PSVariable.GetValue("CurrentModulePath", string.Empty).ToString();

            if (string.IsNullOrEmpty(modulePath)) {
                throw new ArgumentException(
                    "No current module. One must be specified or there must be a variable $CurrentModulePath in the current host session.",
                    "currentModule");
            }

            return modulePath;
        }


        public static string GetCurrentModuleName(string defaultValue, SessionState sessionState) {
            string moduleName = sessionState.PSVariable.GetValue("CurrentModuleName", defaultValue).ToString();
            if (string.IsNullOrEmpty(moduleName)) {
                throw new ArgumentException("There must be a variable $CurrentModuleName in the current host session.");
            }
            return moduleName;
        }

        /// <summary>
        /// Gets the value of the $BranchLocalDirectory variable
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">There must be a variable $BranchLocalDirectory in the current host session.</exception>
        public static string GetBranchLocalDirectory(SessionState state) {
            string branchLocalDirectory = state.PSVariable.GetValue("BranchLocalDirectory", string.Empty).ToString();

            if (string.IsNullOrEmpty(branchLocalDirectory)) {
                throw new ArgumentException(
                    "There must be a variable $BranchLocalDirectory in the current host session.");
            }

            return branchLocalDirectory;
        }

        public static string GetBranchBinariesDirectory(SessionState state) {
            string branchBinariesDirectory =
                state.PSVariable.GetValue("BranchBinariesDirectory", string.Empty).ToString();
            if (string.IsNullOrEmpty(branchBinariesDirectory)) {
                throw new ArgumentException(
                    "There must be a variable $BranchBinariesDirectory in the current host session.");
            }
            return branchBinariesDirectory;
        }

        public static string BranchExpertSourceDirectory(SessionState state) {
            string branchExpertSourceDirectory =
                state.PSVariable.GetValue("BranchExpertSourceDirectory", string.Empty).ToString();
            if (string.IsNullOrEmpty(branchExpertSourceDirectory)) {
                throw new ArgumentException(
                    "There must be a variable $BranchExpertSourceDirectory in the current host session.");
            }
            return branchExpertSourceDirectory;
        }

        /// <summary>
        /// Gets the value of the $BranchName variable
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">There must be a variable $BranchName in the current host session.</exception>
        public static string GetBranchName(SessionState state) {
            var branchName = state.PSVariable.GetValue("BranchName", string.Empty).ToString();

            if (string.IsNullOrEmpty(branchName)) {
                throw new ArgumentException("There must be a variable $BranchName in the current host session.");
            }

            string[] parts = branchName.Split('\\');

            if (parts.Length == 1) {
                return parts[0];
            }

            string location = parts[0];
            string upper = char.ToUpper(location[0]).ToString(CultureInfo.InvariantCulture);

            return string.Concat(upper + location.Substring(1, location.Length - 1), @"\", parts[1]);
        }

        /// <summary>
        /// Gets the value of the $BranchModulesDirectory variable
        /// </summary>
        /// <param name="branchModulePath"></param>
        /// <param name="state">The state.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">There must be a variable $BranchLocalDirectory in the current host session.</exception>
        public static string GetBranchModulesDirectory(string branchModulePath, SessionState state) {
            var localDirectory = state.PSVariable.GetValue("BranchModulesDirectory", branchModulePath).ToString();

            if (string.IsNullOrEmpty(localDirectory)) {
                throw new ArgumentException(
                    "There must be a variable $BranchModulesDirectory in the current host session.");
            }

            return localDirectory;
        }

        /// <summary>
        /// Gets the drop from the current session path.
        /// </summary>
        /// <param name="targetBranch">The target branch.</param>
        /// <param name="sessionState">State of the session.</param>
        /// <returns></returns>
        public static string GetDropPath(string targetBranch, SessionState sessionState) {
            string currentDrop = sessionState.PSVariable.GetValue("BranchServerDirectory", string.Empty).ToString();
            string currentBranch = PathHelper.GetBranch(currentDrop);

            if (currentDrop.IndexOf(currentBranch, StringComparison.InvariantCultureIgnoreCase) > 0) {
                return currentDrop;
            }

            return Path.Combine(currentDrop.Replace(currentBranch, string.Empty), targetBranch);
        }
    }
}
using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// This module is for compiling LESS files.
    /// </summary>
    public class CompileLess : Task {
        public CompileLess() {
        }

        /// <summary>
        /// Entrance from ≤Target Name="CompileLess"... ≤LessFiles
        /// </summary>
        [Required]
        public ITaskItem[] LessFiles { get; set; }
       
        public override bool Execute() {
            BuildEngine4.Yield();

            Parallel.ForEach(
                LessFiles,
                item => {
                    var compiler = new LessCompilerTool {
                        BuildEngine = this.BuildEngine,
                        LessFile = item
                    };
                    
                    compiler.Execute();
                });

            BuildEngine4.Reacquire();
            return !Log.HasLoggedErrors;
        }
        
    }

    internal class LessCompilerTool : ToolTask {
        private static string pathToLessCompiler;

        static LessCompilerTool() {
            pathToLessCompiler = $@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\LessCompiler\lessc.cmd";
        }
      
        [Required]
        public ITaskItem LessFile { get; set; }

        protected override string GenerateFullPathToTool() {
            return pathToLessCompiler;
        }

        protected override string ToolName {
            get {
                return "lessc.cmd";
            }
        }

        protected override string GenerateCommandLineCommands() {
            CommandLineBuilderExtension commandLineBuilderExtension = new CommandLineBuilderExtension();

            commandLineBuilderExtension.AppendSwitchIfNotNull("-ru ", LessFile.GetMetadata("FullPath"));
            
            string cssOutputPath = Path.GetFullPath($"{LessFile.GetMetadata("RelativeDir")}{LessFile.GetMetadata("FileName")}.css");
            commandLineBuilderExtension.AppendSwitch(cssOutputPath);

            return commandLineBuilderExtension.ToString();
        }
    }
}

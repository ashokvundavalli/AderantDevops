using System;
using System.IO;

namespace Aderant.Build {
    internal class Program {
        private static void Main(string[] args) {
            if (args.Length > 0) {
                string path = args[0];

                path = path.Replace("@", string.Empty);

                if (path.EndsWith(".rsp") && File.Exists(path)) {
                    string commandLine = File.ReadAllText(path);
                    var wrapper = new FxCopWrapper(commandLine);
                    Environment.ExitCode = wrapper.Execute();
                }
            } else {
                throw new ArgumentOutOfRangeException("args", "No command line parameters were passed to the FxCop wrapper");
            }
        }
    }
}
using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class WaitForDebugger : Task {

        public bool Wait { get; set; }

        public override bool Execute() {
            if (userInteractive) {
                if (Wait) {
                    bool sleep = false;
                    SpinWait.SpinUntil(
                        () => {
                            if (sleep) {
                                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                            }

                            Log.LogMessage("Waiting for debugger... [C] to cancel waiting");
                            if (Console.KeyAvailable) {
                                var consoleKeyInfo = Console.ReadKey(true);
                                if (consoleKeyInfo.Key == ConsoleKey.C) {
                                    return true;
                                }
                            }

                            sleep = true;
                            return Debugger.IsAttached;
                        },
                        TimeSpan.FromSeconds(10));
                }
            }

            return !Log.HasLoggedErrors;
        }

        private static readonly bool userInteractive = Environment.UserInteractive;
    }
}

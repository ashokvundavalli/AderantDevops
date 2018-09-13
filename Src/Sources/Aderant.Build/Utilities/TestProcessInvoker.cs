using System;
using System.Diagnostics;

namespace Aderant.Build.Utilities {
    internal class TestProcessInvoker : ProcessInvoker {
        private ConsoleColor currentForegroundColor;

        public TestProcessInvoker(ProcessStartInfo startInfo)
            : base(startInfo) {

            OnOutputLine += singleLine => {
                if (!string.IsNullOrEmpty(singleLine)) {
                    if (ColorizeTestResultLine(singleLine))
                        return;

                    if (singleLine.StartsWith("Warning: ")) {
                        WriteInColor(singleLine, ConsoleColor.Yellow);
                        return;
                    }

                    WriteInColor(singleLine, currentForegroundColor);
                }
            };

            OnErrorLine += singleLine => {
                WriteInColor(singleLine, currentForegroundColor);
            };
        }

        public override void Start() {
            currentForegroundColor = Console.ForegroundColor;
            base.Start();
        }

        private bool ColorizeTestResultLine(string singleLine) {
            var firstChar = singleLine[0];

            if (firstChar == 'P' || firstChar == 'F' || firstChar == 'E' || firstChar == 'I' || firstChar == 'S') {
                bool isStatusMessage = false;
                bool isSuccessful = false;
                bool isSkipped = false;

                if (singleLine.StartsWith("Passed ")) {
                    isStatusMessage = true;
                    isSuccessful = true;
                } else if (singleLine.StartsWith("Failed ")) {
                    isStatusMessage = true;
                } else if (singleLine.StartsWith("Error ")) {
                    isStatusMessage = true;
                } else if (singleLine.StartsWith("Inconclusive ")) {
                    isStatusMessage = true;
                } else if (singleLine.StartsWith("Ignored ")) {
                    isStatusMessage = true;
                } else if (singleLine.StartsWith("Skipped ")) {
                    isStatusMessage = true;
                    isSkipped = true;
                }

                if (isStatusMessage) {
                    var resultAndTest = singleLine.Split(new[] { ' ' }, 2);

                    if (resultAndTest.Length == 2) {
                        if (isSuccessful) {
                            Console.ForegroundColor = ConsoleColor.Green;
                        } else if (isSkipped) {
                            Console.ForegroundColor = ConsoleColor.Gray;
                        } else {
                            Console.ForegroundColor = ConsoleColor.Red;
                        }

                        Console.Write(resultAndTest[0]);
                        Console.ForegroundColor = currentForegroundColor;
                        Console.WriteLine(resultAndTest[1]);
                        return true;
                    }
                }
            }

            return false;
        }

        private void WriteInColor(string singleLine, ConsoleColor color) {
            Console.ForegroundColor = color;
            Console.WriteLine(singleLine);
            Console.ForegroundColor = currentForegroundColor;
        }
    }
}

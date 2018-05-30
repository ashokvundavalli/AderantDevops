using System;
using Aderant.Build.Logging;

namespace PaketArtifactTool.ViewModels {
    public class ConsoleLogger : ILogger {

        public void Debug(string message, params object[] args) {
            WriteConsole(ConsoleColor.Gray, message, args);
        }

        public void Info(string message, params object[] args) {
            WriteConsole(ConsoleColor.White, message, args);
        }

        public void Warning(string message, params object[] args) {
            WriteConsole(ConsoleColor.Yellow, message, args);
        }

        public void Error(string message, params object[] args) {
            WriteConsole(ConsoleColor.Red, message, args);
        }

        public void WriteConsole(ConsoleColor color, string message, params object[] args) {
            var current = Console.ForegroundColor;
            try {
                Console.ForegroundColor = color;
                Console.WriteLine(message, args);
            } finally {
                Console.ForegroundColor = current;
            }
        }

    }
}
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Aderant.Build.Logging {
    internal static class LoggerExtensions {
        public static void LogErrorFromException(this ILogger logger, Exception exception) {
            LogErrorFromException(logger, exception, false);
        }

        public static void LogErrorFromException(this ILogger logger, Exception exception, bool showStackTrace) {
            LogErrorFromException(logger, exception, showStackTrace, false);
        }

        public static void LogErrorFromException(this ILogger logger, Exception exception, bool showStackTrace, bool showDetail, [CallerFilePath] string file = null) {
            string message = FormatErrorMessageFromException(exception, showStackTrace, showDetail, file);

            logger.Error(message);
        }

        internal static string FormatErrorMessageFromException(Exception exception, bool showStackTrace, bool showDetail, [CallerFilePath] string file = null) {
            string message;

            if (!showDetail) {
                message = exception.Message;

                if (showStackTrace) {
                    message += System.Environment.NewLine + exception.StackTrace;
                }
            }
            else {
                // The more comprehensive output, showing exception types
                // and inner exceptions
                StringBuilder builder = new StringBuilder(200);
                do {
                    builder.Append(exception.GetType().Name);
                    builder.Append(": ");
                    builder.AppendLine(exception.Message);
                    if (showStackTrace) {
                        builder.AppendLine(exception.StackTrace);
                    }
                    exception = exception.InnerException;
                } while (exception != null);

                message = builder.ToString();
            }

            return message;
        }
    }
}
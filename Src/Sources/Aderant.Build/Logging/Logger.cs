namespace Aderant.Build.Logging {
    public class Logger {

        public static ILogger GetLogger() {
            return new EventLogWriter();
        }
    }
}
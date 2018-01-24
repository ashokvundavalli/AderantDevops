using System;

namespace Aderant.Build.Analyzer.Lists.IDisposable {
    /// <summary>
    /// The IDisposable Rules reference this class and iterate through
    /// the below members when determining the severity of a potential violation.
    /// 
    /// Types referenced below will automatically be flagged as 'safe' during rule evaluation.
    /// To add an additional safe condition, simply add a new line to the relevant collection below, using the stated syntax.
    /// NOTE:
    ///     All types must be accompanied by a detailed comment explaining why they are listed.
    /// </summary>
    internal static class IDisposableWhitelist {
        // Syntax:
        // "<FullyQualifiedSignature>"
        public static readonly string[] Methods = {
            // These three methods are part of the UI automation testing framework and never run as live code.
            "TestStack.White.Utility.Retry.For(System.Func<bool>, System.TimeSpan, System.TimeSpan?)",                     // 1
            "TestStack.White.Utility.Retry.For<T>(System.Func<T>, System.TimeSpan, System.TimeSpan?)",                     // 2
            "TestStack.White.Utility.Retry.For<T>(System.Func<T>, System.Predicate<T>, System.TimeSpan, System.TimeSpan?)" // 3
        };

        // Syntax:
        // new Tuple<string, string>("<Namespace>", "<TypeName>")
        public static readonly Tuple<string, string>[] Types = {
            // Tasks only need to be disposed if AsyncAwaitHandle() is explicitly
            // called and the implementation does not have a finalizer.
            // This occurs in roughly 0.001% of use-cases.
            new Tuple<string, string>("System.Threading.Tasks", "Task"),

            // Object is a singleton.
            new Tuple<string, string>("Aderant.Framework.Communication.Client", "IBusinessEventRaise"),

            // Object is a singleton.
            new Tuple<string, string>("Aderant.Framework.Configuration", "IConfigurator"),

            // Object is a singleton.
            new Tuple<string, string>("Aderant.Messaging.Http", "INotificationClientConnection"),

            // Object is a singleton.
            new Tuple<string, string>("Aderant.Query", "IQueryServiceProxy"),
        };
    }
}

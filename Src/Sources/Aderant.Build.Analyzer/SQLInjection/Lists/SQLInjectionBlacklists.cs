using System;

namespace Aderant.Build.Analyzer.SQLInjection.Lists {
    /// <summary>
    /// The SQL Injection Code Analysis Rule references this class and iterates through
    /// the below members when determining the severity of a potential violation.
    /// 
    /// Types referenced below will automatically be flagged as 'error' conditions.
    /// To add an additional error condition, simply add a new line to the relevant collection below, using the stated syntax.
    /// </summary>
    internal static class SQLInjectionBlacklists {
        // Syntax:
        // new Tuple<string, string>("<PropertyName>", "<FullyQualifiedPropertySignature>")
        public static readonly Tuple<string, string>[] Properties = {
            new Tuple<string, string>("CommandText", "System.Data.Common.DbCommand.CommandText"),
            new Tuple<string, string>("CommandText", "System.Data.IDbCommand.CommandText"),
            new Tuple<string, string>("CommandText", "System.Data.SqlClient.SqlCommand.CommandText")
        };

        // Syntax:
        // new Tuple<string, string>("<MethodName>", "<StartOfFullyQualifiedMethodSignature>")
        public static readonly Tuple<string, string>[] Methods = {
            new Tuple<string, string>("SqlQuery", "System.Data.Entity.Database.SqlQuery<TElement>(string"),
            new Tuple<string, string>("IssueSql", "Aderant.FirmControl.DocuDraft.DataAccess.ISqlBase.IssueSql"),
            new Tuple<string, string>("Execute", "Aderant.Framework.Deployment.Controllers.IDatabaseController.Execute"),
            new Tuple<string, string>("ExecuteUsingIntegratedSecurity", "Aderant.Framework.Deployment.Controllers.IDatabaseController.ExecuteUsingIntegratedSecurity")
        };
    }
}

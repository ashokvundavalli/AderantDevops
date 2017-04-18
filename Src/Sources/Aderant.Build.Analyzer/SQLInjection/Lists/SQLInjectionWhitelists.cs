using System;

namespace Aderant.Build.Analyzer.SQLInjection.Lists {
    /// <summary>
    /// The SQL Injection Code Analysis Rule references this class and iterates through
    /// the below members when determining the severity of a potential violation.
    /// 
    /// Types referenced below will automatically be flagged as 'safe' during rule evaluation.
    /// To add an additional safe condition, simply add a new line to the relevant collection below, using the stated syntax.
    /// </summary>
    internal class SQLInjectionWhitelists {
        // Syntax:
        // new Tuple<string, string>("<MethodName>", "<FullyQualifiedMethodSignature>")
        public static readonly Tuple<string, string>[] Methods = {
            new Tuple<string, string>(
                "IssueSql",
                "Aderant.FirmControl.DocuDraft.DataAccess.SqlBase.IssueSql(System.Data.SqlClient.SqlConnection, string, System.Collections.Generic.List<System.Data.SqlClient.SqlParameter>, bool)"),
            new Tuple<string, string>(
                "IssueSqlNoBatch",
                "Aderant.FirmControl.DocuDraft.DataAccess.SqlBase.IssueSqlNoBatch(System.Data.SqlClient.SqlConnection, System.Text.StringBuilder, System.Collections.Generic.List<System.Data.SqlClient.SqlParameter>, bool, bool)"),
            new Tuple<string, string>(
                "IssueSqlNonQuery",
                "Aderant.FirmControl.DocuDraft.DataAccess.SqlBase.IssueSqlNonQuery(System.Data.SqlClient.SqlConnection, string, System.Collections.Generic.List<System.Data.SqlClient.SqlParameter>)"),
            new Tuple<string, string>(
                "IssueSqlToDataTable",
                "Aderant.FirmControl.DocuDraft.DataAccess.SqlBase.IssueSqlToDataTable(System.Data.SqlClient.SqlConnection, string, System.Collections.Generic.List<System.Data.SqlClient.SqlParameter>)")
        };

        // Syntax:
        // new Tuple<string, string>("<PropertyName>", "<PropertySignatureStringToMatch>")
        public static readonly Tuple<string, string>[] Properties = {
            // Empty.
        };
    }
}

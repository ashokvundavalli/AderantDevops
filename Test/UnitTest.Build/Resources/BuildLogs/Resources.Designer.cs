﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace UnitTest.Build.BuildLogProcessor {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("UnitTest.Build.Resources.BuildLogs.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {&quot;count&quot;:6330,&quot;value&quot;:[&quot;2016-09-06T14:24:37.6531197Z Checking if artifacts directory exists: D:\\1\\_work\\7\\a&quot;,&quot;2016-09-06T14:24:37.6531197Z Deleting artifacts directory.&quot;,&quot;2016-09-06T14:24:37.6560483Z Creating artifacts directory.&quot;,&quot;2016-09-06T14:24:37.6560483Z Checking if test results directory exists: D:\\1\\_work\\7\\TestResults&quot;,&quot;2016-09-06T14:24:37.6570245Z Deleting test results directory.&quot;,&quot;2016-09-06T14:24:37.6570245Z Creating test results directory.&quot;,&quot;2016-09-06T14:24:37.6745961Z Starting: Get so [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string buildlog_1 {
            get {
                return ResourceManager.GetString("buildlog_1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {&quot;count&quot;:6319,&quot;value&quot;:[&quot;2016-09-06T20:11:52.9126475Z Repository: Default&quot;,&quot;2016-09-06T20:11:52.9136241Z Version: &quot;,&quot;2016-09-06T20:11:52.9136241Z CustomRepository: &quot;,&quot;2016-09-06T20:11:52.9136241Z SYSTEM_TEAMPROJECT: ExpertSuite&quot;,&quot;2016-09-06T20:11:52.9146007Z SYSTEM_TEAMFOUNDATIONSERVERURI: http://tfs.ap.aderant.com:8080/tfs/ADERANT/&quot;,&quot;2016-09-06T20:11:52.9165539Z SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: http://tfs.ap.aderant.com:8080/tfs/ADERANT/&quot;,&quot;2016-09-06T20:11:52.9175305Z SYSTEM_COLLECTIONID: 5d9e5fa7-8899-4 [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string buildlog_2 {
            get {
                return ResourceManager.GetString("buildlog_2", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 2016-12-16T00:43:11.4297915Z C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -Command &quot;. ([scriptblock]::Create(&apos;if (!$PSHOME) { $null = Get-Item -LiteralPath &apos;&apos;variable:PSHOME&apos;&apos; } else { Import-Module -Name ([System.IO.Path]::Combine($PSHOME, &apos;&apos;Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1&apos;&apos;)) ; Import-Module -Name ([System.IO.Path]::Combine($PSHOME, &apos;&apos;Modules\Microsoft.PowerShell.Utility\Microsof [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string plain_text_build_log {
            get {
                return ResourceManager.GetString("plain_text_build_log", resourceCulture);
            }
        }
    }
}

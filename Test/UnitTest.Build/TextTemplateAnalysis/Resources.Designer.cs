﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace UnitTest.Build.TextTemplateAnalysis {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("UnitTest.Build.TextTemplateAnalysis.Resources", typeof(Resources).Assembly);
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
        ///   Looks up a localized string similar to &lt;#@ template debug=&quot;false&quot; hostspecific=&quot;false&quot; language=&quot;C#&quot; #&gt;
        ///&lt;#@ assembly name=&quot;System.Core&quot; #&gt;
        ///&lt;#@ import namespace=&quot;System.Linq&quot; #&gt;
        ///&lt;#@ import namespace=&quot;System.Text&quot; #&gt;
        ///&lt;#@ import namespace=&quot;System.Collections.Generic&quot; #&gt;
        ///&lt;#@ output extension=&quot;.cs&quot; #&gt;
        ///
        ///&lt;#@ assembly name=&quot;$(ProjectDir)\..\..\Dependencies\Aderant.Query.Modeling.dll&quot; #&gt;
        ///&lt;#@ assembly name=&quot;$(ProjectDir)\..\..\Dependencies\Aderant.Framework.SoftwareFactory.Common.dll&quot; #&gt;
        ///&lt;#@ assembly name=&quot;$(ProjectDir)\..\..\Dependencies\Microso [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string SimpleTextTemplate {
            get {
                return ResourceManager.GetString("SimpleTextTemplate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;#@ DomainModelDsl processor=&quot;DomainModelDslDirectiveProcessor&quot; requires=&quot;fileName=&apos;..\..\..\Dependencies\Collections.dmdsl&apos;&quot; provides=&quot;DomainPart=RootDomainPart&quot; #&gt;.
        /// </summary>
        internal static string TextTemplateWithCustomProcessor {
            get {
                return ResourceManager.GetString("TextTemplateWithCustomProcessor", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;#@ template debug=&quot;false&quot; hostspecific=&quot;false&quot; language=&quot;C#&quot; #&gt;
        ///&lt;#@ assembly name=&quot;System.Core&quot; #&gt;
        ///&lt;#@ import namespace=&quot;System.Linq&quot; #&gt;
        ///&lt;#@ import namespace=&quot;System.Text&quot; #&gt;
        ///&lt;#@ import namespace=&quot;System.Collections.Generic&quot; #&gt;
        ///&lt;#@ output extension=&quot;.cs&quot; #&gt;
        ///
        ///&lt;#@ include file=&quot;common.ttinclude&quot;#&gt;
        ///&lt;#@ include file=&quot;$(ProjectDir)\common1.ttinclude&quot;#&gt;.
        /// </summary>
        internal static string TextTemplateWithInclude {
            get {
                return ResourceManager.GetString("TextTemplateWithInclude", resourceCulture);
            }
        }
    }
}

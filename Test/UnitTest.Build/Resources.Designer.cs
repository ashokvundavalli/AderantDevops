﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace UnitTest.Build {
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("UnitTest.Build.Resources", typeof(Resources).Assembly);
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
        ///   Looks up a localized string similar to {&quot;serializedVersion&quot;:2,&quot;Artifacts&quot;:{&quot;ModuleA&quot;:[{&quot;Id&quot;:&quot;ModuleA&quot;,&quot;Files&quot;:[{&quot;File&quot;:&quot;Bar.dll&quot;},{&quot;File&quot;:&quot;Baz.dll&quot;},{&quot;File&quot;:&quot;Foo.dll&quot;}]}]},&quot;BucketId&quot;:{&quot;id&quot;:&quot;496d79f90104b02eb2c082d834fd4f40a68d939a&quot;,&quot;tag&quot;:&quot;ModuleA&quot;},&quot;BuildId&quot;:&quot;0&quot;,&quot;Id&quot;:&quot;ff4c861b-1955-492a-ae42-694317375071&quot;,&quot;Outputs&quot;:{&quot;_set&quot;:[{&quot;key&quot;:&quot;ModuleA\\Bar\\Bar.csproj&quot;,&quot;value&quot;:{&quot;Directory&quot;:&quot;ModuleA&quot;,&quot;FilesWritten&quot;:[&quot;bin\\Debug\\Bar.dll&quot;,&quot;bin\\Debug\\Bar.pdb&quot;],&quot;Origin&quot;:&quot;ThisBuild&quot;,&quot;OutputPath&quot;:&quot;bin\\Debug\\&quot;}},{&quot;key&quot;:&quot;ModuleA\\Baz\\Baz.csproj&quot;,&quot;value&quot;:{&quot;Dire [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string buildstate {
            get {
                return ResourceManager.GetString("buildstate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project ToolsVersion=&quot;12.0&quot; DefaultTargets=&quot;Build&quot; xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;&gt;
        ///  &lt;Import Project=&quot;$(MSBuildToolsPath)\Microsoft.CSharp.targets&quot; /&gt;
        ///&lt;/Project&gt;.
        /// </summary>
        internal static string CSharpProject {
            get {
                return ResourceManager.GetString("CSharpProject", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project ToolsVersion=&quot;15.0&quot; DefaultTargets=&quot;Build&quot; xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;&gt;
        ///  &lt;PropertyGroup&gt;
        ///    &lt;CommonBuildProject&gt;$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), &apos;dir.proj&apos;))&lt;/CommonBuildProject&gt;
        ///  &lt;/PropertyGroup&gt;
        ///  &lt;Import Project=&quot;$(MSBuildToolsPath)\Microsoft.CSharp.targets&quot; /&gt;
        ///  &lt;Import Project=&quot;$(CommonBuildProject)\dir.proj&quot; Condition=&quot;$(CommonBuildProject) != &apos;&apos;&quot; /&gt;
        ///&lt;/Project&gt;.
        /// </summary>
        internal static string CSharpProjectWithCommonBuildProjectExpectedResult {
            get {
                return ResourceManager.GetString("CSharpProjectWithCommonBuildProjectExpectedResult", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project ToolsVersion=&quot;15.0&quot; DefaultTargets=&quot;Build&quot; xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;&gt;
        ///  &lt;Import Project=&quot;$(MSBuildToolsPath)\Microsoft.CSharp.targets&quot; /&gt;
        ///  &lt;Import Project=&quot;$(CommonBuildProject)\dir.proj&quot; Condition=&quot;$(CommonBuildProject) != &apos;&apos;&quot; /&gt;
        ///&lt;/Project&gt;.
        /// </summary>
        internal static string CSharpProjectWithCommonBuildProjectImport {
            get {
                return ResourceManager.GetString("CSharpProjectWithCommonBuildProjectImport", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project ToolsVersion=&quot;15.0&quot; DefaultTargets=&quot;Build&quot; xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;&gt;
        ///  &lt;PropertyGroup&gt;
        ///    &lt;CommonBuildProject&gt;$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), &apos;dir.proj&apos;))&lt;/CommonBuildProject&gt;
        ///  &lt;/PropertyGroup&gt;
        ///  &lt;Import Project=&quot;$(MSBuildToolsPath)\Microsoft.CSharp.targets&quot; /&gt;
        ///&lt;/Project&gt;.
        /// </summary>
        internal static string CSharpProjectWithCommonBuildProjectProperty {
            get {
                return ResourceManager.GetString("CSharpProjectWithCommonBuildProjectProperty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project ToolsVersion=&quot;12.0&quot; DefaultTargets=&quot;Build&quot; xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;&gt;
        ///  &lt;Import Project=&quot;$(MSBuildToolsPath)\Microsoft.CSharp.targets&quot; /&gt;
        ///  &lt;PropertyGroup Condition=&quot; &apos;$(Configuration)|$(Platform)&apos; == &apos;Debug|AnyCPU&apos; &quot;&gt;
        ///    &lt;PlatformTarget&gt;AnyCPU&lt;/PlatformTarget&gt;
        ///    &lt;DebugSymbols&gt;true&lt;/DebugSymbols&gt;
        ///    &lt;DebugType&gt;full&lt;/DebugType&gt;
        ///    &lt;Optimize&gt;false&lt;/Optimize&gt;
        ///    &lt;OutputPath&gt;..\..\Bin\TestApp\&lt;/OutputPath&gt;
        ///    &lt;Defi [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string DifferentOutputPathsProject {
            get {
                return ResourceManager.GetString("DifferentOutputPathsProject", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;ProductManifest Name=&quot;Expert&quot; ExpertVersion=&quot;802&quot;&gt;
        ///  &lt;Modules&gt;
        ///    &lt;Module Name=&quot;Applications.Administration&quot; AssemblyVersion=&quot;1.8.0.0&quot; /&gt;
        ///    &lt;Module Name=&quot;Applications.CCLogViewer&quot; AssemblyVersion=&quot;1.8.0.0&quot; GetAction=&quot;branch&quot; Path=&quot;Main&quot; /&gt;
        ///    &lt;Module Name=&quot;Applications.Customization&quot; AssemblyVersion=&quot;1.8.0.0&quot; /&gt;
        ///    &lt;Module Name=&quot;Applications.Deployment&quot; AssemblyVersion=&quot;1.8.0.0&quot; /&gt;
        ///    &lt;Module Name=&quot;Applications.DesignStudio&quot; AssemblyVersion=&quot;1.8.0.0&quot; /&gt;
        /// [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ExpertManifest {
            get {
                return ResourceManager.GetString("ExpertManifest", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project ToolsVersion=&quot;12.0&quot; DefaultTargets=&quot;Build&quot; xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;&gt;
        ///  &lt;Import Project=&quot;$(MSBuildToolsPath)\Microsoft.CSharp.targets&quot; /&gt;
        ///  &lt;PropertyGroup Condition=&quot; &apos;$(Configuration)|$(Platform)&apos; == &apos;Debug|AnyCPU&apos; &quot;&gt;
        ///    &lt;PlatformTarget&gt;AnyCPU&lt;/PlatformTarget&gt;
        ///    &lt;DebugSymbols&gt;true&lt;/DebugSymbols&gt;
        ///    &lt;DebugType&gt;full&lt;/DebugType&gt;
        ///    &lt;Optimize&gt;false&lt;/Optimize&gt;
        ///    &lt;OutputPath&gt;..\..\Bin\Module\&lt;/OutputPath&gt;
        ///    &lt;Defin [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string MatchingOutputPathsProject {
            get {
                return ResourceManager.GetString("MatchingOutputPathsProject", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to REFERENCES: STRICT
        ///NUGET
        ///  remote: https://expertpackages.azurewebsites.net/v3/index.json
        ///    Aderant.Build.Analyzer (2.1.1)
        ///    
        ///GROUP Development
        ///REFERENCES: STRICT
        ///NUGET
        ///  remote: https://expertpackages.azurewebsites.net/v3/index.json
        ///    Aderant.Build.Analyzer (3.1.1).
        /// </summary>
        internal static string PaketLock {
            get {
                return ResourceManager.GetString("PaketLock", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project DefaultTargets=&quot;ModuleBuild&quot;
        ///         xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;
        ///         ToolsVersion=&quot;12.0&quot;&gt;
        ///
        ///  &lt;Import Project=&quot;$(MSBuildExtensionsPath)\Microsoft\VisualStudio\TeamBuild\Microsoft.TeamFoundation.Build.targets&quot; /&gt;
        ///  &lt;Import Project=&quot;$(MSBuildExtensionsPath)\MSBuildCommunityTasks\MSBuild.Community.Tasks.Targets&quot;/&gt;
        ///  &lt;!--Server build for all modules--&gt;
        ///  &lt;Import Condition=&quot;(&apos;$(BuildAll)&apos;==&apos;true&apos;) And (&apos;$(IsDesktopBuild) [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ProjectFileText1 {
            get {
                return ResourceManager.GetString("ProjectFileText1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project ToolsVersion=&quot;15.0&quot; DefaultTargets=&quot;Build&quot; xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;&gt;
        ///&lt;/Project&gt;.
        /// </summary>
        internal static string ProjectWithNoCSharpImport {
            get {
                return ResourceManager.GetString("ProjectWithNoCSharpImport", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;ProductManifest Name=&quot;Expert&quot; ExpertVersion=&quot;8.2.0&quot;&gt;
        ///  &lt;Modules&gt;
        ///    &lt;Module ExcludeFromPackaging=&quot;false&quot; Name=&quot;Aderant.Deployment.Core&quot; GetAction=&quot;NuGet&quot; Version=&quot;&amp;gt;= 12.0.0 build&quot; /&gt;
        ///    &lt;Module ExcludeFromPackaging=&quot;false&quot; Name=&quot;Aderant.Libraries.Models&quot; GetAction=&quot;NuGet&quot; Version=&quot;13.0.0-build4978&quot; /&gt;
        ///  &lt;/Modules&gt;
        ///&lt;/ProductManifest&gt;.
        /// </summary>
        internal static string ReferenceExpertManifest {
            get {
                return ResourceManager.GetString("ReferenceExpertManifest", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;Project ToolsVersion=&quot;12.0&quot; DefaultTargets=&quot;Build&quot; xmlns=&quot;http://schemas.microsoft.com/developer/msbuild/2003&quot;&gt;
        ///  &lt;ItemGroup&gt;
        ///    &lt;Reference Include=&quot;Aderant.FooBar&quot;&gt;
        ///      &lt;HintPath&gt;..\..\Dependencies\Aderant.FooBar.dll&lt;/HintPath&gt;
        ///      &lt;Private&gt;False&lt;/Private&gt;
        ///    &lt;/Reference&gt;
        ///    &lt;Reference Include=&quot;System.Data.DataSetExtensions&quot; /&gt;
        ///    &lt;Reference Include=&quot;System.Web.DynamicData&quot; /&gt;
        ///    &lt;Reference Include=&quot;System.Web.Entity&quot; /&gt;
        ///    &lt;Reference Include=&quot;Sys [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ReplaceReferencesProject {
            get {
                return ResourceManager.GetString("ReplaceReferencesProject", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SRCSRV: ini ------------------------------------------------
        ///VERSION=3
        ///INDEXVERSION=2
        ///VERCTRL=Team Foundation Server
        ///DATETIME=Wed Apr 22 01:16:10 2015
        ///INDEXER=MSCT
        ///SRCSRV: variables ------------------------------------------
        ///TFS_EXTRACT_CMD=tf.exe view /version:%var4% /noprompt &quot;$%var3%&quot; /server:%fnvar%(%var2%) /console &gt;%srcsrvtrg%
        ///TFS_EXTRACT_TARGET=C:\Temp\%var2%%fnbksl%(%var3%)\%var4%\%fnfile%(%var5%)
        ///SRCSRVVERCTRL=tfs
        ///SRCSRVERRDESC=access
        ///SRCSRVERRVAR=var2
        ///VSTFSSERVER=http://tfs:8080/tfs/ad [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string SrcSrvStream1 {
            get {
                return ResourceManager.GetString("SrcSrvStream1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;package xmlns=&quot;http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd&quot;&gt;
        ///  &lt;metadata&gt;
        ///    &lt;id&gt;$id$&lt;/id&gt;
        ///    &lt;description&gt;Default package for $id$&lt;/description&gt;
        ///    &lt;version&gt;$version$&lt;/version&gt;
        ///    &lt;authors&gt;Aderant&lt;/authors&gt;
        ///    &lt;owners&gt;Aderant&lt;/owners&gt;    
        ///    &lt;requireLicenseAcceptance&gt;false&lt;/requireLicenseAcceptance&gt;
        ///  &lt;/metadata&gt;
        ///  &lt;files&gt;
        ///    &lt;file src=&quot;bin\**&quot; exclude=&quot;**\*paket*&quot; target=&quot;lib&quot; /&gt;
        ///  &lt;/files&gt;
        ///&lt;/package&gt;.
        /// </summary>
        internal static string TemplateNuspec {
            get {
                return ResourceManager.GetString("TemplateNuspec", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to type file
        ///id Aderant.Deployment.Core
        ///authors Aderant
        ///description
        ///    Provides libraries and services for deploying an Expert environment.
        ///files    
        ///    Bin/Module/*.config ==&gt; lib 
        ///    Bin/Module/Aderant.* ==&gt; lib 
        ///    Bin/Module/PrerequisitesPowerShell/* ==&gt; lib/PrerequisitesPowerShell
        ///    Bin/Module/PrerequisitesPowerShell ==&gt; lib/PrerequisitesPowerShell
        ///    Bin/Module/Monitoring ==&gt; lib/Monitoring
        ///    Bin/Module/InstallerManifests ==&gt; lib/InstallerManifests
        ///    !Bin/Module/*.exe.config
        ///
        ///dep [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string test_paket_template_with_dependencies {
            get {
                return ResourceManager.GetString("test_paket_template_with_dependencies", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to type file
        ///id Aderant.Deployment.Core
        ///authors Aderant
        ///dependencies
        ///    Foo
        ///excludeddependencies
        ///    Bar.
        /// </summary>
        internal static string test_paket_template_with_exclude_section {
            get {
                return ResourceManager.GetString("test_paket_template_with_exclude_section", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to type file
        ///id Aderant.Framework.Core
        ///authors Aderant
        ///description
        ///    Core Framework libraries.
        ///files
        ///	Bin/Module/Aderant.Framework.* ==&gt; lib
        ///	Bin/Module/Aderant.Expressions.* ==&gt; lib
        ///	Bin/Module/Aderant.Deployment.Client.* ==&gt; lib
        ///	Bin/Module/Aderant.Framework.Communication.Client.* ==&gt; lib
        ///	Bin/Module/Aderant.Framework.Communication.Server.* ==&gt; lib
        ///	Bin/Module/Aderant.Framework.Configuration.* ==&gt; lib
        ///	Bin/Module/Aderant.Framework.Configuration.Service.* ==&gt; lib
        ///	Bin/Module/Aderant.Framework.I [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string test_paket_template_with_mixed_whitespace {
            get {
                return ResourceManager.GetString("test_paket_template_with_mixed_whitespace", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to type file
        ///id Aderant.Deployment.Core
        ///authors Aderant
        ///description
        ///    Provides libraries and services for deploying an Expert environment.
        ///files    
        ///    Bin/Module/*.config ==&gt; lib 
        ///    Bin/Module/Aderant.* ==&gt; lib 
        ///    Bin/Module/PrerequisitesPowerShell/* ==&gt; lib/PrerequisitesPowerShell
        ///    Bin/Module/PrerequisitesPowerShell ==&gt; lib/PrerequisitesPowerShell
        ///    Bin/Module/Monitoring ==&gt; lib/Monitoring
        ///    Bin/Module/InstallerManifests ==&gt; lib/InstallerManifests
        ///    !Bin/Module/*.exe.config.
        /// </summary>
        internal static string test_paket_template_without_dependencies {
            get {
                return ResourceManager.GetString("test_paket_template_without_dependencies", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to type file
        ///id Aderant.Deployment.Core
        ///authors Aderant
        ///description
        ///    Provides libraries and services for deploying an Expert environment.
        ///files    
        ///    Bin/Module/*.config ==&gt; lib 
        ///    Bin/Module/Aderant.* ==&gt; lib 
        ///    Bin/Module/PrerequisitesPowerShell/* ==&gt; lib/PrerequisitesPowerShell
        ///    Bin/Module/PrerequisitesPowerShell ==&gt; lib/PrerequisitesPowerShell
        ///    Bin/Module/Monitoring ==&gt; lib/Monitoring
        ///    Bin/Module/InstallerManifests ==&gt; lib/InstallerManifests
        ///    !Bin/Module/*.exe.config.
        /// </summary>
        internal static string test_paket_template_without_dependencies_UNIX {
            get {
                return ResourceManager.GetString("test_paket_template_without_dependencies_UNIX", resourceCulture);
            }
        }
    }
}

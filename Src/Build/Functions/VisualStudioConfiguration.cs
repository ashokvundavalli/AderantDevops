// Powershell -ExecutionPolicy Unrestricted -Command "Add-Type -Path Find-VisualStudio.cs"

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace VisualStudioConfiguration {
    [Flags]
    public enum InstanceState : uint {
        None = 0,
        Local = 1,
        Registered = 2,
        NoRebootRequired = 4,
        NoErrors = 8,
        Complete = 4294967295,
    }

    [Guid("6380BCFF-41D3-4B2E-8B2E-BF8A6810C848")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IEnumSetupInstances {
        void Next([MarshalAs(UnmanagedType.U4), In] int celt,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface), Out]
            ISetupInstance[] rgelt,
            [MarshalAs(UnmanagedType.U4)] out int pceltFetched);

        void Skip([MarshalAs(UnmanagedType.U4), In] int celt);

        void Reset();

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumSetupInstances Clone();
    }

    [Guid("42843719-DB4C-46C2-8E7C-64F1816EFD5B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ISetupConfiguration {
    }

    [Guid("26AAB78C-4A60-49D6-AF3B-3C35BC93365D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ISetupConfiguration2 : ISetupConfiguration {
        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumSetupInstances EnumInstances();

        [return: MarshalAs(UnmanagedType.Interface)]
        ISetupInstance GetInstanceForCurrentProcess();

        [return: MarshalAs(UnmanagedType.Interface)]
        ISetupInstance GetInstanceForPath([MarshalAs(UnmanagedType.LPWStr), In] string path);

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumSetupInstances EnumAllInstances();
    }

    [Guid("B41463C3-8866-43B5-BC33-2B0676F7F42E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ISetupInstance {
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetInstallationVersion();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDisplayName([In] [MarshalAs(UnmanagedType.U4)] int lcid = 0);

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetInstallationPath();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDescription([In] [MarshalAs(UnmanagedType.U4)] int lcid = 0);
    }

    [Guid("89143C9A-05AF-49B0-B717-72E218A2185C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ISetupInstance2 : ISetupInstance {
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetInstanceId();

        [return: MarshalAs(UnmanagedType.Struct)]
        System.Runtime.InteropServices.ComTypes.FILETIME GetInstallDate();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetInstallationName();

        [return: MarshalAs(UnmanagedType.BStr)]
        new string GetInstallationPath();

        [return: MarshalAs(UnmanagedType.BStr)]
        new string GetInstallationVersion();

        [return: MarshalAs(UnmanagedType.BStr)]
        new string GetDisplayName([In] [MarshalAs(UnmanagedType.U4)] int lcid = 0);

        [return: MarshalAs(UnmanagedType.BStr)]
        new string GetDescription([In] [MarshalAs(UnmanagedType.U4)] int lcid = 0);

        [return: MarshalAs(UnmanagedType.BStr)]
        string ResolvePath([MarshalAs(UnmanagedType.LPWStr), In] string pwszRelativePath);

        [return: MarshalAs(UnmanagedType.U4)]
        InstanceState GetState();

        [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_UNKNOWN)]
        ISetupPackageReference[] GetPackages();

        ISetupPackageReference GetProduct();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetProductPath();

        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool IsLaunchable();

        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool IsComplete();

        [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_UNKNOWN)]
        ISetupPropertyStore GetProperties();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetEnginePath();
    }

    [Guid("DA8D8A16-B2B6-4487-A2F1-594CCCCD6BF5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ISetupPackageReference {
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetId();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetVersion();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetChip();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetLanguage();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetBranch();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetType();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetUniqueId();

        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool GetIsExtension();
    }

    [Guid("c601c175-a3be-44bc-91f6-4568d230fc83")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ISetupPropertyStore {
        [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)]
        string[] GetNames();

        object GetValue([MarshalAs(UnmanagedType.LPWStr), In] string pwszName);
    }

    [Guid("42843719-DB4C-46C2-8E7C-64F1816EFD5B")]
    [CoClass(typeof(SetupConfigurationClass))]
    [ComImport]
    public interface SetupConfiguration : ISetupConfiguration2, ISetupConfiguration {
    }

    [Guid("177F0C4A-1CD3-4DE7-A32C-71DBBB9FA36D")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComImport]
    public class SetupConfigurationClass {
    }


    /// <summary>
    /// Helper class to wrap the Microsoft.VisualStudio.Setup.Configuration.Interop API to query
    /// Visual Studio setup for instances installed on the machine.
    /// Code derived from sample: https://code.msdn.microsoft.com/Visual-Studio-Setup-0cedd331
    /// </summary>
    public class VisualStudioLocationHelper {
        private const int REGDB_E_CLASSNOTREG = unchecked((int) 0x80040154);

        /// <summary>
        /// Query the Visual Studio setup API to get instances of Visual Studio installed
        /// on the machine. Will not include anything before Visual Studio "15".
        /// </summary>
        /// <returns>Enumerable list of Visual Studio instances</returns>
        public static IList<VisualStudioInstance> GetInstances() {
            IList<VisualStudioInstance> instance;
            return GetInstances(out instance);
        }

        /// <summary>
        /// Query the Visual Studio setup API to get instances of Visual Studio installed
        /// on the machine. Will not include anything before Visual Studio "15".
        /// </summary>
        /// <returns>Enumerable list of Visual Studio instances</returns>
        public static IList<VisualStudioInstance> GetInstances(out IList<VisualStudioInstance> invalidInstances) {
            var validInstances = new List<VisualStudioInstance>();
            invalidInstances = new List<VisualStudioInstance>();

            try {
                // This code is not obvious. See the sample (link above) for reference.
                var query = (ISetupConfiguration2) GetQuery();
                var e = query.EnumAllInstances();

                int fetched;
                var instances = new ISetupInstance[1];
                do {
                    // Call e.Next to query for the next instance (single item or nothing returned).
                    e.Next(1, instances, out fetched);
                    if (fetched <= 0) continue;

                    var instance = instances[0] as ISetupInstance2;
                    if (instance != null) {
                        var state = instance.GetState();
                        Version version;

                        try {
                            version = new Version(instance.GetInstallationVersion());
                        } catch (FormatException) {
                            continue;
                        }

                        var studioInstance = new VisualStudioInstance(
                            instance.GetDisplayName(),
                            instance.GetInstallationPath(),
                            version);


                        // InstanceState.Complete may not work see
                        // https://github.com/dotnet/msbuild/commit/68b96728bc2b7c554a37d12723e68e5d37768743#commitcomment-20959231 and
                        // https://github.com/dotnet/msbuild/issues/1939
                        if (state == InstanceState.Complete || state.HasFlag(InstanceState.Local)) {
                            validInstances.Add(studioInstance);
                        } else {
                            invalidInstances.Add(studioInstance);
                        }
                    }
                } while (fetched > 0);
            } catch (COMException) {
            } catch (DllNotFoundException) {
                // This is OK, VS "15" or greater likely not installed.
            }

            return validInstances;
        }


        private static ISetupConfiguration GetQuery() {
            try {
                // Try to CoCreate the class object.
                return new SetupConfiguration();
            } catch (COMException ex) {
                if (ex.ErrorCode == REGDB_E_CLASSNOTREG) {
                    // Try to get the class object using app-local call.
                    ISetupConfiguration query;
                    var result = GetSetupConfiguration(out query, IntPtr.Zero);

                    if (result < 0) {
                        throw new COMException("Failed to get query", result);
                    }

                    return query;
                }

                throw;
            }
        }

        [DllImport("Microsoft.VisualStudio.Setup.Configuration.Native.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int GetSetupConfiguration(
            [MarshalAs(UnmanagedType.Interface), Out]
            out ISetupConfiguration configuration,
            IntPtr reserved);
    }

    /// <summary>
    /// Wrapper class to represent an installed instance of Visual Studio.
    /// </summary>
    public sealed class VisualStudioInstance {
        /// <summary>
        /// Version of the Visual Studio Instance
        /// </summary>
        public Version Version { get; private set; }

        /// <summary>
        /// Path to the Visual Studio installation
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Full name of the Visual Studio instance with SKU name
        /// </summary>
        public string Name { get; private set; }

        internal VisualStudioInstance(string name, string path, Version version) {
            Name = name;
            Path = path;
            Version = version;
        }
    }
}
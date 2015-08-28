using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UITester.Annotations;

namespace UITester {
    public class ParameterController : INotifyPropertyChanged {
        public ParameterController() {
            properties = new ConcurrentDictionary<string, string>();
        }

        public static void Parse(string[] args) {
            Dictionary<string, string> mappings = new Dictionary<string, string> {
                { "r", "RemoteMachineName" },
                { "remotemachinename", "RemoteMachineName" },
                { "e", "EnvironmentManifestPath" },
                { "environmentmanifestpath", "EnvironmentManifestPath" },
                { "m", "BranchModulesDirectory" },
                { "branchmodulesdirectory", "BranchModulesDirectory" },
                { "b", "BuildScriptsDirectory" },
                { "buildscriptsdirectory", "BuildScriptsDirectory" },
                { "p", "PackageScriptsDirectory" },
                { "packagescriptsdirectory", "PackageScriptsDirectory" },
                { "s", "BranchServerDirectory" },
                { "branchserverdirectory", "BranchServerDirectory" },
            };
            for (int i = 0; i < args.Length; i++) {
                if (args[i].StartsWith("/") || args[i].StartsWith("-")) {
                    string argIdentifier = args[i].Substring(1).ToLower();
                    if (mappings.ContainsKey(argIdentifier) && args.Length > i + 1) {
                        if (Singleton.properties.ContainsKey(mappings[argIdentifier])) {
                            //Warn that the same argument has been added twice.
                        } else {
                            Singleton.properties.GetOrAdd(mappings[argIdentifier], args[i + 1]);
                            i++;
                        }
                    } else {
                        throw new ArgumentException("Do not understand argument or value was not given.");
                    }
                } else if (args[i].StartsWith("?")) {
                    Console.WriteLine("[R]emoteMachineName, [E]nvironmentManifestPath, Branch[M]odulesDirectory, [B]uildScriptsDirectory, [P]ackageScriptsDirectory, Branch[S]erverDirectory");
                }
                //else: argument was not paired with an identifier.
            }
        }
        [NotNull]
        private readonly ConcurrentDictionary<string, string> properties;

        public void Update(string key, string value) {
            if (key == null) {
                throw new ArgumentNullException("key");
            }
            string[] knownProperties = { "PackageScriptsDirectory", "BuildScriptsDirectory", "BranchModulesDirectory", "EnvironmentManifestPath", "RemoteMachineName" };
            properties[key] = value;
            if (knownProperties.Contains(key)) {
                OnPropertyChanged(key);
            }
        }

        public string RemoteMachineName {
            get {
                string outValue;
                properties.TryGetValue("RemoteMachineName", out outValue);
                return outValue;
            }
            set { Update("RemoteMachineName", value); }
        }

        public string EnvironmentManifestPath {
            get {
                string outValue;
                properties.TryGetValue("EnvironmentManifestPath", out outValue);
                return outValue;
            }
            set { Update("EnvironmentManifestPath", value); }
        }

        public string BranchModulesDirectory {
            get {
                string outValue;
                properties.TryGetValue("BranchModulesDirectory", out outValue);
                return outValue;
            }
            set { Update("BranchModulesDirectory", value); }
        }

        public string BuildScriptsDirectory {
            get {
                string outValue;
                properties.TryGetValue("BuildScriptsDirectory", out outValue);
                return outValue;
            }
            set { Update("BuildScriptsDirectory", value); }
        }

        public string PackageScriptsDirectory {
            get {
                string outValue;
                properties.TryGetValue("PackageScriptsDirectory", out outValue);
                return outValue;
            }
            set { Update("PackageScriptsDirectory", value); }
        }

        public string BranchServerDirectory {
            get {
                string outValue;
                properties.TryGetValue("BranchServerDirectory", out outValue);
                return outValue;
            }
            set { Update("BranchServerDirectory", value); }
        }

        private static ParameterController singleton;
        [NotNull]
        public static ParameterController Singleton {
            get { return singleton ?? (singleton = new ParameterController()); }
        }
       
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var handler = PropertyChanged;
            if (handler != null) {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

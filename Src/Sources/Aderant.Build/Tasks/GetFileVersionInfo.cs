using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class GetFileVersionInfo : Task {
        [Required]
        public ITaskItem[] Files { get; set; }

        [Output]
        public ITaskItem[] FilesWithInfo { get; set; }

        public override bool Execute() {
            List<ITaskItem> arrayList = new List<ITaskItem>();

            foreach (var file in Files) {
                string fullPath = file.GetMetadata("FullPath");

                if (fullPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) {
                    string productName = GetProductName(fullPath);

                    TaskItem taskItem = CreateTaskItemFromInstallerInfo(file, productName);
                    arrayList.Add(taskItem);
                } else {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(fullPath);

                    TaskItem taskItem = CreateTaskItemFromVersionInfo(file, versionInfo);
                    arrayList.Add(taskItem);
                }
            }

            FilesWithInfo = arrayList.ToArray();

            return !Log.HasLoggedErrors;
        }

        private string GetProductName(string fullPath) {
            dynamic view = null;
            dynamic database = null;
            try {
                Type installerType = Type.GetTypeFromProgID("WindowsInstaller.Installer");

                dynamic installer = Activator.CreateInstance(installerType);

                database = installer.OpenDatabase(fullPath, 0 /*open database mode read only*/);

                view = database.OpenView("SELECT `Value` FROM `Property` WHERE `Property` = 'ProductName'");

                view.Execute(null);

                var record = view.Fetch();

                return record.StringData(1).ToString();
            } finally {
                if (view != null) {
                    view.Close();
                    Marshal.FinalReleaseComObject(view);
                }
                if (database != null) {
                    Marshal.FinalReleaseComObject(database);
                }
            }
        }

        public static TaskItem CreateTaskItemFromVersionInfo(ITaskItem file, FileVersionInfo versionInfo) {
            var taskItem = new TaskItem(file);
            taskItem.SetMetadata("ProductName", versionInfo.ProductName /*AssemblyProductAttribute*/);
            taskItem.SetMetadata("FileDescription", versionInfo.FileDescription /*AssemblyTitleAttribute*/);
            return taskItem;
        }

        public static TaskItem CreateTaskItemFromInstallerInfo(ITaskItem file, string productName) {
            var taskItem = new TaskItem(file);
            taskItem.SetMetadata("ProductName", productName);
            taskItem.SetMetadata("FileDescription", string.Empty);
            return taskItem;
        }
    }
}
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class CheckFreeSpace : Task {
        [Required]
        public int FreeSpace { get; set; }

        [Required]
        public string Units { get; set; }

        public override bool Execute() {
            if (string.Equals("GB", Units, StringComparison.OrdinalIgnoreCase)) {
                long freeSizeInBytes = (long)FreeSpace * 1024 * 1024 * 1024;

                DriveInfo[] allDrives = DriveInfo.GetDrives();

                foreach (DriveInfo d in allDrives) {
                    if (d.IsReady) {
                        if (d.DriveType == DriveType.Fixed) {
                            if (d.AvailableFreeSpace < freeSizeInBytes) {
                                Log.LogError($"Available free space on drive {d.Name} is below minimum requirement of {freeSizeInBytes / 1024 / 1024 / 1024} GB for a reliable build.");
                                break;
                            }
                        }
                    }
                }

                return !Log.HasLoggedErrors;
            }

            throw new ArgumentOutOfRangeException("Only GB is supported as the unit", Units);
        }
    }
}
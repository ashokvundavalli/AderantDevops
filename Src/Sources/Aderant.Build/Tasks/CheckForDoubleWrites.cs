﻿using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Fails if any double writes are detected.
    /// Confirms that two or more files do not write to the same destination. This ensures deterministic behavior.
    /// </summary>
    public class CheckForDoubleWrites : Task {

        [Required]
        public string[] FileList { get; set; }

        public override bool Execute() {
            try {
                DoubleWriteCheck.CheckForDoubleWrites(FileList);
            } catch (Exception exception) {
                Log.LogErrorFromException(exception);
            }

            return !Log.HasLoggedErrors;
        }
    }
}

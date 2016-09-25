﻿using System;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Packaging {
    internal class RootItemHandler {
        private readonly IFileSystem2 fs;

        public RootItemHandler(IFileSystem2 fs) {
            this.fs = fs;
        }

        public ExpertModule Module { get; set; }

        public void MoveContent(ProductAssemblyContext context, string contentDirectory) {
            if (string.IsNullOrEmpty(Module.Target)) {
                throw new InvalidOperationException("Cannot perform operation as the target property is not set on: " + Module.Name);
            }

            string relativeDirectory = context.ResolvePackageRelativeDirectory(Module);

            fs.MoveDirectory(contentDirectory, relativeDirectory);
        }
    }
}
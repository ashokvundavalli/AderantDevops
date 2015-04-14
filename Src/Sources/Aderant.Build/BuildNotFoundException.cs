using System;

namespace Aderant.Build {

    internal class BuildNotFoundException : Exception {

        public BuildNotFoundException(string message) : base(message) {
         
        }
    }
}
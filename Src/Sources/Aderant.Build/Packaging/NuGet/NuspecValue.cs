namespace Aderant.Build.Packaging.NuGet {
    internal abstract class NuspecValue<T> {
        private T value;

        /// <summary>
        /// Gets a value indicating whether this instance represents a single replacement token (variable).
        /// </summary>  
        public abstract bool IsVariable { get; }

        /// <summary>
        /// Gets a value indicating whether this instance has replacement tokens.
        /// </summary>
        public abstract bool HasReplacementTokens { get; }

        public T Value {
            get { return value; }
            set { this.value = value; }
        }
    }
}
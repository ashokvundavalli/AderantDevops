namespace Aderant.Build.Model {
    public interface IDependable {

        /// <summary>
        /// Gets a unique identifier for this dependency within the build.
        /// </summary>
        string Id { get; }
    }
}

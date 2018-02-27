namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild project element.
    /// </summary>
    public abstract class MSBuildProjectElement {
        public abstract void Accept(BuildElementVisitor visitor);
    }
}
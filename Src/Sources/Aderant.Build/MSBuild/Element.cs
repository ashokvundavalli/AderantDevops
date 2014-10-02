namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild project element.
    /// </summary>
    public abstract class Element {

        public abstract void Accept(BuildElementVisitor visitor);
    }
}
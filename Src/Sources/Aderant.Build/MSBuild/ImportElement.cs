namespace Aderant.Build.MSBuild {
    public class ImportElement : MSBuildProjectElement {

        public string Project { get; set; }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }

    }

    public class ExecElement : MSBuildProjectElement {

        public string Command { get; set; }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }

    }
}
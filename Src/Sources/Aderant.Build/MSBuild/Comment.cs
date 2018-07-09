namespace Aderant.Build.MSBuild {
    public class Comment : MSBuildProjectElement {
        public Comment(string text) {
            Text = text;
        }

        public string Text { get; set; }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }
}

namespace Aderant.Build.MSBuild {
    public abstract class BuildElementVisitor {

        public virtual void Visit(Target element) {
        }

        public virtual void Visit(ItemGroup element) {
        }

        public virtual void Visit(Project element) {
        }

        public virtual void Visit(CallTarget element) {
        }

        public virtual void Visit(Message element) {
        }

        public virtual void Visit(MSBuildTask element) {
        }

        public virtual void Visit(PropertyGroup element) {
        }

        public virtual void Visit(Comment element) {
        }

        public virtual void Visit(ImportElement element) {
        }

        public virtual void Visit(ExecElement element) {
        }
    }
}
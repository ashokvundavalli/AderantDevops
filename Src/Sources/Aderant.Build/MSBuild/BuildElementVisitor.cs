using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.MSBuild {
    public abstract class BuildElementVisitor {
        public virtual void Visit(BuildStep buildStep) {
        }

        public virtual void Visit(Target buildStep) {
        }

        public virtual void Visit(ItemGroup buildStep) {
        }

        public virtual void Visit(Project buildStep) {
        }

        public virtual void Visit(CallTarget buildStep) {
        }

        public virtual void Visit(Message buildStep) {
        }

        public virtual void Visit(MSBuildTask buildStep) {
        }

        public virtual void Visit(PropertyGroup buildStep) {
        }

        public virtual void Visit(Comment buildStep) {
        }
    }
}

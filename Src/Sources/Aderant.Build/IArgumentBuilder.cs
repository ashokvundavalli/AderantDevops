namespace Aderant.Build {
    public interface IArgumentBuilder {
        string[] GetArguments(string commandLine);
    }
}
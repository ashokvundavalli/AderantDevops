namespace Aderant.Build.PipelineService {
    internal interface IProxyAccessor {
        IBuildPipelineServiceContract GetProxy(string id);
    }
}

namespace Aderant.Build.Tasks {
    public sealed class WarningRatchetRequest {
        public string TeamProject { get; set; }
        public int BuildId { get; set; }
        public int BuildDefinitionId { get; set; }
        public string BuildDefinitionName { get; set; }
        public bool IsDraft { get; set; }
        public Microsoft.TeamFoundation.Build.WebApi.Build Build { get; internal set; }
        public Microsoft.TeamFoundation.Build.WebApi.Build LastGoodBuild { get; internal set; }
    }
}
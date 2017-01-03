namespace Aderant.WebHooks.Model {
    internal enum ReviewerVote : short {
        Approved = 10,
        ApprovedWithComment = 5,
        None = 0,
        NotReady = -5,
        Rejected = -10
    }
}
namespace AquaPlan.Domain.Enums;

public enum OrderStatus
{
    Draft = 0,
    Assigned = 1,
    InProgress = 2,
    SamplingCompleted = 3,
    Validated = 4,
    SentToLims = 5,
    ResultsReceived = 6,
    Completed = 7,
    Cancelled = 8,
}

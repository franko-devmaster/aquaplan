using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

public class MockLimsOrder
{
    public Guid Id { get; set; }
    public Guid LimsOrderId { get; set; }
    public Guid? OrderId { get; set; }
    public string OrderReference { get; set; } = string.Empty;
    public DateTime SamplingDate { get; set; }
    public string ParametersJson { get; set; } = "[]";
    public string? ResultsJson { get; set; }
    public MockLimsOrderStatus Status { get; set; } = MockLimsOrderStatus.Received;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResultsReadyAt { get; set; }
    public Guid TenantId { get; set; }
}

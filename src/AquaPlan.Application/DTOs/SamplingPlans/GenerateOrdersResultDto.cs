namespace AquaPlan.Application.DTOs.SamplingPlans;

public record GenerateOrdersResultDto(
    int OrdersCreated,
    List<GeneratedOrderSummaryDto> Orders);

public record GeneratedOrderSummaryDto(
    Guid OrderId,
    string OrderNumber,
    string SamplingLocationName,
    string AnalysisProfileName,
    DateTime? PlannedDate);

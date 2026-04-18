using AquaPlan.Application.DTOs.AnalysisPrograms;
using AquaPlan.Application.DTOs.AnalysisProfiles;
using AquaPlan.Application.DTOs.Containers;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.DTOs.SamplingLocations;

namespace AquaPlan.Application.DTOs.SamplingRounds;

/// <summary>
/// AQ-373 — aggregated snapshot returned to the préleveur at round checkout.
/// Contains everything needed to run the round fully offline: the round itself,
/// all its orders with samplings, the distributor's active/validated sampling
/// locations (for on-field location replacement), and the deduplicated catalog
/// fragments (programs, profiles, containers) required to display the forms
/// and expected flacons without any additional network call.
/// </summary>
public record OfflineSnapshotDto(
    SamplingRoundDetailDto Round,
    IList<OrderDetailDto> Orders,
    IList<SamplingLocationDto> SamplingLocations,
    IList<AnalysisProgramDto> AnalysisPrograms,
    IList<AnalysisProfileDto> AnalysisProfiles,
    IList<ContainerDto> Containers,
    DateTime SnapshotedAt);

namespace AquaPlan.Application.DTOs.MockLims;

/// <summary>
/// Reference data for a water quality parameter produced by the Mock LIMS generator.
/// Ranges are simplified for simulation. Bacterial parameters use 0 as both min and max (absence standard).
/// </summary>
public record MockLimsParameterReference(
    string Code,
    string Unit,
    decimal ReferenceMin,
    decimal ReferenceMax,
    decimal SampleMin,
    decimal SampleMax);

public static class MockLimsParameterCatalog
{
    public static readonly IReadOnlyList<MockLimsParameterReference> All = new List<MockLimsParameterReference>
    {
        new("PH", "pH", 6.5m, 8.5m, 6.0m, 9.0m),
        new("CONDUCTIVITY", "µS/cm", 200m, 2500m, 150m, 2800m),
        new("TURBIDITY", "NTU", 0m, 1m, 0m, 3m),
        new("FREE_CHLORINE", "mg/L", 0m, 1m, 0m, 1.5m),
        new("TOTAL_CHLORINE", "mg/L", 0m, 1.5m, 0m, 2m),
        new("NITRATES", "mg/L", 0m, 40m, 0m, 60m),
        new("NITRITES", "mg/L", 0m, 0.1m, 0m, 0.5m),
        new("AMMONIUM", "mg/L", 0m, 0.5m, 0m, 1m),
        new("IRON", "mg/L", 0m, 0.2m, 0m, 0.4m),
        new("MANGANESE", "mg/L", 0m, 0.05m, 0m, 0.1m),
        new("COPPER", "mg/L", 0m, 1m, 0m, 1.5m),
        new("E_COLI", "UFC/100mL", 0m, 0m, 0m, 5m),
        new("ENTEROCOCCI", "UFC/100mL", 0m, 0m, 0m, 5m),
        new("TOTAL_COLIFORMS", "UFC/100mL", 0m, 0m, 0m, 10m),
        new("AEROBIC_COUNT_22", "UFC/mL", 0m, 100m, 0m, 200m),
    };

    public static MockLimsParameterReference? FindByCode(string code)
    {
        return All.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

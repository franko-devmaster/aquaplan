using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;

namespace AquaPlan.Infrastructure.Services.MockLims;

internal class MockLimsResultGenerator : IMockLimsResultGenerator
{
    /// <summary>
    /// Probability (0–1) that a single generated value falls outside the reference range.
    /// Used to simulate non-conformities for UI/dashboard testing.
    /// </summary>
    private const double OutOfRangeProbability = 0.10;

    public IReadOnlyList<MockLimsResultDto> Generate(IReadOnlyList<string> parameterCodes, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(parameterCodes);

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var results = new List<MockLimsResultDto>(parameterCodes.Count);

        foreach (var code in parameterCodes)
        {
            var reference = MockLimsParameterCatalog.FindByCode(code)
                ?? throw new ArgumentException($"Unknown Mock LIMS parameter code: {code}", nameof(parameterCodes));

            var outOfRange = random.NextDouble() < OutOfRangeProbability;
            var value = GenerateValue(reference, random, outOfRange);
            var isConform = value >= reference.ReferenceMin && value <= reference.ReferenceMax;

            results.Add(new MockLimsResultDto(
                ParameterCode: reference.Code,
                Value: Math.Round(value, 3),
                Unit: reference.Unit,
                ReferenceMin: reference.ReferenceMin,
                ReferenceMax: reference.ReferenceMax,
                IsConform: isConform));
        }

        return results;
    }

    private static decimal GenerateValue(MockLimsParameterReference reference, Random random, bool outOfRange)
    {
        decimal min;
        decimal max;

        if (outOfRange)
        {
            // Pick one side of the out-of-range window to simulate a non-conformity.
            var rollHigh = random.NextDouble() < 0.5;
            if (rollHigh && reference.SampleMax > reference.ReferenceMax)
            {
                min = reference.ReferenceMax;
                max = reference.SampleMax;
            }
            else if (reference.SampleMin < reference.ReferenceMin)
            {
                min = reference.SampleMin;
                max = reference.ReferenceMin;
            }
            else
            {
                min = reference.ReferenceMin;
                max = reference.ReferenceMax;
            }
        }
        else
        {
            min = reference.ReferenceMin;
            max = reference.ReferenceMax;
        }

        if (max <= min)
        {
            return min;
        }

        var factor = (decimal)random.NextDouble();
        return min + factor * (max - min);
    }
}

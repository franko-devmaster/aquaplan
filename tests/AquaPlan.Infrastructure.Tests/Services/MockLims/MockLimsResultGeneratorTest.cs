using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Infrastructure.Services.MockLims;

namespace AquaPlan.Infrastructure.Tests.Services.MockLims;

public class MockLimsResultGeneratorTest
{
    private readonly MockLimsResultGenerator _sut = new();

    [Fact]
    public void Generate_WithFixedSeed_ShouldBeDeterministic()
    {
        var parameters = new[] { "PH", "NITRATES", "TURBIDITY" };

        var first = _sut.Generate(parameters, seed: 42);
        var second = _sut.Generate(parameters, seed: 42);

        first.Should().HaveCount(3);
        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public void Generate_WithDifferentSeeds_ShouldProduceDifferentValues()
    {
        var parameters = new[] { "PH", "CONDUCTIVITY" };

        var first = _sut.Generate(parameters, seed: 1);
        var second = _sut.Generate(parameters, seed: 2);

        first.Should().NotBeEquivalentTo(second);
    }

    [Fact]
    public void Generate_ShouldReturnOneResultPerParameter()
    {
        var parameters = MockLimsParameterCatalog.All.Select(p => p.Code).ToList();

        var results = _sut.Generate(parameters, seed: 7);

        results.Should().HaveCount(parameters.Count);
        results.Select(r => r.ParameterCode).Should().BeEquivalentTo(parameters);
    }

    [Fact]
    public void Generate_ShouldUseMockLimsParameterCatalogUnit()
    {
        var results = _sut.Generate(new[] { "PH", "CONDUCTIVITY" }, seed: 11);

        results.Single(r => r.ParameterCode == "PH").Unit.Should().Be("pH");
        results.Single(r => r.ParameterCode == "CONDUCTIVITY").Unit.Should().Be("µS/cm");
    }

    [Fact]
    public void Generate_ShouldComputeIsConformBasedOnReferenceRange()
    {
        var results = _sut.Generate(MockLimsParameterCatalog.All.Select(p => p.Code).ToList(), seed: 99);

        foreach (var r in results)
        {
            var inRange = r.Value >= r.ReferenceMin && r.Value <= r.ReferenceMax;
            r.IsConform.Should().Be(inRange);
        }
    }

    [Fact]
    public void Generate_WithUnknownParameterCode_ShouldThrow()
    {
        var act = () => _sut.Generate(new[] { "UNKNOWN_CODE" });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_OverManySamples_ShouldProduceSomeNonConform()
    {
        // With OutOfRangeProbability ~= 10%, over 500 samples of a strict parameter
        // (nitrates have a real reference window) we expect a handful of non-conformities.
        var parameters = Enumerable.Repeat("NITRATES", 500).ToList();

        var results = _sut.Generate(parameters, seed: 2026);

        var nonConform = results.Count(r => !r.IsConform);
        nonConform.Should().BeGreaterThan(10);
        nonConform.Should().BeLessThan(150);
    }
}

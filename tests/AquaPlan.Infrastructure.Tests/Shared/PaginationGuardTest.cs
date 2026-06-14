using AquaPlan.Shared.Pagination;

namespace AquaPlan.Infrastructure.Tests.Shared;

// Polish F-221 — guards against page=0 (negative OFFSET → 500) and unbounded page sizes.
public class PaginationGuardTest
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void Normalize_ShouldClampPageToAtLeastOne(int page, int expectedPage)
    {
        var (resultPage, _) = PaginationGuard.Normalize(page, 20);

        resultPage.Should().Be(expectedPage);
    }

    [Theory]
    [InlineData(0, PaginationGuard.DefaultPageSize)]
    [InlineData(-1, PaginationGuard.DefaultPageSize)]
    [InlineData(50, 50)]
    [InlineData(100000, PaginationGuard.MaxPageSize)]
    public void Normalize_ShouldClampPageSizeToBounds(int pageSize, int expectedPageSize)
    {
        var (_, resultPageSize) = PaginationGuard.Normalize(1, pageSize);

        resultPageSize.Should().Be(expectedPageSize);
    }
}

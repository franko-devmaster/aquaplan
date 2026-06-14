namespace AquaPlan.Shared.Pagination;

/// <summary>
/// Polish F-221 — normalises client-supplied pagination parameters so a paged query can never
/// produce a negative OFFSET (<c>page = 0</c> → 500) or load an unbounded number of rows
/// (<c>pageSize = 100000</c>). All paged services route their parameters through this helper.
/// </summary>
public static class PaginationGuard
{
    /// <summary>Maximum number of rows that may be requested in a single page.</summary>
    public const int MaxPageSize = 200;

    /// <summary>Default page size applied when the caller supplies a non-positive value.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Returns a safe (page, pageSize) pair: page is at least 1, pageSize is clamped to
    /// [1, <see cref="MaxPageSize"/>] (falling back to <see cref="DefaultPageSize"/> when non-positive).
    /// </summary>
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return (safePage, safePageSize);
    }
}

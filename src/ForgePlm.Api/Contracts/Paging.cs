namespace ForgePlm.Api.Contracts;

/// <summary>
/// Paging for list endpoints (spec §7): `page` is 1-based and defaults to 1, `pageSize`
/// defaults to 50 and is capped at 200.
///
/// Out-of-range values are clamped rather than rejected. A read that 400s because a client sent
/// `pageSize=1000` punishes the caller for something the server can answer anyway, and the cap
/// exists to bound the server's work — which clamping achieves just as well as refusing.
/// </summary>
public sealed class PageRequest
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public int EffectivePage => Page < 1 ? 1 : Page;

    public int EffectivePageSize => PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);

    public int Skip => (EffectivePage - 1) * EffectivePageSize;

    /// <summary>Builds a request from nullable query parameters, applying the spec's defaults.</summary>
    public static PageRequest From(int? page, int? pageSize) =>
        new() { Page = page ?? 1, PageSize = pageSize ?? DefaultPageSize };
}

/// <summary>
/// The shape every list endpoint returns (spec §7). `total` is the count matching the filters,
/// ignoring paging — that is what lets a pager render its page count.
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

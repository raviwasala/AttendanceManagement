namespace AttendanceSystem.Common.Models;

/// <summary>
/// One page of results plus the count needed to draw a pager.
///
/// The count is deliberately part of the payload rather than a second endpoint: a pager that
/// cannot say "of 4,312" is barely a pager, and the extra COUNT runs against the same filtered
/// query the page came from, so the two can never disagree.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];

    /// <summary>1-based.</summary>
    public int Page { get; set; } = 1;

    /// <summary>0 means "everything on one page" — the caller opted out of paging.</summary>
    public int PageSize { get; set; }

    /// <summary>Rows matching the filter, before paging.</summary>
    public int TotalCount { get; set; }

    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    /// <summary>Index of the first row on this page, 1-based. 0 when the page is empty.</summary>
    public int FirstRowNumber => Items.Count == 0 ? 0 : (PageSize <= 0 ? 1 : (Page - 1) * PageSize + 1);

    public static PagedResult<T> Empty(int page, int pageSize) =>
        new() { Page = page, PageSize = pageSize, TotalCount = 0 };

    /// <summary>Projects the items to another shape while carrying the paging figures across.</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) => new()
    {
        Items = Items.Select(selector).ToList(),
        Page = Page,
        PageSize = PageSize,
        TotalCount = TotalCount
    };
}

/// <summary>
/// Paging arguments accepted by list endpoints.
///
/// PageSize is clamped rather than rejected: a caller asking for 100,000 rows is a mistake, not
/// an attack, and failing the request teaches them nothing. 0 is honoured as "no paging" so the
/// screens that legitimately want a whole small table can still say so.
/// </summary>
public class PageRequest
{
    public const int MaxPageSize = 500;
    public const int DefaultPageSize = 25;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value <= 0 ? 0 : Math.Min(value, MaxPageSize);
    }

    public int Skip => PageSize <= 0 ? 0 : (Page - 1) * PageSize;
}

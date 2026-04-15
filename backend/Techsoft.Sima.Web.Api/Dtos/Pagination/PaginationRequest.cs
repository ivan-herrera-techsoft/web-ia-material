using Microsoft.AspNetCore.Mvc;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Dtos.Pagination;

public class PaginationRequest
{
    [FromQuery]
    public int? PageNumber { get; set; } = 0;

    private int _pageSize = 10;

    [FromQuery]
    public int? PageSize
    {
        get => _pageSize;
        set
        {
            if (value != null && value >= MIN_PAGE_SIZE && value <= MAX_PAGE_SIZE)
                _pageSize = value.Value;
            else
                _pageSize = MAX_PAGE_SIZE;
        }
    }

    [FromQuery]
    public string? Sort { get; set; }

    [FromQuery]
    public string? Filter { get; set; }

    public static ValueTask<PaginationRequest?> BindAsync(HttpContext context)
    {
        var query = context.Request.Query;
        if (!int.TryParse(query["PageNumber"], out var pageNumber) || pageNumber < 0)
            pageNumber = 0;
        if (!int.TryParse(query["PageSize"], out var pageSize))
            pageSize = 10;
        return ValueTask.FromResult<PaginationRequest?>(new PaginationRequest
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Sort = query["Sort"],
            Filter = query["Filter"]
        });
    }
}

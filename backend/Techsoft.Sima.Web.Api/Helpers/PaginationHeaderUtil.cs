using System.Text.Json;
using Techsoft.Sima.Web.Api.Dtos.Pagination;

namespace Techsoft.Sima.Web.Api.Helpers;

public static class PaginationHeaderUtil
{
    public static HttpContext AddPaginationHeader<T>(this HttpContext context, PagedList<T> pagedList)
        where T : class
    {
        const string headerKey = "X-Pagination";
        if (!context.Response.Headers.ContainsKey(headerKey))
        {
            context.Response.Headers.Append(headerKey, JsonSerializer.Serialize(new
            {
                totalPages = pagedList.TotalPages,
                currentPage = pagedList.CurrentPage,
                pageSize = pagedList.PageSize,
                totalCount = pagedList.TotalCount,
                hasPrevious = pagedList.HasPrevious,
                hasNext = pagedList.HasNext
            }));
        }
        return context;
    }
}

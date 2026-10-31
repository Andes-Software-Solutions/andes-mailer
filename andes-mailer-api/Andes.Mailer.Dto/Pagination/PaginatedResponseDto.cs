namespace Andes.Mailer.Dto.Pagination;

/// <summary>
/// Wraps one page of a list response.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Page">The one-based page number.</param>
/// <param name="PageSize">The maximum number of items on a page.</param>
/// <param name="TotalCount">The number of items across all pages.</param>
public sealed record PaginatedResponseDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

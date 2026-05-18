namespace PlantProcess.Application.Common.Paging;

public sealed record PageRequest(
    int Page = 1,
    int PageSize = 50)
{
    public const int MaxPageSize = 500;

    public int SafePage => Page < 1 ? 1 : Page;

    public int SafePageSize => PageSize switch
    {
        < 1 => 50,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };

    public int Skip => (SafePage - 1) * SafePageSize;
}



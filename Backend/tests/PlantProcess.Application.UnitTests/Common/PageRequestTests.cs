using FluentAssertions;
using PlantProcess.Application.Common.Paging;

namespace PlantProcess.Application.UnitTests.Common;

public sealed class PageRequestTests
{
    [Fact]
    public void SafePage_should_normalize_values_below_one()
    {
        var request = new PageRequest(Page: 0, PageSize: 50);

        request.SafePage.Should().Be(1);
    }

    [Fact]
    public void SafePageSize_should_default_when_value_is_invalid()
    {
        var request = new PageRequest(Page: 1, PageSize: 0);

        request.SafePageSize.Should().Be(50);
    }

    [Fact]
    public void SafePageSize_should_cap_to_max_page_size()
    {
        var request = new PageRequest(Page: 1, PageSize: 9999);

        request.SafePageSize.Should().Be(PageRequest.MaxPageSize);
    }

    [Fact]
    public void Skip_should_calculate_offset_from_safe_values()
    {
        var request = new PageRequest(Page: 3, PageSize: 25);

        request.Skip.Should().Be(50);
    }
}

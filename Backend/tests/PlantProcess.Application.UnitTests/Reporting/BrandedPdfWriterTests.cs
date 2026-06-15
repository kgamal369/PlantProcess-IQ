using System.Text;
using PlantProcess.Application.Reporting;

namespace PlantProcess.Application.UnitTests.Reporting;

public sealed class BrandedPdfWriterTests
{
    [Fact]
    public void Ppiq705_Pdf_Contains_Light_Surface_Header_And_Footer_Markers()
    {
        var bytes = BrandedPdfWriter.Create("Test report", new[] { "Evidence line" });
        var ascii = Encoding.ASCII.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", ascii);
        Assert.Contains("PPIQ-LIGHT-SURFACE:#F4F6F8", ascii);
        Assert.Contains("PPIQ-BRAND-HEADER", ascii);
        Assert.Contains("PPIQ-BRAND-FOOTER", ascii);
        Assert.Contains("0.9569 0.9647 0.9725 rg 0 0 595 842 re f", ascii);
        Assert.Contains("Connect Your Plant Data. Understand Your Process.", ascii);
    }
}
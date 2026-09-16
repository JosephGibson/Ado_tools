using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace AdoToolkit.Core.Tests.Reporting;

public sealed class SinkEncodingTests
{
    [Fact]
    public void FrameworkEncoderBehaviorIsPinnedBeforeRendererImplementation()
    {
        HtmlEncoder encoder = HtmlEncoder.Create(UnicodeRanges.All);
        Assert.Equal("&#xA0;&#x202F;’« »&#x1F600;", encoder.Encode("  ’« »😀"));
    }
}

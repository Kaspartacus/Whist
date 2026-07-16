using Core;
using ServerAPI.Utils;

namespace Whist.Tests;

public sealed class TextAutoReplaceTests
{
    [Theory]
    [InlineData("KSDH", "BIF<3")]
    [InlineData("ksdh for altid", "BIF<3 for altid")]
    [InlineData("Ingen match", "Ingen match")]
    public void Apply_ReplacesKsdhCaseInsensitive(string input, string expected)
    {
        Assert.Equal(expected, TextAutoReplace.Apply(input));
    }

    [Fact]
    public void Apply_DoesNotModifyImageUrl()
    {
        var highlight = new Highlight
        {
            Title = "KSDH",
            Description = "ksdh",
            ImageUrl = "https://example.test/uploads/KSDH.jpg"
        };

        TextAutoReplace.Apply(highlight);

        Assert.Equal("BIF<3", highlight.Title);
        Assert.Equal("BIF<3", highlight.Description);
        Assert.Equal("https://example.test/uploads/KSDH.jpg", highlight.ImageUrl);
    }
}

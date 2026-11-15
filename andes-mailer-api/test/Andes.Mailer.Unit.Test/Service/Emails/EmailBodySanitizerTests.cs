using Andes.Mailer.Service.Emails;

namespace Andes.Mailer.Unit.Test.Service.Emails;

public sealed class EmailBodySanitizerTests
{
    [Theory]
    [InlineData("<p>Hi <strong>team</strong>, <em>see</em> <u>below</u>.</p>")]
    [InlineData("<h2>Agenda</h2><ul><li>One</li><li>Two</li></ul><ol start=\"3\"><li>Three</li></ol>")]
    [InlineData("<blockquote><p>Quoted</p></blockquote><pre><code>var x = 1;</code></pre><hr>")]
    [InlineData("<p style=\"text-align: center\">Centred</p>")]
    [InlineData("<p><a href=\"https://contoso.com\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">link</a></p>")]
    public void Sanitize_EditorOutput_KeepsIt(string html)
    {
        Assert.Equal(html, EmailBodySanitizer.Sanitize(html));
    }

    [Theory]
    [InlineData("<p>Hi</p><script>alert(1)</script>", "<p>Hi</p>")]
    [InlineData("<p onclick=\"alert(1)\">Hi</p>", "<p>Hi</p>")]
    [InlineData("<p><a href=\"javascript:alert(1)\">x</a></p>", "<p><a>x</a></p>")]
    [InlineData("<p><img src=\"x\" onerror=\"alert(1)\">Hi</p>", "<p>Hi</p>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe><p>Hi</p>", "<p>Hi</p>")]
    [InlineData("<p style=\"position: fixed; text-align: left\">Hi</p>", "<p style=\"text-align: left\">Hi</p>")]
    public void Sanitize_UnsafeMarkup_StripsIt(string html, string expected)
    {
        Assert.Equal(expected, EmailBodySanitizer.Sanitize(html));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_Blank_ReturnsEmpty(string html)
    {
        Assert.Equal(string.Empty, EmailBodySanitizer.Sanitize(html));
    }
}

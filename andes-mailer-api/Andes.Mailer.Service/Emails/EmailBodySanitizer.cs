using Ganss.Xss;

namespace Andes.Mailer.Service.Emails;

/// <summary>
/// Strips an email body down to the formatting the compose editor produces.
/// </summary>
/// <remarks>
/// The allowlist covers TipTap's StarterKit, Underline, Link, and TextAlign output. Anything else (scripts, event handlers, images, embedded frames) is removed,
/// so a stored body is safe to render when the draft is reloaded.
/// </remarks>
public static class EmailBodySanitizer
{
    // Configured once here and never changed afterwards, which is what makes the shared instance thread-safe.
    private static readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    #region Private methods

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(
        [
            "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6",
            "strong", "b", "em", "i", "u", "s", "strike", "code", "pre", "blockquote",
            "ul", "ol", "li", "a", "span",
        ]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "target", "rel", "start", "style"]);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.Add("text-align");
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);
        sanitizer.AllowedAtRules.Clear();
        sanitizer.AllowDataAttributes = false;

        return sanitizer;
    }

    #endregion

    #region Public static methods

    /// <summary>
    /// Sanitizes an HTML email body.
    /// </summary>
    /// <param name="html">The body as the client sent it.</param>
    /// <returns>The body with every element, attribute, style, and URL scheme outside the allowlist removed.</returns>
    public static string Sanitize(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        return string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);
    }

    #endregion
}

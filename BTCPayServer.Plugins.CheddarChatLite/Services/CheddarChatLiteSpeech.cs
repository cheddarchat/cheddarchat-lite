using System;
using System.Text.RegularExpressions;

namespace BTCPayServer.Plugins.CheddarChatLite.Services;

/// <summary>
/// What the voice reads for a message: a link is spoken as "link to x.com" instead of the whole address.
/// </summary>
public static class CheddarChatLiteSpeech
{
    // A link starts with http(s):// or www., or is a bare domain: followed by a path (x.com/user), or ending in
    // one of the common endings that are not words of their own (x.com, but not "love.it" with a missing space).
    // Not inside another word, an email address or a longer address.
    private static readonly Regex LinkRegex = new(
        @"(?<![\w@./-])(?:(?:https?://|www\.)\S+" +
        @"|(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+" +
        @"(?:[a-z]{2,}/\S*|(?:com|net|org|io|co|tv|gg|ai|app|dev|xyz|info|edu|gov|fm|ly)(?![\w@-])(?:[:?#]\S*)?))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Punctuation after a link belongs to the sentence, not to the link
    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\''];

    public static string GetSpeech(string message) =>
        LinkRegex.Replace(message, match =>
        {
            var link = match.Value.TrimEnd(TrailingPunctuation);
            var url = link.Contains("://", StringComparison.Ordinal) ? link : $"https://{link}";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                return match.Value;

            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            return $"link to {host}{match.Value[link.Length..]}";
        });
}

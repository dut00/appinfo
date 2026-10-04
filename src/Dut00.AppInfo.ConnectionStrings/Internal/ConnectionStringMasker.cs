using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dut00.AppInfo.ConnectionStrings.Internal;

/// <summary>
/// Masks secrets in connection strings. Every rule fails closed: when in doubt, the whole value is masked.
/// </summary>
internal static class ConnectionStringMasker
{
    // postgres://user:pass@host/db, mongodb://..., redis://... are not key=value pairs. Defense in depth:
    // the key-shape rule below rejects these too, but this rule states the intent and doesn't depend on it.
    private static readonly Regex UriPrefix = new(@"^\s*[A-Za-z][A-Za-z0-9+.\-]*://", RegexOptions.CultureInvariant);

    // Real connection string keys are words separated by spaces, dots, dashes or underscores. Anything else
    // (for example "localhost:6379,password" from a Redis string) means the value isn't in key=value format.
    private static readonly Regex KeyShape = new(@"^[A-Za-z0-9][A-Za-z0-9 _.\-]*$", RegexOptions.CultureInvariant);

    // Finds key candidates in the original text, only to restore the casing the parser lowercases.
    private static readonly Regex KeyCandidate = new(@"(?:^|;)\s*([^;=]+?)\s*=", RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns <paramref name="value"/> with every sensitive part replaced by <paramref name="mask"/>.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Empty or whitespace values are returned as-is.</item>
    /// <item>URI-style values, values that can't be parsed, values without keys and values with keys that don't
    /// look like connection string keys are masked completely.</item>
    /// <item>A key is masked when it contains a sensitive word (ignoring case).</item>
    /// <item>A value that isn't a properly closed ODBC braced value (with <c>}}</c> escapes) means the parser split
    /// it; the whole value is masked.</item>
    /// <item>Any other value is masked when it contains a sensitive word followed by <c>=</c> or <c>:</c> (a nested
    /// connection string), contains <c>@</c> (credentials in an address), or contains <c>?</c> or <c>#</c> together
    /// with <c>/</c> or <c>:</c> (a signature in an address query), even when it isn't a valid URI.</item>
    /// </list>
    /// </remarks>
    public static string Mask(string value, IEnumerable<string> sensitiveKeys, string mask)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        try
        {
            return MaskPairs(value, sensitiveKeys, mask) ?? mask;
        }
        catch (Exception)
        {
            // DbConnectionStringBuilder throws ArgumentException for malformed input. Whatever goes wrong,
            // a secret must never leak because of it.
            return mask;
        }
    }

    // Returns null when the value must be masked completely.
    private static string? MaskPairs(string value, IEnumerable<string> sensitiveKeys, string mask)
    {
        if (UriPrefix.IsMatch(value))
        {
            return null;
        }

        var parsed = new DbConnectionStringBuilder { ConnectionString = value };
        if (parsed.Count == 0)
        {
            return null;
        }

        var words = sensitiveKeys.Where(word => !string.IsNullOrWhiteSpace(word)).Select(word => word.Trim()).ToArray();
        var originalKeys = OriginalKeys(value);
        var result = new StringBuilder();
        foreach (string key in parsed.Keys)
        {
            if (!KeyShape.IsMatch(key))
            {
                return null;
            }

            var text = Convert.ToString(parsed[key], CultureInfo.InvariantCulture) ?? string.Empty;

            // The parser doesn't treat ODBC braces as quoting, so PWD={ab;cd=secret} is split into "{ab" and
            // cd="secret}". A value that isn't a properly closed braced value means such a split happened.
            if (text.StartsWith('{') ? !IsClosedOdbcBrace(text) : text.EndsWith('}'))
            {
                return null;
            }

            var output = ContainsAny(key, words) || HidesSecret(text, words) ? mask : text;
            DbConnectionStringBuilder.AppendKeyValuePair(result, originalKeys.GetValueOrDefault(key, key), output);
        }

        return result.ToString();
    }

    // In ODBC "}}" inside braces is a literal "}", so "{ab}}" is still open. Closed means: ends with "}" and no
    // single "}" is left inside once the outer braces are stripped and "}}" pairs removed.
    private static bool IsClosedOdbcBrace(string text) =>
        text.Length >= 2 && text[^1] == '}' && !text[1..^1].Replace("}}", string.Empty, StringComparison.Ordinal).Contains('}');

    private static bool ContainsAny(string text, string[] words) =>
        words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static bool HidesSecret(string value, string[] words)
    {
        // Credentials in an address (user:pass@host, user/pass@host, http://user:pass@host, ...). Any "@" counts,
        // wherever it is, so odd prefixes, missing schemes or unicode tricks can't hide it.
        if (value.Contains('@'))
        {
            return true;
        }

        // Signatures or tokens in a query or fragment, for example BlobEndpoint=https://...?sv=...&sig=...
        // Anything with "?" or "#" that also has "/" or ":" counts as an address. Decided on the text, not by
        // Uri parsing, so addresses that aren't valid URIs (no scheme, "//host/...") are still caught.
        if (value.AsSpan().IndexOfAny('?', '#') >= 0 && value.AsSpan().IndexOfAny('/', ':') >= 0)
        {
            return true;
        }

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0))
        {
            return true;
        }

        // Nested pairs, for example Extended Properties="Excel 12.0;Password=xyz", '"Password"=xyz' or Password:xyz.
        foreach (var word in words)
        {
            for (var index = value.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                 index >= 0;
                 index = value.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase))
            {
                var next = index + word.Length;
                while (next < value.Length && (char.IsWhiteSpace(value[next]) || value[next] is '"' or '\''))
                {
                    next++;
                }

                if (next < value.Length && value[next] is '=' or ':')
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Maps each parsed (lowercased) key to its spelling in the original text. A candidate is used only when it
    // equals the parsed key ignoring case, so the output never contains text the parser didn't treat as a key.
    private static Dictionary<string, string> OriginalKeys(string value)
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in KeyCandidate.Matches(value))
        {
            keys.TryAdd(match.Groups[1].Value.Trim(), match.Groups[1].Value.Trim());
        }

        return keys;
    }
}

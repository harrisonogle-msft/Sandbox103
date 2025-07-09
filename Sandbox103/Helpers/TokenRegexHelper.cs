using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Sandbox103.Helpers;

public static class TokenRegexHelper
{
    private static readonly ConcurrentDictionary<(string, string), Regex> s_regexCache = new();

    public static string Expand(string source, Func<string, object?, string?> valueFactory, object? state = null, string? tokenPrefix = null, string? tokenSuffix = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentNullException.ThrowIfNull(valueFactory);

        tokenPrefix ??= "$(";
        tokenSuffix ??= ")";

        int prefixLength = tokenPrefix.Length;
        int suffixLength = tokenSuffix.Length;

        Regex regex = GetTokenRegex(tokenPrefix, tokenSuffix);

        int cursor = 0;
        ReadOnlySpan<char> chars = source.AsSpan();

        var sb = new StringBuilder();

        foreach (ValueMatch match in regex.EnumerateMatches(chars))
        {
            if (cursor < match.Index)
            {
                // Write everything before the token.
                sb.Append(chars.Slice(cursor, match.Index - cursor));
            }

            string token = source.Substring(match.Index + prefixLength, match.Length - prefixLength - suffixLength);

            string? value = valueFactory.Invoke(token, state);

            if (value is null)
            {
                Trace.TraceWarning($"Unable to replace token '{token}': no value was provided.");

                // If it can't be replaced, put the token back.
                sb.Append(tokenPrefix);
                sb.Append(token);
                sb.Append(tokenSuffix);
            }
            else
            {
                // Write the value to replace the token.
                sb.Append(value);
            }

            // Move the cursor beyond the current match.
            cursor = match.Index + match.Length;
        }

        if (cursor == 0)
        {
            // The source had no tokens to replace - return it as is.
            return source;
        }
        else if (cursor < chars.Length)
        {
            // Write everything after the match.
            sb.Append(chars.Slice(cursor, chars.Length - cursor));
        }

        return sb.ToString();
    }

    private static Regex GetTokenRegex(string tokenPrefix, string tokenSuffix)
    {
        return s_regexCache.GetOrAdd((tokenPrefix, tokenSuffix), static (key) =>
        {
            var (tokenPrefix, tokenSuffix) = key;
            return CreateTokenRegex(tokenPrefix, tokenSuffix);
        });
    }

    internal static Regex CreateTokenRegex(string tokenPrefix, string tokenSuffix)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(tokenPrefix);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(tokenSuffix);

        string escapedPrefix = Regex.Escape(tokenPrefix);
        string escapedSuffix = Regex.Escape(tokenSuffix);

        if (tokenPrefix != escapedPrefix)
        {
            Trace.WriteLine($"Using escaped token prefix: {escapedPrefix}");
        }

        if (tokenSuffix != escapedSuffix)
        {
            Trace.WriteLine($"Using escaped token suffix: {escapedSuffix}");
        }

        string pattern = $@"{escapedPrefix}([a-zA-Z0-9._-]+){escapedSuffix}";

        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.Singleline, TimeSpan.FromSeconds(10));
    }
}

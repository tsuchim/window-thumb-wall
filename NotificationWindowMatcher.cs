using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowThumbWall;

internal enum NotificationMatchKind
{
    None,
    Unique,
    Ambiguous
}

internal sealed record NotificationSignal(
    string AppUserModelId,
    string AppDisplayName,
    IReadOnlyList<string> NotificationTexts);

internal sealed record NotificationWindowCandidate(
    IntPtr Handle,
    string Title,
    string ProcessName,
    string ExecutablePath,
    string AppUserModelId);

internal sealed record NotificationMatchResult(
    NotificationMatchKind Kind,
    IReadOnlyList<IntPtr> CandidateHandles)
{
    internal static NotificationMatchResult None { get; } =
        new(NotificationMatchKind.None, []);
}

internal static partial class NotificationWindowMatcher
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "with", "from", "this", "that", "have", "your",
        "will", "into", "error", "warning", "info", "open", "click", "view",
        "task", "build", "branch", "repo", "project", "window", "notification",
        "message", "app", "desktop"
    };

    public static NotificationMatchResult Resolve(
        NotificationSignal signal,
        IReadOnlyList<NotificationWindowCandidate> windows)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(windows);

        List<NotificationWindowCandidate> candidates = windows
            .Where(static window => window.Handle != IntPtr.Zero && !string.IsNullOrWhiteSpace(window.Title))
            .DistinctBy(static window => window.Handle)
            .ToList();

        if (candidates.Count == 0)
            return NotificationMatchResult.None;

        List<NotificationWindowCandidate> appCompatible = NarrowByAppIdentity(candidates, signal);
        bool hasAppIdentity = HasAppIdentity(signal);
        if (hasAppIdentity && appCompatible.Count == 0)
            return NotificationMatchResult.None;

        List<NotificationWindowCandidate> narrowed = appCompatible.Count > 0 ? appCompatible : candidates;

        List<NotificationWindowCandidate> titleMatches = NarrowByTitleTokens(narrowed, signal.NotificationTexts);
        if (titleMatches.Count > 0)
            narrowed = titleMatches;

        return narrowed.Count switch
        {
            0 => NotificationMatchResult.None,
            1 => new NotificationMatchResult(NotificationMatchKind.Unique, [narrowed[0].Handle]),
            _ => new NotificationMatchResult(
                NotificationMatchKind.Ambiguous,
                narrowed.Select(static window => window.Handle).ToArray())
        };
    }

    private static List<NotificationWindowCandidate> NarrowByAppIdentity(
        IReadOnlyList<NotificationWindowCandidate> candidates,
        NotificationSignal signal)
    {
        List<NotificationWindowCandidate> aumidMatches = NarrowByExactAumid(candidates, signal.AppUserModelId);
        if (aumidMatches.Count > 0)
            return aumidMatches;

        return NarrowByExecutableHints(candidates, signal);
    }

    private static List<NotificationWindowCandidate> NarrowByTitleTokens(
        IReadOnlyList<NotificationWindowCandidate> candidates,
        IReadOnlyList<string> notificationTexts)
    {
        if (candidates.Count == 0 || notificationTexts.Count == 0)
            return [];

        Dictionary<NotificationWindowCandidate, HashSet<string>> titleTokens = candidates.ToDictionary(
            static window => window,
            static window => ExtractTitleTokens(window.Title));

        List<NotificationWindowCandidate> narrowed = [];
        foreach (string token in ExtractStrongTokens(notificationTexts))
        {
            List<NotificationWindowCandidate> matches = candidates
                .Where(window => titleTokens[window].Contains(token))
                .ToList();

            if (matches.Count == 1)
                return matches;

            if (matches.Count > 1 && (narrowed.Count == 0 || matches.Count < narrowed.Count))
                narrowed = matches;
        }

        return narrowed;
    }

    private static List<NotificationWindowCandidate> NarrowByExactAumid(
        IReadOnlyList<NotificationWindowCandidate> candidates,
        string appUserModelId)
    {
        string normalizedAumid = NormalizeIdentity(appUserModelId);
        if (string.IsNullOrEmpty(normalizedAumid))
            return [];

        return candidates
            .Where(window => NormalizeIdentity(window.AppUserModelId) == normalizedAumid)
            .ToList();
    }

    private static List<NotificationWindowCandidate> NarrowByExecutableHints(
        IReadOnlyList<NotificationWindowCandidate> candidates,
        NotificationSignal signal)
    {
        HashSet<string> hintTokens = ExtractExecutableHintTokens(signal);
        if (hintTokens.Count == 0)
            return [];

        return candidates
            .Where(window => MatchesExecutableHint(window, hintTokens))
            .ToList();
    }

    private static bool MatchesExecutableHint(NotificationWindowCandidate window, IReadOnlyCollection<string> hintTokens)
    {
        string processName = NormalizeIdentity(window.ProcessName);
        string executableName = NormalizeIdentity(Path.GetFileNameWithoutExtension(window.ExecutablePath) ?? string.Empty);

        foreach (string token in hintTokens)
        {
            if (string.IsNullOrEmpty(token))
                continue;

            if (processName == token || executableName == token)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> ExtractExecutableHintTokens(NotificationSignal signal)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);

        AddHintTokens(signal.AppDisplayName, tokens);
        AddHintTokens(signal.AppUserModelId, tokens);
        return tokens;
    }

    private static void AddHintTokens(string source, ISet<string> tokens)
    {
        foreach (string token in SplitIdentityTokens(source))
        {
            if (IsStrongExecutableToken(token))
                tokens.Add(token);
        }
    }

    private static List<string> ExtractStrongTokens(IReadOnlyList<string> notificationTexts)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (string text in notificationTexts)
        {
            foreach (string token in TokenRegex().Matches(NormalizeText(text)).Cast<Match>().Select(static match => match.Value))
            {
                if (IsStrongNotificationToken(token))
                    tokens.Add(token);
            }
        }

        return tokens
            .OrderByDescending(static token => ScoreToken(token))
            .ThenByDescending(static token => token.Length)
            .ToList();
    }

    private static bool IsStrongNotificationToken(string token)
    {
        if (StopWords.Contains(token))
            return false;

        if (ContainsCjk(token))
            return token.Length >= 2;

        if (HasIdentityShape(token))
            return token.Length >= 3;

        return token.Length >= 5;
    }

    private static bool IsStrongExecutableToken(string token)
    {
        if (StopWords.Contains(token))
            return false;

        return token.Length >= 3 && token.Any(char.IsLetter);
    }

    private static int ScoreToken(string token)
    {
        int score = token.Length;
        if (HasIdentityShape(token))
            score += 10;
        if (token.Any(char.IsDigit))
            score += 10;
        return score;
    }

    private static bool HasIdentityShape(string token) =>
        token.IndexOfAny(['-', '_', '/', '\\', '.', ':', '#']) >= 0;

    private static bool ContainsCjk(string token)
    {
        foreach (char c in token)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is UnicodeCategory.OtherLetter)
                return true;
        }

        return false;
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        StringBuilder builder = new(normalized.Length);
        bool previousWasWhitespace = false;
        foreach (char c in normalized)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasWhitespace)
                    builder.Append(' ');
                previousWasWhitespace = true;
            }
            else
            {
                builder.Append(c);
                previousWasWhitespace = false;
            }
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeIdentity(string value) => NormalizeText(value);

    private static HashSet<string> ExtractTitleTokens(string title)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TokenRegex().Matches(NormalizeText(title)))
            tokens.Add(match.Value);
        return tokens;
    }

    private static IEnumerable<string> SplitIdentityTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        foreach (string token in IdentitySplitRegex().Split(NormalizeText(value)))
        {
            if (!string.IsNullOrWhiteSpace(token))
                yield return token;
        }
    }

    private static bool HasAppIdentity(NotificationSignal signal) =>
        !string.IsNullOrWhiteSpace(signal.AppUserModelId) ||
        !string.IsNullOrWhiteSpace(signal.AppDisplayName);

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}\-_/\\.:#]{1,}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex IdentitySplitRegex();
}

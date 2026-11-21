using System;

namespace Video_Size_Optimizer.Utils
{
    public static class VersionHelper
    {
        /// <summary>
        /// Parses a release tag such as "v1.4.3", "1.4.1.1" or "v1.5.0-beta" into a Version.
        /// Missing parts count as 0, so "v1.4" equals "v1.4.0.0". Text after '-' or '+' is ignored.
        /// </summary>
        public static bool TryParse(string? tag, out Version version)
        {
            version = new Version(0, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(tag)) return false;

            string text = tag.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text[1..];
            int suffix = text.IndexOfAny(new[] { '-', '+', ' ' });
            if (suffix >= 0) text = text[..suffix];

            if (!Version.TryParse(text, out var parsed)) return false;

            version = new Version(
                parsed.Major,
                parsed.Minor,
                Math.Max(parsed.Build, 0),
                Math.Max(parsed.Revision, 0));
            return true;
        }

        /// <summary>
        /// True only when the remote tag is a newer version than the local one.
        /// If either tag cannot be parsed, falls back to "different means newer" (the old behaviour).
        /// </summary>
        public static bool IsNewer(string? remoteTag, string localTag)
        {
            if (TryParse(remoteTag, out var remote) && TryParse(localTag, out var local))
                return remote > local;

            return !string.Equals(remoteTag?.Trim(), localTag.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}

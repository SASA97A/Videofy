using System.Text;

namespace Video_Size_Optimizer.Utils
{
    /// <summary>
    /// Builds single arguments for ProcessStartInfo.Arguments.
    /// .NET splits the Arguments string with the Windows rules on every OS (also Linux and macOS).
    /// A path written as "\"{path}\"" breaks when it contains a double quote, which Linux and macOS
    /// file names may have: the quote is removed and ffmpeg looks for another file.
    /// </summary>
    public static class ProcessArgs
    {
        /// <summary>
        /// Wraps a value in double quotes and escapes it so it reaches the program unchanged.
        /// Normal Windows paths ("C:\Videos\a.mp4") come out exactly as "\"{path}\"" did before.
        /// </summary>
        public static string Quote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            int backslashes = 0;

            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    // Backslashes before a quote are doubled, and the quote itself gets one more.
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    // Backslashes not followed by a quote are taken literally.
                    sb.Append('\\', backslashes);
                    sb.Append(c);
                }
                backslashes = 0;
            }

            // Backslashes at the end come before the closing quote, so they are doubled.
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// Escapes a value for an FFMETADATA file (chapter titles). "=", ";", "#", "\" and line
        /// breaks have a meaning there and need a backslash in front.
        /// </summary>
        public static string EscapeMetadata(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c is '=' or ';' or '#' or '\\' or '\n') sb.Append('\\');
                if (c == '\r') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}

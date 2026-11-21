using Avalonia.Platform;
using System;
using System.IO;
using System.Text;

namespace Video_Size_Optimizer.Services;

/// <summary>
/// Linux has no icon inside the executable. The app menu, the panel and the dock find the icon
/// through a .desktop file whose StartupWMClass matches the window's WM_CLASS. This writes that
/// file and the icon into the user's own folders (no root needed) and keeps Exec up to date when
/// the executable is moved.
/// </summary>
public static class LinuxDesktopIntegration
{
    public const string WmClass = "Videofy";
    private const string Marker = "X-Videofy-Generated=true";

    public static void EnsureRegistered()
    {
        if (!OperatingSystem.IsLinux()) return;
        try
        {
            string? exePath = Environment.ProcessPath;
            // Started through "dotnet Videofy.dll" (development): there is no app executable to point at.
            if (string.IsNullOrEmpty(exePath) ||
                Path.GetFileNameWithoutExtension(exePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                return;

            string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg && Path.IsPathRooted(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

            string iconPath = Path.Combine(dataHome, "icons", "hicolor", "512x512", "apps", "videofy.png");
            WriteIcon(iconPath);

            string desktopPath = Path.Combine(dataHome, "applications", "videofy.desktop");
            // The user replaced our file with their own: leave it alone.
            if (File.Exists(desktopPath) && !File.ReadAllText(desktopPath).Contains(Marker)) return;

            string content = BuildDesktopEntry(exePath, iconPath);
            if (File.Exists(desktopPath) && File.ReadAllText(desktopPath) == content) return;

            Directory.CreateDirectory(Path.GetDirectoryName(desktopPath)!);
            File.WriteAllText(desktopPath, content, new UTF8Encoding(false));
            LogService.Instance.Log($"Linux app menu entry written: {desktopPath}", LogLevel.Info, "SysUtil");
        }
        catch (Exception ex)
        {
            LogService.Instance.Log($"Could not register the Linux app menu entry: {ex.Message}", LogLevel.Warning, "SysUtil");
        }
    }

    private static void WriteIcon(string iconPath)
    {
        using var source = AssetLoader.Open(new Uri("avares://Videofy/Assets/app.png"));
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        byte[] bytes = memory.ToArray();

        if (File.Exists(iconPath) && new FileInfo(iconPath).Length == bytes.Length &&
            File.ReadAllBytes(iconPath).AsSpan().SequenceEqual(bytes))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
        File.WriteAllBytes(iconPath, bytes);
    }

    public static string BuildDesktopEntry(string exePath, string iconPath)
    {
        var sb = new StringBuilder();
        sb.Append("[Desktop Entry]\n");
        sb.Append("Type=Application\n");
        sb.Append("Name=Videofy\n");
        sb.Append("Comment=Compress, convert and trim videos\n");
        sb.Append("Exec=").Append(QuoteExec(exePath)).Append('\n');
        sb.Append("Icon=").Append(EscapeString(iconPath)).Append('\n');
        sb.Append("Terminal=false\n");
        sb.Append("Categories=AudioVideo;Video;\n");
        sb.Append("StartupWMClass=").Append(WmClass).Append('\n');
        sb.Append(Marker).Append('\n');
        return sb.ToString();
    }

    // Desktop Entry spec: the Exec argument goes in double quotes with " ` $ \ escaped by a backslash,
    // "%" becomes "%%", and then the whole value is a string where "\" itself is written as "\\".
    private static string QuoteExec(string path)
    {
        var arg = new StringBuilder("\"");
        foreach (char c in path)
        {
            if (c is '"' or '`' or '$' or '\\') arg.Append('\\');
            arg.Append(c);
        }
        arg.Append('"');
        return EscapeString(arg.ToString().Replace("%", "%%"));
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}

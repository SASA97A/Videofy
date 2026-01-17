using System.Collections.Generic;
using System.Linq;


namespace Video_Size_Optimizer
{
    public static class AppConstants
    {
        public const string AppVersion = "v1.3.6";

        // Encoders
        public static readonly Dictionary<string, string> EncoderMap = new()
        {
            { "Standard (Slow, Best Quality)", "libx265" },
            { "NVIDIA GeForce (Fastest)", "hevc_nvenc" },
            { "NVIDIA GeForce (Legacy)", "h264_nvenc" },
            { "AMD Radeon (Fastest)", "hevc_amf" },
            { "AMD Radeon (Legacy)", "h264_amf" },
            { "Intel Graphics (Fastest)", "hevc_qsv" },
            { "Intel Graphics (Legacy)", "h264_qsv" }
        };
        public static List<string> AvailableEncoderNames => EncoderMap.Keys.ToList();

        // Resolutions
        public static readonly Dictionary<string, string> ResolutionMap = new()
        {
            { "Original Resolution", "Original" },
            { "4K (3840p)", "3840" },
            { "2K (1440p)", "2560" },
            { "Full HD (1080p)", "1920" },
            { "HD (720p)", "1280" },
            { "SD (480p)", "854" },
            { "Mobile (360p)", "640" }
        };
        public static List<string> AvailableResolutionNames => ResolutionMap.Keys.ToList();

        // CRF Labels helper
        public static string GetCrfLabel(int value) => value switch
        {
            <= 15 => "Archive Quality (Large)",
            <= 22 => "High Quality",
            <= 28 => "Balanced (Recommended)",
            <= 32 => "Small (Space Saver)",
            _ => "Tiny (Maximum Compression)"
        };

        // Framerate label
        public static List<string> FpsOptions { get; } = new() { "Original", "60", "30", "24" };

        public const string AboutMessage =
               $"Videofy {AppVersion}\n\n" +
               "A high-performance recursive video optimizer.\n" +
               "Compress your library using H.264/H.265 (HEVC) technology.\n\n" +
               "--- Encoding Pro-Tips ---\n" +
               "• Videos will not upscale if the source resolution is lower than selected.\n" +
               "• CPU (x265): Recommended CRF is 28.\n" +
               "• NVIDIA (NVENC): Aim for 2-5 digits LOWER than CPU (e.g., 23-25).\n" +
               "• AMD (AMF): Aim for 5-8 digits LOWER than CPU (e.g., 20-23).\n" +
               "• Intel (QSV): Similar to CPU, try 24-26 for best results.\n\n" +
               "--- Note on Source Files ---\n" +
               "If your source video was already recorded using a GPU (e.g., NVIDIA Shadowplay), " +
               "re-encoding with the same hardware encoder may not significantly reduce file size. " +
               "Use 'x265 (CPU)' for the highest possible compression ratio.\n\n";

        public const string NoSelectionMessage =
            "No valid files were found in your selection.\n\n" +
            "Note: Videos already marked as 'Completed' or files previously compressed by Videofy" +
            " (containing '-CRF' or '-Target') are skipped to prevent accidental double-compression.\n\n" +
            "Tip: Use the 'Refresh' button to reset the completion status of the list.";

        public const string NoFolderAccess =
            "Videofy could not resolve a local path for the selected folder.\n\n" +
            "This can happen if you select a Cloud drive (like OneDrive/iCloud) that isn't synchronized to your computer, " +
            "or a specialized network location.\n\n" +
            "Please select a folder stored directly on your hard drive.";
    }
}

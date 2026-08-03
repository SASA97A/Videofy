
using System.Collections.Generic;
using Video_Size_Optimizer.Utils;

namespace Video_Size_Optimizer.Models
{
    public class AppSettings
    {
        public bool DeleteOriginalAfterCompression { get; set; } = false;
        public string DefaultOutputFormat { get; set; } = AppConstants.OriginalFormat;
        public bool PreventSleep { get; set; } = true;
        public int LowDiskBufferGb { get; set; } = 5;
        public bool ProcessAlreadyOptimized { get; set; } = false;
        public List<string> EnabledEncoders { get; set; } = new() { "Standard (Slow, Best Quality)" };
        public string CustomExtensions { get; set; } = "";
        public bool PreventUpsampling { get; set; } = false;
        public bool UseSoftwareRendering { get; set; } = false;
        public bool AutoCheckUpdatesOnStartup { get; set; } = true;
    }
}

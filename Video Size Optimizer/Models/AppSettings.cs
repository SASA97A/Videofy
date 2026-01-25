
using System.Collections.Generic;

namespace Video_Size_Optimizer.Models
{
    public class AppSettings
    {
        public bool DeleteOriginalAfterCompression { get; set; } = false;
        public string DefaultOutputFormat { get; set; } = ".mp4";
        public bool PreventSleep { get; set; } = true;
        public int LowDiskBufferGb { get; set; } = 5;
        public bool ProcessAlreadyOptimized { get; set; } = false;
        public List<string> EnabledEncoders { get; set; } = new() { "Standard (Slow, Best Quality)" };
        public string CustomExtensions { get; set; } = "";
    }
}


namespace Video_Size_Optimizer.Models
{
    public class AppSettings
    {
        public bool DeleteOriginalAfterCompression { get; set; } = false;
        public string DefaultOutputFormat { get; set; } = ".mp4";
        public bool PreventSleep { get; set; } = true;
        public int LowDiskBufferGb { get; set; } = 5;
    }
}

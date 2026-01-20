
namespace Video_Size_Optimizer.Models
{
    public class AppSettings
    {
        public bool DeleteOriginalAfterCompression { get; set; } = false;
        public string DefaultOutputFormat { get; set; } = ".mp4"; 
    }
}

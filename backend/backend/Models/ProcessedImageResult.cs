namespace backend.Models
{
    public class ProcessedImageResult
    {
        public byte[] ImageBytes { get; set; } = null!;
        public string ContentType { get; set; } = "image/png";
    }
}

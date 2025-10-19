namespace backend.Models
{
    public class ImageUploadRequest
    {
        public IFormFile Image { get; set; } = null!;
    }
}

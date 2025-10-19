using backend.Models;

namespace backend.Services.Interfaces
{
    public interface IImageProcessingService
    {
        Task<ProcessedImageResult> ProcessImageAsync(Stream imageStream);
    }
}

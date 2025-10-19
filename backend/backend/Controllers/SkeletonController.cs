using backend.Models;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SkeletonController : ControllerBase
    {
        private readonly IImageProcessingService _imageService;

        public SkeletonController(IImageProcessingService imageService)
        {
            _imageService = imageService;
        }

        [HttpPost("skeletonize")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Skeletonize([FromForm] ImageUploadRequest request)
        {
            if (request.Image == null || request.Image.Length == 0)
                return BadRequest("No image uploaded.");

            var result = await _imageService.ProcessImageAsync(request.Image.OpenReadStream());
            return File(result.ImageBytes, result.ContentType);
        }
    }
}

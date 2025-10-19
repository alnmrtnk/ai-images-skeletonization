using backend.Models;
using backend.Services.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace backend.Services.Implementations
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;

        public ImageProcessingService(ILogger<ImageProcessingService> logger)
        {
            _logger = logger;
        }

        public async Task<ProcessedImageResult> ProcessImageAsync(Stream imageStream)
        {
            try
            {
                using var image = await Image.LoadAsync<Rgba32>(imageStream);

                // Enhance contrast for cleaner edges
                image.Mutate(ctx => ctx.Contrast(1.5f));

                var gray = ConvertToGrayscale(image);

                var binary = Binarize(gray);
                binary = MorphologicalClose(binary, 1);

                var skeleton = HilditchThinning(binary);
                NormalizeSkeleton(skeleton);
                MarkEndpointsAndBranches(skeleton);

                using var ms = new MemoryStream();
                await skeleton.SaveAsPngAsync(ms);

                return new ProcessedImageResult
                {
                    ImageBytes = ms.ToArray(),
                    ContentType = "image/png"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image processing failed: {Message}", ex.Message);
                throw;
            }
        }

        // --- Step 1: Grayscale ---
        private Image<Rgba32> ConvertToGrayscale(Image<Rgba32> image)
        {
            var clone = image.Clone(ctx => ctx.Grayscale());
            return clone;
        }

        // --- Step 2: Adaptive binarization with improved threshold ---
        private Image<Rgba32> Binarize(Image<Rgba32> gray)
        {
            var result = gray.Clone();
            int threshold = ComputeOtsuThreshold(gray);

            result.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var v = (row[x].R + row[x].G + row[x].B) / 3;
                        row[x] = v < threshold
                            ? Color.Black
                            : Color.White;
                    }
                }
            });

            return result;
        }

        // --- Step 3: Otsu threshold calculation ---
        private int ComputeOtsuThreshold(Image<Rgba32> img)
        {
            long[] hist = new long[256];
            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        int v = (row[x].R + row[x].G + row[x].B) / 3;
                        hist[v]++;
                    }
                }
            });

            long total = img.Width * img.Height;
            long sum = 0;
            for (int i = 0; i < 256; i++) sum += i * hist[i];

            long sumB = 0, wB = 0, wF = 0;
            double maxVar = 0.0;
            int threshold = 128;

            for (int t = 0; t < 256; t++)
            {
                wB += hist[t];
                if (wB == 0) continue;
                wF = total - wB;
                if (wF == 0) break;

                sumB += t * hist[t];
                double mB = (double)sumB / wB;
                double mF = (double)(sum - sumB) / wF;
                double varBetween = wB * wF * Math.Pow(mB - mF, 2);

                if (varBetween > maxVar)
                {
                    maxVar = varBetween;
                    threshold = t;
                }
            }

            return (int)(threshold * 0.85); // Reduce threshold slightly to capture more detail
        }

        // --- Step 3.5: Morphological operations ---
        private Image<Rgba32> MorphologicalClose(Image<Rgba32> binary, int iterations)
        {
            var result = binary.Clone();

            for (int iter = 0; iter < iterations; iter++)
            {
                result = Dilate(result);
                result = Erode(result);
            }

            return result;
        }

        private Image<Rgba32> Dilate(Image<Rgba32> binary)
        {
            var result = binary.Clone();

            for (int y = 1; y < binary.Height - 1; y++)
            {
                for (int x = 1; x < binary.Width - 1; x++)
                {
                    if (binary[x, y].R < 128) continue; // Already black

                    // Check 8-neighborhood
                    bool hasBlackNeighbor = false;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (binary[x + dx, y + dy].R < 128)
                            {
                                hasBlackNeighbor = true;
                                break;
                            }
                        }
                        if (hasBlackNeighbor) break;
                    }

                    if (hasBlackNeighbor)
                    {
                        result[x, y] = Color.Black;
                    }
                }
            }

            return result;
        }

        private Image<Rgba32> Erode(Image<Rgba32> binary)
        {
            var result = binary.Clone();

            for (int y = 1; y < binary.Height - 1; y++)
            {
                for (int x = 1; x < binary.Width - 1; x++)
                {
                    if (binary[x, y].R >= 128) continue; // Already white

                    // Check 8-neighborhood
                    bool allNeighborsBlack = true;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (binary[x + dx, y + dy].R >= 128)
                            {
                                allNeighborsBlack = false;
                                break;
                            }
                        }
                        if (!allNeighborsBlack) break;
                    }

                    if (!allNeighborsBlack)
                    {
                        result[x, y] = Color.White;
                    }
                }
            }

            return result;
        }

        // --- Step 4: Hilditch Thinning ---
        private Image<Rgba32> HilditchThinning(Image<Rgba32> binary)
        {
            var bmp = binary.Clone();
            bool changed;
            int iteration = 0;

            // Convert to Hilditch convention: BLACK = 1, WHITE = 0
            int width = bmp.Width;
            int height = bmp.Height;
            int[,] imageData = new int[height, width];

            // Initialize: black pixels (R < 128) become 1, white pixels become 0
            bmp.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        imageData[y, x] = row[x].R < 128 ? 1 : 0;
                    }
                }
            });

            do
            {
                changed = false;
                iteration++;
                var toRemove = new List<(int x, int y)>();

                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (imageData[y, x] != 1) continue; // Skip white pixels

                        // Get 8-neighborhood in Hilditch order
                        int p2 = imageData[y - 1, x];     // North
                        int p3 = imageData[y - 1, x + 1]; // Northeast
                        int p4 = imageData[y, x + 1];     // East
                        int p5 = imageData[y + 1, x + 1]; // Southeast
                        int p6 = imageData[y + 1, x];     // South
                        int p7 = imageData[y + 1, x - 1]; // Southwest
                        int p8 = imageData[y, x - 1];     // West
                        int p9 = imageData[y - 1, x - 1]; // Northwest

                        // Condition 1: 2 <= B(p1) <= 6
                        int B = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9;
                        if (B < 2 || B > 6) continue;

                        // Condition 2: A(p1) = 1 (connectivity number)
                        int A = GetConnectivityNumber(p2, p3, p4, p5, p6, p7, p8, p9);
                        if (A != 1) continue;

                        // Condition 3: p2 * p4 * p8 = 0 OR A(p2) != 1
                        bool condition3 = (p2 * p4 * p8 == 0) ||
                                          (GetAForNeighbor(imageData, x, y - 1) != 1); // A(p2)

                        if (!condition3) continue;

                        // Condition 4: p2 * p4 * p6 = 0 OR A(p4) != 1
                        bool condition4 = (p2 * p4 * p6 == 0) ||
                                          (GetAForNeighbor(imageData, x + 1, y) != 1); // A(p4)

                        if (!condition4) continue;

                        // All conditions satisfied - mark for removal
                        toRemove.Add((x, y));
                    }
                }

                // Remove marked pixels
                foreach (var (x, y) in toRemove)
                {
                    imageData[y, x] = 0;
                    changed = true;
                }

            } while (changed);

            // Convert back to image: 1 -> black (0), 0 -> white (255)
            bmp.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x] = imageData[y, x] == 1 ? Color.Black : Color.White;
                    }
                }
            });

            return bmp;
        }

        // CORRECTED: Count 0->1 transitions (white to black)
        private int GetConnectivityNumber(int p2, int p3, int p4, int p5, int p6, int p7, int p8, int p9)
        {
            int[] neighbors = { p2, p3, p4, p5, p6, p7, p8, p9 };
            int count = 0;

            for (int i = 0; i < 8; i++)
            {
                // Count transitions from 0 to 1 (white to black)
                if (neighbors[i] == 0 && neighbors[(i + 1) % 8] == 1)
                    count++;
            }

            return count;
        }

        // Calculate A value for a neighbor pixel (for conditions 3 and 4)
        private int GetAForNeighbor(int[,] imageData, int x, int y)
        {
            int height = imageData.GetLength(0);
            int width = imageData.GetLength(1);

            // Boundary check
            if (x <= 0 || x >= width - 1 || y <= 0 || y >= height - 1)
                return 0;

            int p2 = imageData[y - 1, x];
            int p3 = imageData[y - 1, x + 1];
            int p4 = imageData[y, x + 1];
            int p5 = imageData[y + 1, x + 1];
            int p6 = imageData[y + 1, x];
            int p7 = imageData[y + 1, x - 1];
            int p8 = imageData[y, x - 1];
            int p9 = imageData[y - 1, x - 1];

            return GetConnectivityNumber(p2, p3, p4, p5, p6, p7, p8, p9);
        }

        // Normalize skeleton to pure black/white
        private void NormalizeSkeleton(Image<Rgba32> bmp)
        {
            bmp.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        // Force pure black or pure white
                        row[x] = row[x].R < 128 ? Color.Black : Color.White;
                    }
                }
            });
        }

        // --- Step 5: Detection using crossing number (CN) ---
        private void MarkEndpointsAndBranches(Image<Rgba32> bmp)
        {
            int endpoints = 0, branches = 0;
            int width = bmp.Width;
            int height = bmp.Height;

            var endpointList = new List<(int x, int y)>();
            var branchList = new List<(int x, int y)>();

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    // Check if this is a skeleton pixel
                    if (bmp[x, y].R >= 128) continue;

                    // Get 8-neighborhood (clockwise from top)
                    int[] neighbors = new int[8];
                    neighbors[0] = bmp[x, y - 1].R < 128 ? 1 : 0;     // N
                    neighbors[1] = bmp[x + 1, y - 1].R < 128 ? 1 : 0; // NE
                    neighbors[2] = bmp[x + 1, y].R < 128 ? 1 : 0;     // E
                    neighbors[3] = bmp[x + 1, y + 1].R < 128 ? 1 : 0; // SE
                    neighbors[4] = bmp[x, y + 1].R < 128 ? 1 : 0;     // S
                    neighbors[5] = bmp[x - 1, y + 1].R < 128 ? 1 : 0; // SW
                    neighbors[6] = bmp[x - 1, y].R < 128 ? 1 : 0;     // W
                    neighbors[7] = bmp[x - 1, y - 1].R < 128 ? 1 : 0; // NW

                    // Calculate Crossing Number (CN) - half the number of 0-1 transitions
                    int cn = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (neighbors[i] == 0 && neighbors[(i + 1) % 8] == 1)
                            cn++;
                    }

                    // CN = 1 means endpoint
                    // CN = 2 means normal skeleton point
                    // CN = 3 means branch point (junction)

                    if (cn == 1)
                    {
                        endpointList.Add((x, y));
                    }
                    else if (cn >= 3)
                    {
                        branchList.Add((x, y));
                    }
                }
            }

            foreach (var (x, y) in endpointList)
            {
                DrawCircle(bmp, x, y, 3, Color.Red);
                endpoints++;
            }

            foreach (var (x, y) in branchList)
            {
                DrawCircle(bmp, x, y, 3, Color.Blue);
                branches++;
            }
        }

        // Helper method to draw a filled circle
        private void DrawCircle(Image<Rgba32> bmp, int centerX, int centerY, int radius, Color color)
        {
            int width = bmp.Width;
            int height = bmp.Height;

            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height)
                        continue;

                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        bmp[x, y] = color;
                    }
                }
            }
        }
    }
}

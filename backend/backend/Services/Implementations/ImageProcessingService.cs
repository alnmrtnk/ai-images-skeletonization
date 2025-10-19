using backend.Models;
using backend.Services.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;

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

        private Image<Rgba32> ConvertToGrayscale(Image<Rgba32> image)
        {
            var clone = image.Clone(ctx => ctx.Grayscale());
            return clone;
        }

        // Sequential binarization (ProcessPixelRows doesn't support Parallel)
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
                        row[x] = v < threshold ? Color.Black : Color.White;
                    }
                }
            });

            return result;
        }

        // PARALLELIZED: Otsu threshold calculation
        private int ComputeOtsuThreshold(Image<Rgba32> img)
        {
            long[] hist = new long[256];
            var lockObj = new object();

            // Extract pixel data first
            int width = img.Width;
            int height = img.Height;
            var pixelValues = new byte[height, width];

            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        pixelValues[y, x] = (byte)((row[x].R + row[x].G + row[x].B) / 3);
                    }
                }
            });

            // Now parallel histogram computation
            Parallel.For(0, height, () => new long[256],
                (y, loop, localHist) =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        localHist[pixelValues[y, x]]++;
                    }
                    return localHist;
                },
                localHist =>
                {
                    lock (lockObj)
                    {
                        for (int i = 0; i < 256; i++)
                            hist[i] += localHist[i];
                    }
                });

            long total = width * height;
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

            return (int)(threshold * 0.85);
        }

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

        // PARALLELIZED: Dilation using direct pixel access
        private Image<Rgba32> Dilate(Image<Rgba32> binary)
        {
            var result = binary.Clone();
            int width = binary.Width;
            int height = binary.Height;

            Parallel.For(1, height - 1, y =>
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (binary[x, y].R < 128) continue;

                    bool hasBlackNeighbor = false;
                    for (int dy = -1; dy <= 1 && !hasBlackNeighbor; dy++)
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
                    }

                    if (hasBlackNeighbor)
                    {
                        result[x, y] = Color.Black;
                    }
                }
            });

            return result;
        }

        // PARALLELIZED: Erosion using direct pixel access
        private Image<Rgba32> Erode(Image<Rgba32> binary)
        {
            var result = binary.Clone();
            int width = binary.Width;
            int height = binary.Height;

            Parallel.For(1, height - 1, y =>
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (binary[x, y].R >= 128) continue;

                    bool allNeighborsBlack = true;
                    for (int dy = -1; dy <= 1 && allNeighborsBlack; dy++)
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
                    }

                    if (!allNeighborsBlack)
                    {
                        result[x, y] = Color.White;
                    }
                }
            });

            return result;
        }

        // PARALLELIZED: Hilditch Thinning
        private Image<Rgba32> HilditchThinning(Image<Rgba32> binary)
        {
            var bmp = binary.Clone();
            bool changed;
            int iteration = 0;

            int width = bmp.Width;
            int height = bmp.Height;
            int[,] imageData = new int[height, width];

            // Extract to array first
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

                var toRemove = new ConcurrentBag<(int x, int y)>();

                // PARALLELIZED: Pixel scanning
                Parallel.For(1, height - 1, y =>
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (imageData[y, x] != 1) continue;

                        int p2 = imageData[y - 1, x];
                        int p3 = imageData[y - 1, x + 1];
                        int p4 = imageData[y, x + 1];
                        int p5 = imageData[y + 1, x + 1];
                        int p6 = imageData[y + 1, x];
                        int p7 = imageData[y + 1, x - 1];
                        int p8 = imageData[y, x - 1];
                        int p9 = imageData[y - 1, x - 1];

                        int B = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9;
                        if (B < 2 || B > 6) continue;

                        int A = GetConnectivityNumber(p2, p3, p4, p5, p6, p7, p8, p9);
                        if (A != 1) continue;

                        bool condition3 = (p2 * p4 * p8 == 0) ||
                                          (GetAForNeighbor(imageData, x, y - 1) != 1);
                        if (!condition3) continue;

                        bool condition4 = (p2 * p4 * p6 == 0) ||
                                          (GetAForNeighbor(imageData, x + 1, y) != 1);
                        if (!condition4) continue;

                        toRemove.Add((x, y));
                    }
                });

                foreach (var (x, y) in toRemove)
                {
                    imageData[y, x] = 0;
                    changed = true;
                }

            } while (changed);

            // Write back to image
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

        private int GetConnectivityNumber(int p2, int p3, int p4, int p5, int p6, int p7, int p8, int p9)
        {
            int[] neighbors = { p2, p3, p4, p5, p6, p7, p8, p9 };
            int count = 0;

            for (int i = 0; i < 8; i++)
            {
                if (neighbors[i] == 0 && neighbors[(i + 1) % 8] == 1)
                    count++;
            }

            return count;
        }

        private int GetAForNeighbor(int[,] imageData, int x, int y)
        {
            int height = imageData.GetLength(0);
            int width = imageData.GetLength(1);

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

        // Sequential normalization (fast enough as is)
        private void NormalizeSkeleton(Image<Rgba32> bmp)
        {
            bmp.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x] = row[x].R < 128 ? Color.Black : Color.White;
                    }
                }
            });
        }

        // PARALLELIZED: Endpoint and branch detection
        private void MarkEndpointsAndBranches(Image<Rgba32> bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;

            var endpointList = new ConcurrentBag<(int x, int y)>();
            var branchList = new ConcurrentBag<(int x, int y)>();

            // PARALLELIZED detection
            Parallel.For(1, height - 1, y =>
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (bmp[x, y].R >= 128) continue;

                    int[] neighbors = new int[8];
                    neighbors[0] = bmp[x, y - 1].R < 128 ? 1 : 0;
                    neighbors[1] = bmp[x + 1, y - 1].R < 128 ? 1 : 0;
                    neighbors[2] = bmp[x + 1, y].R < 128 ? 1 : 0;
                    neighbors[3] = bmp[x + 1, y + 1].R < 128 ? 1 : 0;
                    neighbors[4] = bmp[x, y + 1].R < 128 ? 1 : 0;
                    neighbors[5] = bmp[x - 1, y + 1].R < 128 ? 1 : 0;
                    neighbors[6] = bmp[x - 1, y].R < 128 ? 1 : 0;
                    neighbors[7] = bmp[x - 1, y - 1].R < 128 ? 1 : 0;

                    int cn = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (neighbors[i] == 0 && neighbors[(i + 1) % 8] == 1)
                            cn++;
                    }

                    if (cn == 1)
                    {
                        endpointList.Add((x, y));
                    }
                    else if (cn >= 3)
                    {
                        branchList.Add((x, y));
                    }
                }
            });

            // Sequential drawing
            foreach (var (x, y) in endpointList)
            {
                DrawCircle(bmp, x, y, 3, Color.Red);
            }

            foreach (var (x, y) in branchList)
            {
                DrawCircle(bmp, x, y, 3, Color.Blue);
            }
        }

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

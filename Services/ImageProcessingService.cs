using System.Diagnostics;
using System.Text.Json;
using BellPepperMVC.Data;
using BellPepperMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace BellPepperMVC.Services
{
    public interface IImageProcessingService
    {
        Task<BellPepperImage> ProcessImageAsync(IFormFile file, string userId, bool requestDetailedAnalysis);
        Task<BellPepperImage?> GetAnalysisAsync(int id);
        Task<IEnumerable<BellPepperImage>> GetUserAnalysesAsync(string userId, int take = 10);
        Task<Dictionary<string, int>> GetMaturityDistributionAsync(string userId);
    }

    public class ImageProcessingResult
    {
        public string Status { get; set; }
        public string Prediction { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, string> Plots { get; set; }
        public Dictionary<string, double> Features { get; set; }
        public string Error { get; set; }
    }

    public class ImageProcessingService : IImageProcessingService
    {
        private readonly BellPepperMVCContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImageProcessingService> _logger;

        public ImageProcessingService(
            BellPepperMVCContext context,
            IConfiguration configuration,
            ILogger<ImageProcessingService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<BellPepperImage> ProcessImageAsync(IFormFile file, string userId, bool requestDetailedAnalysis)
        {
            // Create temp directory if it doesn't exist
            var tempPath = _configuration["PythonSettings:TempUploadPath"];
            Directory.CreateDirectory(tempPath);

            // Save uploaded file temporarily
            var tempFilePath = Path.Combine(tempPath, file.FileName);
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                // Create BellPepperImage entity
                var bellPepperImage = new BellPepperImage
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    UploadDate = DateTime.UtcNow,
                    UserId = userId,
                    HasDetailedAnalysis = requestDetailedAnalysis
                };

                // Read file bytes for storage
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    bellPepperImage.Image = ms.ToArray();
                }

                if (requestDetailedAnalysis)
                {
                    // Run detailed analysis
                    var detailedResult = await RunDetailedAnalysisAsync(tempFilePath);

                    // Store detailed analysis results
                    bellPepperImage.ProcessedImage = Convert.FromBase64String(detailedResult.Plots["processed_image"]);
                    bellPepperImage.SpectrumR = Convert.FromBase64String(detailedResult.Plots["spectrum_R"]);
                    bellPepperImage.SpectrumG = Convert.FromBase64String(detailedResult.Plots["spectrum_G"]);
                    bellPepperImage.SpectrumB = Convert.FromBase64String(detailedResult.Plots["spectrum_B"]);
                    bellPepperImage.SpectrumCombined = Convert.FromBase64String(detailedResult.Plots["spectrum_combined"]);
                    bellPepperImage.InverseFFT = Convert.FromBase64String(detailedResult.Plots["inverse_fft"]);
                    bellPepperImage.SobelH1 = Convert.FromBase64String(detailedResult.Plots["sobel_h1"]);
                    bellPepperImage.SobelH2 = Convert.FromBase64String(detailedResult.Plots["sobel_h2"]);

                    bellPepperImage.PredictedMaturityLevel = detailedResult.Prediction;
                    bellPepperImage.PredictionConfidence = Convert.ToDecimal(detailedResult.Confidence);

                    // Store feature values
                    bellPepperImage.MaxValue = Convert.ToDecimal(detailedResult.Features["max_value"]);
                    bellPepperImage.MinValue = Convert.ToDecimal(detailedResult.Features["min_value"]);
                    bellPepperImage.StdValue = Convert.ToDecimal(detailedResult.Features["std_value"]);
                    bellPepperImage.MeanValue = Convert.ToDecimal(detailedResult.Features["mean_value"]);
                    bellPepperImage.MedianValue = Convert.ToDecimal(detailedResult.Features["median_value"]);
                }
                else
                {
                    // Run quick analysis
                    var quickResult = await RunQuickAnalysisAsync(tempFilePath);
                    bellPepperImage.PredictedMaturityLevel = quickResult.Prediction;
                    bellPepperImage.PredictionConfidence = Convert.ToDecimal(quickResult.Confidence);
                }

                // Save to database
                _context.BellPepperImages.Add(bellPepperImage);
                await _context.SaveChangesAsync();

                return bellPepperImage;
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        private async Task<ImageProcessingResult> RunQuickAnalysisAsync(string imagePath)
        {
            var pythonPath = _configuration["PythonSettings:PythonPath"];
            var scriptPath = _configuration["PythonSettings:AnalysisScriptPath"];

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" \"{imagePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError($"Python analysis failed: {error}");
                throw new Exception($"Analysis failed: {error}");
            }

            return JsonSerializer.Deserialize<ImageProcessingResult>(output);
        }

        private async Task<ImageProcessingResult> RunDetailedAnalysisAsync(string imagePath)
        {
            var pythonPath = _configuration["PythonSettings:PythonPath"];
            var scriptPath = _configuration["PythonSettings:DetailedAnalysisScriptPath"];
            var outputDir = Path.Combine(_configuration["PythonSettings:TempUploadPath"], "analysis_output");
            Directory.CreateDirectory(outputDir);

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" \"{imagePath}\" \"{outputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError($"Python detailed analysis failed: {error}");
                throw new Exception($"Detailed analysis failed: {error}");
            }

            return JsonSerializer.Deserialize<ImageProcessingResult>(output);
        }

        public async Task<BellPepperImage?> GetAnalysisAsync(int id)
        {
            return await _context.BellPepperImages
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<IEnumerable<BellPepperImage>> GetUserAnalysesAsync(string userId, int take = 10)
        {
            return await _context.BellPepperImages
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UploadDate)
                .Take(take)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetMaturityDistributionAsync(string userId)
        {
            return await _context.BellPepperImages
                .Where(x => x.UserId == userId)
                .GroupBy(x => x.PredictedMaturityLevel)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Count()
                );
        }
    }
}
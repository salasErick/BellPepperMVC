using System.Diagnostics;
using System.Text.Json;
using BellPepperMVC.Data;
using BellPepperMVC.Models;
using Microsoft.CodeAnalysis.Diagnostics;
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
        private readonly string _tempUploadPath;

        public ImageProcessingService(
            BellPepperMVCContext context,
            IConfiguration configuration,
            ILogger<ImageProcessingService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _tempUploadPath = _configuration["PythonSettings:TempUploadPath"];
            Directory.CreateDirectory(_tempUploadPath); // Ensure temp directory exists
        }


        public async Task<BellPepperImage> ProcessImageAsync(IFormFile file, string userId, bool requestDetailedAnalysis)
        {
            var tempUploadPath = Path.GetFullPath(_configuration["PythonSettings:TempUploadPath"]);
            var tempFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var tempFilePath = Path.GetFullPath(Path.Combine(tempUploadPath, tempFileName));

            try
            {
                Directory.CreateDirectory(tempUploadPath);

                // Save the uploaded file temporarily
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var pythonPath = _configuration["PythonSettings:PythonPath"];
                var scriptPath = _configuration["PythonSettings:AnalysisScriptPath"];
                var modelPath = _configuration["PythonSettings:ModelPath"];

                _logger.LogInformation($"Image temp path: {tempFilePath}");
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\" \"{tempFilePath}\" \"{modelPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = _configuration["PythonSettings:ProjectPath"]
                };

                _logger.LogInformation($"Image temp path: {tempFilePath}");
                _logger.LogInformation($"Full Python command: {startInfo.FileName} {startInfo.Arguments}");
                using var process = Process.Start(startInfo);
                var output = await process.StandardOutput.ReadToEndAsync();
                _logger.LogInformation($"Raw Python output: {output}");

                var error = await process.StandardError.ReadToEndAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<AnalysisResult>(output, options);

                _logger.LogInformation($"Deserialized result - Status: {result?.Status}, Prediction: {result?.Prediction}, Confidence: {result?.Confidence}");

                if (result == null || result.Status != "success")
                {
                    throw new Exception($"Analysis failed: {result?.Error ?? "Unknown error"}");
                }
                await process.WaitForExitAsync();

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    throw new Exception($"Analysis failed. Error: {error}");
                }

                if (result == null)
                {
                    throw new Exception("Failed to parse analysis results");
                }

                var bellPepperImage = new BellPepperImage
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    UploadDate = DateTime.UtcNow,
                    UserId = userId,
                    PredictedMaturityLevel = result.Prediction ?? "Unknown",
                    PredictionConfidence = Convert.ToDecimal(result.Confidence),
                    HasDetailedAnalysis = requestDetailedAnalysis
                };

                // Save the original image
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    bellPepperImage.Image = ms.ToArray();
                }

                if (requestDetailedAnalysis && result.Features != null)
                {
                    bellPepperImage.MaxValue = Convert.ToDecimal(result.Features.MaxValue);
                    bellPepperImage.MinValue = Convert.ToDecimal(result.Features.MinValue);
                    bellPepperImage.StdValue = Convert.ToDecimal(result.Features.StdValue);
                    bellPepperImage.MeanValue = Convert.ToDecimal(result.Features.MeanValue);
                    bellPepperImage.MedianValue = Convert.ToDecimal(result.Features.MedianValue);
                }

                _context.BellPepperImages.Add(bellPepperImage);
                await _context.SaveChangesAsync();

                return bellPepperImage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image: {Message}", ex.Message);
                throw;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        public async Task<IEnumerable<BellPepperImage>> GetUserAnalysesAsync(string userId, int take = 10)
        {
            return await _context.BellPepperImages
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UploadDate)
                .Take(take)
                .ToListAsync();
        }

        public async Task<BellPepperImage?> GetAnalysisAsync(int id)
        {
            return await _context.BellPepperImages.FindAsync(id);
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
    public class AnalysisResult
    {
        public string Status { get; set; }
        public string Prediction { get; set; }
        public double Confidence { get; set; }
        public Features Features { get; set; }
        public string Error { get; set; }
    }


    public class Features
    {
        public double MaxValue { get; set; }
        public double MinValue { get; set; }
        public double StdValue { get; set; }
        public double MeanValue { get; set; }
        public double MedianValue { get; set; }
    }

    public static class FormFileExtensions
    {
        public static async Task<byte[]> GetBytesAsync(this IFormFile formFile)
        {
            using var memoryStream = new MemoryStream();
            await formFile.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
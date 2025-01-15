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
            var tempFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var tempFilePath = Path.Combine(_configuration["PythonSettings:TempUploadPath"], tempFileName);

            try
            {
                Directory.CreateDirectory(_configuration["PythonSettings:TempUploadPath"]);

                // Save the uploaded file temporarily
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var pythonPath = _configuration["PythonSettings:PythonPath"];
                var scriptPath = requestDetailedAnalysis ?
                    _configuration["PythonSettings:DetailedAnalysisScriptPath"] :
                    _configuration["PythonSettings:AnalysisScriptPath"];
                var modelPath = _configuration["PythonSettings:ModelPath"];
                var projectPath = _configuration["PythonSettings:ProjectPath"];

                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\" \"{tempFilePath}\" \"{modelPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = projectPath
                };

                _logger.LogInformation($"Executing Python script: {startInfo.FileName} {startInfo.Arguments}");
                _logger.LogInformation($"Working Directory: {startInfo.WorkingDirectory}");

                using var process = Process.Start(startInfo);
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Python script failed with exit code {process.ExitCode}. Error: {error}");
                    throw new Exception($"Analysis failed: {error}");
                }

                _logger.LogInformation($"Python script output: {output}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<AnalysisResult>(output, options);

                if (result == null || result.Status != "success")
                {
                    throw new Exception($"Analysis failed: {result?.Error ?? "Unknown error"}");
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

                // Process detailed analysis results
                if (requestDetailedAnalysis && result.Features != null)
                {
                    // Store numerical features
                    bellPepperImage.MaxValue = Convert.ToDecimal(result.Features.MaxValue);
                    bellPepperImage.MinValue = Convert.ToDecimal(result.Features.MinValue);
                    bellPepperImage.StdValue = Convert.ToDecimal(result.Features.StdValue);
                    bellPepperImage.MeanValue = Convert.ToDecimal(result.Features.MeanValue);
                    bellPepperImage.MedianValue = Convert.ToDecimal(result.Features.MedianValue);

                    // Store processed and analysis images
                    if (!string.IsNullOrEmpty(result.ProcessedImage))
                        bellPepperImage.ProcessedImage = Convert.FromBase64String(result.ProcessedImage);

                    if (!string.IsNullOrEmpty(result.SpectrumR))
                        bellPepperImage.SpectrumR = Convert.FromBase64String(result.SpectrumR);

                    if (!string.IsNullOrEmpty(result.SpectrumG))
                        bellPepperImage.SpectrumG = Convert.FromBase64String(result.SpectrumG);

                    if (!string.IsNullOrEmpty(result.SpectrumB))
                        bellPepperImage.SpectrumB = Convert.FromBase64String(result.SpectrumB);

                    if (!string.IsNullOrEmpty(result.SpectrumCombined))
                        bellPepperImage.SpectrumCombined = Convert.FromBase64String(result.SpectrumCombined);

                    if (!string.IsNullOrEmpty(result.InverseFft))
                        bellPepperImage.InverseFFT = Convert.FromBase64String(result.InverseFft);

                    if (!string.IsNullOrEmpty(result.SobelH1))
                        bellPepperImage.SobelH1 = Convert.FromBase64String(result.SobelH1);

                    if (!string.IsNullOrEmpty(result.SobelH2))
                        bellPepperImage.SobelH2 = Convert.FromBase64String(result.SobelH2);
                }

                // Save to database
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
                // Clean up temporary file
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
        public string ProcessedImage { get; set; }
        public string SpectrumR { get; set; }
        public string SpectrumG { get; set; }
        public string SpectrumB { get; set; }
        public string SpectrumCombined { get; set; }
        public string InverseFft { get; set; }
        public string SobelH1 { get; set; }
        public string SobelH2 { get; set; }
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
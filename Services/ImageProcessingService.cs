using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                    _logger.LogInformation($"Processing features: MaxValue={result.Features.MaxValue}, MinValue={result.Features.MinValue}");

                    // Store numerical features
                    bellPepperImage.MaxValue = Convert.ToDecimal(result.Features.MaxValue);
                    bellPepperImage.MinValue = Convert.ToDecimal(result.Features.MinValue);
                    bellPepperImage.StdValue = Convert.ToDecimal(result.Features.StdValue);
                    bellPepperImage.MeanValue = Convert.ToDecimal(result.Features.MeanValue);
                    bellPepperImage.MedianValue = Convert.ToDecimal(result.Features.MedianValue);

                    _logger.LogInformation($"Stored features: MaxValue={bellPepperImage.MaxValue}, MinValue={bellPepperImage.MinValue}");

                    // Process image data
                    _logger.LogInformation($"ProcessedImage length: {result.ProcessedImage?.Length ?? 0}");
                    _logger.LogInformation($"SpectrumR length: {result.SpectrumR?.Length ?? 0}");

                    try
                    {
                        if (result.ProcessedImage != null)
                        {
                            bellPepperImage.ProcessedImage = Convert.FromBase64String(result.ProcessedImage);
                            _logger.LogInformation("Processed image converted successfully");
                        }

                        if (result.SpectrumR != null)
                        {
                            bellPepperImage.SpectrumR = Convert.FromBase64String(result.SpectrumR);
                            _logger.LogInformation("SpectrumR converted successfully");
                        }

                        if (result.SpectrumG != null)
                        {
                            bellPepperImage.SpectrumG = Convert.FromBase64String(result.SpectrumG);
                            _logger.LogInformation("SpectrumG converted successfully");
                        }

                        if (result.SpectrumB != null)
                        {
                            bellPepperImage.SpectrumB = Convert.FromBase64String(result.SpectrumB);
                            _logger.LogInformation("SpectrumB converted successfully");
                        }

                        if (result.SpectrumCombined != null)
                        {
                            bellPepperImage.SpectrumCombined = Convert.FromBase64String(result.SpectrumCombined);
                            _logger.LogInformation("SpectrumCombined converted successfully");
                        }

                        if (result.InverseFft != null)
                        {
                            bellPepperImage.InverseFFT = Convert.FromBase64String(result.InverseFft);
                            _logger.LogInformation("InverseFft converted successfully");
                        }

                        if (result.SobelH1 != null)
                        {
                            bellPepperImage.SobelH1 = Convert.FromBase64String(result.SobelH1);
                            _logger.LogInformation("SobelH1 converted successfully");
                        }

                        if (result.SobelH2 != null)
                        {
                            bellPepperImage.SobelH2 = Convert.FromBase64String(result.SobelH2);
                            _logger.LogInformation("SobelH2 converted successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error converting base64 images: {ex.Message}");
                        throw;
                    }
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
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("prediction")]
        public string Prediction { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("features")]
        public Features Features { get; set; }

        [JsonPropertyName("processed_image")]
        public string ProcessedImage { get; set; }

        [JsonPropertyName("spectrum_r")]
        public string SpectrumR { get; set; }

        [JsonPropertyName("spectrum_g")]
        public string SpectrumG { get; set; }

        [JsonPropertyName("spectrum_b")]
        public string SpectrumB { get; set; }

        [JsonPropertyName("spectrum_combined")]
        public string SpectrumCombined { get; set; }

        [JsonPropertyName("inverse_fft")]
        public string InverseFft { get; set; }

        [JsonPropertyName("sobel_h1")]
        public string SobelH1 { get; set; }

        [JsonPropertyName("sobel_h2")]
        public string SobelH2 { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }


    public class Features
    {
        [JsonPropertyName("max_value")]
        public double MaxValue { get; set; }

        [JsonPropertyName("min_value")]
        public double MinValue { get; set; }

        [JsonPropertyName("std_value")]
        public double StdValue { get; set; }

        [JsonPropertyName("mean_value")]
        public double MeanValue { get; set; }

        [JsonPropertyName("median_value")]
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
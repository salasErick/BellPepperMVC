using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BellPepperMVC.Services;
using BellPepperMVC.Models;
using BellPepperMVC.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;

namespace BellPepperMVC.Controllers
{
    [Authorize]
    public class AnalysisController : Controller
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AnalysisController> _logger;

        public AnalysisController(
            IImageProcessingService imageProcessingService,
            UserManager<ApplicationUser> userManager,
            ILogger<AnalysisController> logger)
        {
            _imageProcessingService = imageProcessingService;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var viewModel = new AnalysisViewModel
            {
                RecentAnalyses = (await _imageProcessingService.GetUserAnalysesAsync(user.Id, 5)).ToList(),
                MaturityLevelDistribution = await _imageProcessingService.GetMaturityDistributionAsync(user.Id)
            };
            return View(viewModel);
        }

        public IActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, bool requestDetailedAnalysis = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                var user = await _userManager.GetUserAsync(User);
                var result = await _imageProcessingService.ProcessImageAsync(file, user.Id, requestDetailedAnalysis);
                return RedirectToAction("Details", new { id = result.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image");
                ModelState.AddModelError("", "Error processing image: " + ex.Message);
                return View();
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var analysis = await _imageProcessingService.GetAnalysisAsync(id);
            if (analysis == null)
                return NotFound();

            if (analysis.UserId != (await _userManager.GetUserAsync(User)).Id)
                return Forbid();

            return View(analysis);
        }

        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);
            var analyses = await _imageProcessingService.GetUserAnalysesAsync(user.Id);
            return View(analyses);
        }

        [HttpGet]
        public IActionResult BatchUpload()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> BatchUpload(List<IFormFile> files, bool requestDetailedAnalysis = false)
        {
            if (files == null || !files.Any())
                return BadRequest("No files uploaded");

            var user = await _userManager.GetUserAsync(User);
            var results = new List<BatchUploadResult>();

            foreach (var file in files)
            {
                try
                {
                    var result = await _imageProcessingService.ProcessImageAsync(file, user.Id, requestDetailedAnalysis);
                    results.Add(new BatchUploadResult
                    {
                        FileName = file.FileName,
                        Success = true,
                        AnalysisId = result.Id
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing file {file.FileName}");
                    results.Add(new BatchUploadResult
                    {
                        FileName = file.FileName,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            return View("BatchResults", results);
        }

        [HttpGet]
        public async Task<IActionResult> GetImage(int id, string type = "original")
        {
            var analysis = await _imageProcessingService.GetAnalysisAsync(id);
            if (analysis == null)
                return NotFound();

            if (analysis.UserId != (await _userManager.GetUserAsync(User)).Id)
                return Forbid();

            byte[] imageData = type switch
            {
                "original" => analysis.Image,
                "processed" => analysis.ProcessedImage,
                "spectrumR" => analysis.SpectrumR,
                "spectrumG" => analysis.SpectrumG,
                "spectrumB" => analysis.SpectrumB,
                "spectrumCombined" => analysis.SpectrumCombined,
                "inverseFFT" => analysis.InverseFFT,
                "sobelH1" => analysis.SobelH1,
                "sobelH2" => analysis.SobelH2,
                _ => analysis.Image
            };

            return File(imageData, analysis.ContentType);
        }
    }

    public class BatchUploadResult
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public int? AnalysisId { get; set; }
        public string Error { get; set; }
    }
}
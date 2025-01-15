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
            var recentAnalyses = await _imageProcessingService.GetUserAnalysesAsync(user.Id, 5);

            var viewModel = new DashboardViewModel
            {
                RecentAnalyses = recentAnalyses.ToList(),
                TotalAnalyses = recentAnalyses.Count(),
                UserName = $"{user.FirstName} {user.LastName}"
            };

            return View(viewModel);
        }

        public IActionResult Upload()
        {
            return View(new ImageUploadViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Upload([FromForm] ImageUploadViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.File == null || model.File.Length == 0)
            {
                ModelState.AddModelError("File", "Please select a file to upload");
                return View(model);
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                var result = await _imageProcessingService.ProcessImageAsync(
                    model.File,
                    user.Id,
                    model.RequestDetailedAnalysis
                );

                TempData["SuccessMessage"] = "Image processed successfully!";
                return RedirectToAction("Details", new { id = result.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image upload");
                ModelState.AddModelError("", "Error processing image: " + ex.Message);
                return View(model);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var analysis = await _imageProcessingService.GetAnalysisAsync(id);
            if (analysis == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (analysis.UserId != user.Id)
                return Forbid();

            var viewModel = new AnalysisDetailsViewModel
            {
                Id = analysis.Id,
                FileName = analysis.FileName,
                UploadDate = analysis.UploadDate,
                PredictedMaturityLevel = analysis.PredictedMaturityLevel,
                PredictionConfidence = analysis.PredictionConfidence,
                HasDetailedAnalysis = analysis.HasDetailedAnalysis
            };

            if (analysis.HasDetailedAnalysis)
            {
                viewModel.DetailedFeatures = new DetailedFeatures
                {
                    MaxValue = analysis.MaxValue ?? 0,  // Provide default value if null
                    MinValue = analysis.MinValue ?? 0,
                    StdValue = analysis.StdValue ?? 0,
                    MeanValue = analysis.MeanValue ?? 0,
                    MedianValue = analysis.MedianValue ?? 0
                };

                // Also make sure to set the flags for available images
                viewModel.HasSpectrumImages = analysis.SpectrumR != null &&
                                             analysis.SpectrumG != null &&
                                             analysis.SpectrumB != null &&
                                             analysis.SpectrumCombined != null;
                viewModel.HasSobelFilters = analysis.SobelH1 != null && analysis.SobelH2 != null;
                viewModel.HasInverseFFT = analysis.InverseFFT != null;
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetImage(int id, string type = "original")
        {
            var analysis = await _imageProcessingService.GetAnalysisAsync(id);
            if (analysis == null)
                return NotFound();

            if (analysis.UserId != (await _userManager.GetUserAsync(User)).Id)
                return Forbid();

            byte[] imageData;
            string contentType = analysis.ContentType;

            switch (type.ToLower())
            {
                case "original":
                    imageData = analysis.Image;
                    break;
                case "processed":
                    imageData = analysis.ProcessedImage ?? analysis.Image;
                    break;
                case "spectrumr":
                    imageData = analysis.SpectrumR;
                    break;
                case "spectrumg":
                    imageData = analysis.SpectrumG;
                    break;
                case "spectrumb":
                    imageData = analysis.SpectrumB;
                    break;
                case "spectrumcombined":
                    imageData = analysis.SpectrumCombined;
                    break;
                case "inversefft":
                    imageData = analysis.InverseFFT;
                    break;
                case "sobelh1":
                    imageData = analysis.SobelH1;
                    break;
                case "sobelh2":
                    imageData = analysis.SobelH2;
                    break;
                default:
                    imageData = analysis.Image;
                    break;
            }

            if (imageData == null)
                return NotFound();

            return File(imageData, contentType);
        }

        public async Task<IActionResult> History(int page = 1, int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            var analyses = await _imageProcessingService.GetUserAnalysesAsync(user.Id, pageSize);

            var viewModel = new AnalysisHistoryViewModel
            {
                Analyses = analyses,
                CurrentPage = page,
                PageSize = pageSize
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult BatchUpload()
        {
            return View(new BatchUploadViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> BatchUpload([FromForm] BatchUploadViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.Files == null || !model.Files.Any())
            {
                ModelState.AddModelError("", "Please select at least one file to upload");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var results = new List<BatchUploadResult>();

            foreach (var file in model.Files)
            {
                try
                {
                    var result = await _imageProcessingService.ProcessImageAsync(
                        file,
                        user.Id,
                        model.RequestDetailedAnalysis
                    );

                    results.Add(new BatchUploadResult
                    {
                        FileName = file.FileName,
                        Success = true,
                        AnalysisId = result.Id,
                        PredictedMaturityLevel = result.PredictedMaturityLevel,
                        PredictionConfidence = result.PredictionConfidence
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

            return View("BatchResults", new BatchResultsViewModel { Results = results });
        }
    }

    public class ImageUploadViewModel
    {
        public IFormFile File { get; set; }
        public bool RequestDetailedAnalysis { get; set; }
    }

    public class DashboardViewModel
    {
        public List<BellPepperImage> RecentAnalyses { get; set; }
        public int TotalAnalyses { get; set; }
        public string UserName { get; set; }
    }

    public class DetailedFeatures
    {
        public decimal MaxValue { get; set; }
        public decimal MinValue { get; set; }
        public decimal StdValue { get; set; }
        public decimal MeanValue { get; set; }
        public decimal MedianValue { get; set; }
    }

    public class AnalysisDetailsViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public DateTime UploadDate { get; set; }
        public string PredictedMaturityLevel { get; set; }
        public decimal PredictionConfidence { get; set; }
        public bool HasDetailedAnalysis { get; set; }
        public DetailedFeatures DetailedFeatures { get; set; }

        // Image analysis visualizations
        public bool HasSpectrumImages { get; set; }
        public bool HasSobelFilters { get; set; }
        public bool HasInverseFFT { get; set; }

        // URLs for different image types
        public string OriginalImageUrl => $"/Analysis/GetImage/{Id}?type=original";
        public string ProcessedImageUrl => $"/Analysis/GetImage/{Id}?type=processed";
        public string SpectrumRUrl => $"/Analysis/GetImage/{Id}?type=spectrumR";
        public string SpectrumGUrl => $"/Analysis/GetImage/{Id}?type=spectrumG";
        public string SpectrumBUrl => $"/Analysis/GetImage/{Id}?type=spectrumB";
        public string SpectrumCombinedUrl => $"/Analysis/GetImage/{Id}?type=spectrumCombined";
        public string InverseFFTUrl => $"/Analysis/GetImage/{Id}?type=inverseFFT";
        public string SobelH1Url => $"/Analysis/GetImage/{Id}?type=sobelH1";
        public string SobelH2Url => $"/Analysis/GetImage/{Id}?type=sobelH2";
    }

    public class AnalysisHistoryViewModel
    {
        public IEnumerable<BellPepperImage> Analyses { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }

    public class BatchUploadViewModel
    {
        public List<IFormFile> Files { get; set; }
        public bool RequestDetailedAnalysis { get; set; }
    }

    public class BatchUploadResult
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public int? AnalysisId { get; set; }
        public string PredictedMaturityLevel { get; set; }
        public decimal? PredictionConfidence { get; set; }
        public string Error { get; set; }
    }

    public class BatchResultsViewModel
    {
        public List<BatchUploadResult> Results { get; set; }
    }
}
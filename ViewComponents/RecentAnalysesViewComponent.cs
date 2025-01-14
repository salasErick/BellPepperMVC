using Microsoft.AspNetCore.Mvc;
using BellPepperMVC.Services;
using BellPepperMVC.Areas.Identity.Data;
using BellPepperMVC.Models;
using Microsoft.AspNetCore.Identity;

namespace BellPepperMVC.ViewComponents
{
    public class RecentAnalysesViewComponent : ViewComponent
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RecentAnalysesViewComponent(
            IImageProcessingService imageProcessingService,
            UserManager<ApplicationUser> userManager)
        {
            _imageProcessingService = imageProcessingService;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
                return View(new List<BellPepperImage>());

            var recentAnalyses = await _imageProcessingService.GetUserAnalysesAsync(user.Id, 5);
            return View(recentAnalyses);
        }
    }
}
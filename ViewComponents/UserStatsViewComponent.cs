using Microsoft.AspNetCore.Mvc;
using BellPepperMVC.Services;
using BellPepperMVC.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;

namespace BellPepperMVC.ViewComponents
{
    public class UserStatsViewComponent : ViewComponent
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserStatsViewComponent(
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
                return View(new Dictionary<string, int>());

            var distribution = await _imageProcessingService.GetMaturityDistributionAsync(user.Id);
            return View(distribution);
        }
    }
}
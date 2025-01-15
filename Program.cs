using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BellPepperMVC.Data;
using BellPepperMVC.Areas.Identity.Data;
using BellPepperMVC.Services;
var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("BellPepperMVCContextConnection") ?? throw new InvalidOperationException("Connection string 'BellPepperMVCContextConnection' not found.");

builder.Services.AddDbContext<BellPepperMVCContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false).AddEntityFrameworkStores<BellPepperMVCContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
var app = builder.Build();

// Create the directory where temp uploads will reside
var tempUploadPath = builder.Configuration["PythonSettings:TempUploadPath"];
if (!string.IsNullOrEmpty(tempUploadPath))
{
    var fullPath = Path.GetFullPath(tempUploadPath);
    Directory.CreateDirectory(fullPath);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.Run();

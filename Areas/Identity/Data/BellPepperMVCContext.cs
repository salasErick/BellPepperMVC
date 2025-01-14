using BellPepperMVC.Areas.Identity.Data;
using BellPepperMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BellPepperMVC.Data;

public class BellPepperMVCContext : IdentityDbContext<ApplicationUser>
{
    public BellPepperMVCContext(DbContextOptions<BellPepperMVCContext> options)
        : base(options)
    {
    }

    public DbSet<BellPepperImage> BellPepperImages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Customize the ASP.NET Identity model and override the defaults if needed.
        // For example, you can rename the ASP.NET Identity table names and more.
        // Add your customizations after calling base.OnModelCreating(builder);

        builder.Entity<BellPepperImage>()
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using kgs_api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace kgs_api.Data
{
    public class KgsDbContext : IdentityDbContext<ApplicationUser>
    {
        public KgsDbContext(DbContextOptions<KgsDbContext> options) : base(options)
        {
        }
        
        public DbSet<Appointment> Appointments { get; set; }

    }
}

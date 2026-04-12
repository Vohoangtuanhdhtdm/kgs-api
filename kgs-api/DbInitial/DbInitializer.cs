using kgs_api.Data;
using kgs_api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace kgs_api.DbInitial
{
    public class DbInitializer : IDbInitial
    {
        private readonly KgsDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public DbInitializer(KgsDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }
        public async Task Initialize()
        {
            try
            {
                if (_context.Database.GetPendingMigrations().Count() > 0)
                {
                    _context.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying migrations: {ex.Message}");
            }

            
            if (await _roleManager.RoleExistsAsync(Utility.Helper.Admin)) return;

          
            await _roleManager.CreateAsync(new IdentityRole(Utility.Helper.Admin));


            await _roleManager.CreateAsync(new IdentityRole(Utility.Helper.Doctor));
            await _roleManager.CreateAsync(new IdentityRole(Utility.Helper.Patient));

      
            var adminUser = new ApplicationUser
            {
                UserName = "admin@gmail.com",
                Email = "admin@gmail.com",
                Name = "Admin sprak",
                EmailConfirmed = true
            };

          
            var result = await _userManager.CreateAsync(adminUser, "Admin123!");

            
            if (result.Succeeded)
            {
              
                var userInDb = await _userManager.FindByEmailAsync("admin@gmail.com");
                if (userInDb != null)
                {
                    await _userManager.AddToRoleAsync(userInDb, Utility.Helper.Admin);
                }
            }
        }
    }
}

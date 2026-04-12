using Microsoft.AspNetCore.Identity;

namespace kgs_api.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; } = string.Empty;
    }
}

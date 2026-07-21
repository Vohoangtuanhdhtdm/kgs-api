using Microsoft.AspNetCore.Identity;

namespace kgs_api.Domain.Entity
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

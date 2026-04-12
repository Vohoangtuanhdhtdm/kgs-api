using kgs_api.Models;

namespace kgs_api.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(ApplicationUser user);
    }
}

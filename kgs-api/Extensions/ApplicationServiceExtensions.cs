using kgs_api.Data;
using kgs_api.DbInitial;
using kgs_api.Interfaces;
using kgs_api.Models;
using kgs_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace kgs_api.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("PostgresDb") ?? throw new InvalidOperationException("Connection string 'PostgresDb' not found.");

            // Database
            services.AddDbContext<KgsDbContext>(options => options.UseNpgsql(connectionString));

            // Token Provider Tool
            services.AddDataProtection();

            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<KgsDbContext>()
                .AddDefaultTokenProviders();

            services.AddScoped<IDbInitial, DbInitializer>();

            services.AddControllers();
            services.AddHttpClient();
            services.AddCors();

            // Services
            services.AddScoped<ITokenService, TokenService>();

            // Ensure the JWT token is valid
            var secretKey = config["AppSettings:TokenKey"];
            if (string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("TokenKey is not configured.");
            }

            // Configure JWT authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });

            return services;
        }
    }
}

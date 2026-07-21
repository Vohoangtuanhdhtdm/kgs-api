using Hangfire;
using Hangfire.PostgreSql;
using kgs_api.Data;
using kgs_api.Domain.Entity;
using kgs_api.Interfaces;
using kgs_api.Repositories;
using kgs_api.Services;
using kgs_api.Storage;
using kgs_api.Utility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NetTopologySuite;
using System.Text;
using static kgs_api.Common.Common;

namespace kgs_api.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("PostgresDb") ?? throw new InvalidOperationException("Connection string 'PostgresDb' not found.");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString, o => o.UseNetTopologySuite()));

            services.AddHttpContextAccessor();

            services.AddSingleton(NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326));

            // Database
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

            // Token Provider Tool
            services.AddDataProtection();

            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            //Đăng ký DI
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            services.Configure<CloudinarySettings>(config.GetSection("CloudinarySettings"));
            services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();


            services.AddScoped<IAssetService, AssetService>();
            services.AddScoped<IAssetMediaService, AssetMediaService>();
            services.AddScoped<IAssetDocumentService, AssetDocumentService>();
            services.AddScoped<IAssetUnitService, AssetUnitService>();
            services.AddScoped<IContactPartyService, ContactPartyService>();
            services.AddScoped<ILeaseContractService, LeaseContractService>();
            services.AddScoped<ICashFlowService, CashFlowService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<IReminderService, ReminderService>();
            services.AddScoped<IEquipmentService, EquipmentService>();
            services.AddScoped<IMaintenanceService, MaintenanceService>();
            services.AddScoped<IUsagePeriodService, UsagePeriodService>();
            services.AddScoped<ISaleListingService, SaleListingService>();
            services.AddScoped<INotificationSender, LoggingNotificationSender>(); // thay bằng FCM/APNs sau

           
            //___
            services.AddControllers();
            services.AddHttpClient();
            services.AddCors();

            // Services
            services.AddScoped<ITokenService, TokenService>();

            // Đọc cấu hình từ appsettings map vào class CloudinarySettings
            services.Configure<CloudinarySettings>(config.GetSection("CloudinarySettings"));

            // Đăng ký PhotoService
            services.AddScoped<IPhotoService, PhotoService>();

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

            services.AddHangfire(configuration =>
                configuration
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

            // Thêm Hangfire Server
            services.AddHangfireServer();

            // Đăng ký các background job
            services.AddScoped<ReminderProcessingJob>();
            services.AddScoped<FileCleanupJob>();

            return services;
        }
    }
}

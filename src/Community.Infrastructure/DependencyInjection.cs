using Community.Application.Interfaces;
using Community.Infrastructure.Base;
using Community.Infrastructure.Interceptors;
using Community.Infrastructure.Persistence;
using Community.Infrastructure.Services;
using Community.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Community.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddHttpContextAccessor();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddDbContext<CommunityDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<CommunityDbContext>());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<Community.Domain.Interfaces.IUnitOfWork>(sp => sp.GetRequiredService<IUnitOfWork>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Kiểm duyệt nội dung: stub hiện tại, sẽ thay bằng QualityService HttpClient ở phiên sau
        services.AddScoped<IContentModerationService, StubContentModerationService>();

        // Quét tự động bằng Scrutor để đăng ký các class kết thúc bằng Repository hoặc Service
        services.Scan(scan => scan
            .FromAssemblies(typeof(CommunityDbContext).Assembly)
            .AddClasses(classes => classes.Where(type =>
                type.Name.EndsWith("Repository", StringComparison.Ordinal) ||
                type.Name.EndsWith("Service", StringComparison.Ordinal)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // ===== JWT Authentication (khớp contract IdentityService) =====
        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                  ?? new JwtSettings();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    // Giữ nguyên claim "sub" (JwtSecurityTokenHandler mặc định map nó sang NameIdentifier)
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        return services;
    }
}

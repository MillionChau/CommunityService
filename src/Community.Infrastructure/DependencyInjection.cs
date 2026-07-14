using Community.Application.Interfaces;
using Community.Infrastructure.Base;
using Community.Infrastructure.Persistence;
using Community.Infrastructure.Interceptors;
using Community.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Community.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
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
        // Quét tự động bằng Scrutor để đăng ký các class kết thúc bằng Repository hoặc Service
        services.Scan(scan => scan
            .FromAssemblies(typeof(CommunityDbContext).Assembly)
            .AddClasses(classes => classes.Where(type =>
                type.Name.EndsWith("Repository", StringComparison.Ordinal) ||
                type.Name.EndsWith("Service", StringComparison.Ordinal)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        return services;
    }
}

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
namespace Community.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
        
            services.AddMediatR(cfg => 
            {
                cfg.RegisterServicesFromAssembly(assembly);
                cfg.AddOpenBehavior(typeof(Common.Behaviors.ValidationBehavior<,>));
            });
            services.AddValidatorsFromAssembly(assembly);
            services.AddAutoMapper(assembly);
            return services;
        }
    }
}
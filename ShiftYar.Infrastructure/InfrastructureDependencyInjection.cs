using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using ShiftYar.Infrastructure.Persistence.Repositories;
using ShiftYar.Infrastructure.Persistence.Repositories.HospitalModel;
using ShiftYar.Infrastructure.Services.Security;
using ShiftYar.Application.Interfaces.HospitalModel;
using ShiftYar.Application.Interfaces.Security;
using ShiftYar.Application.Interfaces.CalendarSeeder;
using ShiftYar.Application.Interfaces.IFileSystem;
using ShiftYar.Infrastructure.Services.FileSystem;
using ShiftYar.Application.Interfaces.SmsModel;

namespace ShiftYar.Infrastructure
{
    public static class InfrastructureDependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Database Context
            services.AddDbContext(configuration);

            // Repositories
            services.AddRepositories();

            // Services
            services.AddInfrastructureServices();

            return services;
        }

        //private static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
        //{
        //    //services.AddDbContext<ShiftYarDbContext>(options =>
        //    //    options.UseSqlServer(configuration.GetConnectionString("ShiftYarDbConnection")));

        //    services.AddDbContext<ShiftYarDbContext>(options =>
        //        options.UseSqlServer(
        //            configuration.GetConnectionString("DefaultConnection"),
        //            sqlOptions => sqlOptions.CommandTimeout(180)  // زمان بر حسب ثانیه، مثلاً 180 ثانیه = 3 دقیقه
        //    ));

        //}

        private static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ShiftYarDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions =>
                    {
                        sqlOptions.CommandTimeout(180); // 3 دقیقه

                        // فعال‌سازی مجدد تلاش‌ها (RetryOnFailure)
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5, // تعداد دفعات تلاش مجدد
                            maxRetryDelay: TimeSpan.FromSeconds(10), // فاصله تلاش مجدد
                            errorNumbersToAdd: null // اگر شماره خطای خاصی مدنظر باشه
                        );
                    }));
        }

        private static void AddRepositories(this IServiceCollection services)
        {
            services.AddScoped(typeof(IEfRepository<>), typeof(EfRepository<>));
            services.AddScoped<ISmsTemplateRepository, SmsTemplateRepository>();
        }

        private static void AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddScoped<IHospitalService, HospitalService>();
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddScoped<IAuthService, AuthService>();
        }
    }
}
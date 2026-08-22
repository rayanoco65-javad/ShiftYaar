using Microsoft.Extensions.DependencyInjection;
using ShiftYar.Application.Common.Mappings;
using ShiftYar.Application.Features.DepartmentModel.Services;
using ShiftYar.Application.Features.PermissionModel.Services;
using ShiftYar.Application.Features.RoleModel.Services;
using ShiftYar.Application.Features.UserModel.Services;
using ShiftYar.Application.Interfaces.DepartmentModel;
using ShiftYar.Application.Interfaces.RoleModel;
using ShiftYar.Application.Interfaces.Security;
using ShiftYar.Application.Interfaces.UserModel;
using ShiftYar.Application.Interfaces.PermissionModel;
using System.Security;
using ShiftYar.Application.Interfaces.RolePermissionModel;
using ShiftYar.Application.Features.RolePermissionModel.Services;
using ShiftYar.Application.Interfaces.SpecialtyModel;
using ShiftYar.Application.Features.SpecialtyModel.Services;
using ShiftYar.Application.Interfaces.ShiftRequiredSpecialtyModel;
using ShiftYar.Application.Features.ShiftRequiredSpecialtyModel.Services;
using ShiftYar.Application.Interfaces.ShiftModel;
using ShiftYar.Application.Features.ShiftModel.Services;
using ShiftYar.Application.Features.ShiftModel.Rescheduling;
using ShiftYar.Application.Interfaces.FileUploaderInterface;
using ShiftYar.Application.Features.FileUploader.Services;
using ShiftYar.Application.Features.CalendarSeeder.Services;
using ShiftYar.Application.Interfaces.CalendarSeeder;
using ShiftYar.Application.Interfaces.AddressModel;
using ShiftYar.Application.Features.AddressModel.Services;
using ShiftYar.Application.Interfaces.ShiftRequestModel;
using ShiftYar.Application.Features.ShiftRequestModel.Services;
using ShiftYar.Application.Features.SmsModel;
using ShiftYar.Application.Interfaces.SmsModel;
using ShiftYar.Application.Interfaces;
using ShiftYar.Application.Features.ShiftExchangeModel.Services;
using ShiftYar.Application.Features.ShiftModel.PerformanceComparison;
using ShiftYar.Application.Interfaces.Settings;
using ShiftYar.Application.Features.Settings.Services;
using ShiftYar.Application.Interfaces.ProductivityModel;
using ShiftYar.Application.Features.ProductivityModel.Services;
using ShiftYar.Application.Features.ShiftModel.Jobs;

namespace ShiftYar.Application
{
    public static class ApplicationDependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // AutoMapper
            services.AddAutoMapper(typeof(UserProfile).Assembly);

            // Services
            services.AddApplicationServices();

            return services;
        }

        private static void AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserMonthlyNightQuotaService, UserMonthlyNightQuotaService>();
            services.AddScoped<IUserMonthlyDayShiftQuotaService, UserMonthlyDayShiftQuotaService>();
            services.AddScoped<IUserMonthlyComboShiftQuotaService, UserMonthlyComboShiftQuotaService>();
            services.AddScoped<IDepartmentService, DepartmentService>();
            services.AddScoped<IDepartmentNameService, DepartmentNameService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IRolePermissionService, RolePermissionService>();
            services.AddScoped<ISpecialtyService, SpecialtyService>();
            services.AddScoped<ISpecialtyNameService, SpecialtyNameService>();
            services.AddScoped<IShiftRequiredSpecialtyService, ShiftRequiredSpecialtyService>();
            services.AddScoped<IShiftService, ShiftService>();
            services.AddScoped<IFileUploader, FileUploaderService>();
            services.AddScoped<ICalendarSeederService, CalendarSeederService>();
            services.AddScoped<IProvinceService, ProvinceService>();
            services.AddScoped<ICityService, CityService>();
            services.AddScoped<IShiftRequestService, ShiftRequestService>();
            services.AddScoped<ISmsTemplateService, SmsTemplateService>();
            services.AddScoped<ISmsService, SmsService>();
            services.AddScoped<IShiftSchedulingService, ShiftSchedulingService>();
            services.AddScoped<IEmergencyReschedulingService, EmergencyReschedulingService>();
            services.AddScoped<AlgorithmPerformanceComparator>(); // ابزار مقایسه عملکرد الگوریتم‌ها
            services.AddScoped<IDepartmentSchedulingSettingsService, DepartmentSchedulingSettingsService>();
            services.AddScoped<IShiftExchangeService, ShiftExchangeService>();
            services.AddScoped<IAlgorithmSettingsService, AlgorithmSettingsService>();
            services.AddScoped<IWorkingHoursCalculator, WorkingHoursCalculator>();

            // اجرای پس‌زمینهٔ زمان‌بندی (برای جلوگیری از 502 ناشی از حل طولانی/سنگین در مسیر درخواست)
            services.AddSingleton<ISchedulingJobStore, SchedulingJobStore>();
            services.AddSingleton<ISchedulingJobQueue, SchedulingJobQueue>();
            services.AddHostedService<SchedulingBackgroundService>();
        }
    }
}
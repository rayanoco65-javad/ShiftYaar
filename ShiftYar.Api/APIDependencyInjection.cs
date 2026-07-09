using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShiftYar.Api.Filters;
using ShiftYar.Application.Common.Constants;
using System.Text;

namespace ShiftYar.API
{
    public static class APIDependencyInjection
    {
        public static IServiceCollection AddAPI(this IServiceCollection services, IConfiguration configuration)
        {
            // Controllers with Filters
            services.AddControllers(options =>
            {
                ///این دوتا رو به صورت گلوبال در Program.cs رجیستر کردیم 
                ///یعنی روی همه‌ی کنترلرها و اکشن‌ها به طور پیش‌فرض اعمال می‌شن. نیازی نیست جایی جداگانه بنویسی.
                options.Filters.Add<GlobalExceptionFilter>();
                options.Filters.Add<ValidateModelFilter>();
            })
            .AddJsonOptions(opt =>
            {
                opt.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                opt.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                    });
            });

            // Swagger
            services.AddSwagger(configuration);

            // Authentication
            services.AddAuthentication(configuration);

            // Filters
            services.AddScoped<RequestLoggingFilter>();
            services.AddScoped<GlobalExceptionFilter>();
            services.AddScoped<ValidateModelFilter>();

            return services;
        }

        private static void AddSwagger(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ShiftYar API",
                    Version = "v1",
                    Description = "API for ShiftYar application"
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
        }

        private static void AddAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = configuration["JwtConfig:Issuer"],
                        ValidAudience = configuration["JwtConfig:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtConfig:Key"])),
                        //// تنظیم RoleClaimType برای پشتیبانی از نقش‌ها
                        //RoleClaimType = System.Security.Claims.ClaimTypes.Role
                        // تنظیم RoleClaimType و NameClaimType برای پشتیبانی از claim types استاندارد JWT
                        RoleClaimType = JwtClaimTypes.Role,
                        NameClaimType = JwtClaimTypes.Name
                    };
                });
            services.AddAuthorization();
        }
    }
}
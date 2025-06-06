using Bookstore.Mobile.Handlers;
using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Interfaces.Services;
using Bookstore.Mobile.Models;
using Bookstore.Mobile.Services;
using Bookstore.Mobile.ViewModels;
using CommunityToolkit.Maui;
using FluentValidation;
using Microcharts.Maui;
using Microsoft.Extensions.Logging;
using Refit;
using System.Text.Json;

namespace Bookstore.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit(options => options.SetShouldEnableSnackbarOnWindows(true))
                .UseMicrocharts()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialSymbolsRounded");
                    fonts.AddFont("Inter-Bold.otf", "InterBold");
                    fonts.AddFont("Inter-Regular.otf", "InterRegular");
                });

            // ----- Đăng ký Dependency Injection -----

            // Logging
#if DEBUG
            builder.Logging.AddDebug();
#endif
            var logger = builder.Services.BuildServiceProvider().GetService<ILogger<MauiApp>>();

            // Lấy địa chỉ API gốc (ưu tiên HTTPS)
            string apiBaseAddress = builder.Configuration["ApiSettings:BaseAddress"] ?? "https://localhost:7264/api";
            string httpApiBaseAddress = builder.Configuration["ApiSettings:HttpBaseAddress"] ?? "http://localhost:5244/api";

            // --- XỬ LÝ KẾT NỐI CHO ANDROID DEBUG ---
#if DEBUG && ANDROID 
            // Emulator dùng 10.0.2.2 để trỏ về localhost của máy host
            // và phải dùng HTTP nếu không cấu hình HTTPS phức tạp
            logger?.LogWarning("Android DEBUG detected. Using HTTP address for API connection.");
            apiBaseAddress = httpApiBaseAddress.Replace("http://localhost", "http://10.0.2.2");
            logger?.LogInformation("API Base Address set to: {ApiBaseAddress}", apiBaseAddress);

            // Đăng ký Refit clients với địa chỉ HTTP đã sửa đổi
            ConfigureDefaultRefitClients(builder.Services, apiBaseAddress);

#else
            // Cấu hình cho các platform khác hoặc Release build (dùng HTTPS mặc định)
            logger?.LogInformation("Using default HTTPS address for API connection: {ApiBaseAddress}", apiBaseAddress);
            ConfigureDefaultRefitClients(builder.Services, apiBaseAddress);
#endif

            // Singleton Services
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<AppShellViewModel>();
            builder.Services.AddSingleton<AppShell>();

            // Đăng ký Handler là Transient
            builder.Services.AddTransient<AuthHeaderHandler>();
            //Đăng ký Validators
            builder.Services.AddValidatorsFromAssemblyContaining<LoginViewModelValidator>();

            // ----- Auto-register ViewModels & Views (Transient) -----
            RegisterViewModelsAndViews(builder.Services);

            return builder.Build();
        }

        // Auto-register all ViewModels and Views as transient
        private static void RegisterViewModelsAndViews(IServiceCollection services)
        {
            var assembly = typeof(MauiProgram).Assembly;
            var types = assembly.GetTypes();

            // Register ViewModels
            foreach (var type in types.Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("ViewModel")))
            {
                services.AddTransient(type);
            }

            // Register Views (Pages)
            foreach (var type in types.Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Page")))
            {
                services.AddTransient(type);
            }
        }

        // Helper đăng ký Refit client
        private static void ConfigureDefaultRefitClients(IServiceCollection services, string apiBaseAddress)
        {
            var refitSettings = new RefitSettings(new SystemTextJsonContentSerializer(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }));
            var baseUri = new Uri(apiBaseAddress);

            // --- Client KHÔNG cần Auth Header ---
            services.AddRefitClient<IAuthApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri);

            // --- Clients CÓ THỂ cần hoặc KHÔNG cần Auth Header (gắn sẵn handler cho an toàn) ---
            // Public endpoints (GetBooks, GetCategories) sẽ không bị ảnh hưởng nếu không có token
            services.AddRefitClient<IBooksApi>(refitSettings)
                   .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                   .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<ICategoriesApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IDashboardApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri);

            services.AddRefitClient<IReviewApi>(refitSettings)
                   .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                   .AddHttpMessageHandler<AuthHeaderHandler>();

            // --- Clients CHẮC CHẮN cần Auth Header ---
            services.AddRefitClient<IWishlistApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<ICartApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IAddressApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IOrderApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            // --- Clients cho Admin/Staff (Luôn cần Auth Header) ---
            services.AddRefitClient<IAdminDashboardApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IAuthorApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IAdminReportApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<ISupplierApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IStockReceiptApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IInventoryApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseAddress))
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IAdminPromotionApi>(refitSettings)
                    .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            services.AddRefitClient<IAdminUserApi>(refitSettings)
                   .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                   .AddHttpMessageHandler<AuthHeaderHandler>();
        }
    }

}
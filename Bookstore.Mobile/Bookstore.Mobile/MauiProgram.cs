using Bookstore.Mobile.Handlers;
using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Interfaces.Services;
using Bookstore.Mobile.Models;
using Bookstore.Mobile.Services;
using Bookstore.Mobile.ViewModels;
using CommunityToolkit.Maui;
using FluentValidation;
using Microcharts.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refit;
using System.Text.Json;

namespace Bookstore.Mobile
{
    public class ApiConfiguration
    {
        public string BaseAddress { get; }
        public string HttpBaseAddress { get; }

        public ApiConfiguration(IConfiguration configuration)
        {
            BaseAddress = configuration["ApiSettings:BaseAddress"] ?? "https://localhost:7264/api";
            HttpBaseAddress = configuration["ApiSettings:HttpBaseAddress"] ?? "http://localhost:5244/api";
        }

        public string GetBaseAddressForPlatform(bool isAndroidDebug)
        {
            if (isAndroidDebug)
            {
                return HttpBaseAddress.Replace("http://localhost", "http://10.0.2.2");
            }
            return BaseAddress;
        }
    }

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

            // Configure logging
            ConfigureLogging(builder);

            // Configure API clients
            ConfigureApiClients(builder);

            // Register services
            RegisterServices(builder.Services);

            return builder.Build();
        }

        private static void ConfigureLogging(MauiAppBuilder builder)
        {
#if DEBUG
            builder.Logging.AddDebug();
#endif
        }

        private static void ConfigureApiClients(MauiAppBuilder builder)
        {
            var logger = builder.Services.BuildServiceProvider().GetService<ILogger<MauiApp>>();
            var apiConfig = new ApiConfiguration(builder.Configuration);

#if DEBUG && ANDROID
            logger?.LogWarning("Android DEBUG detected. Using HTTP address for API connection.");
            var apiBaseAddress = apiConfig.GetBaseAddressForPlatform(true);
            logger?.LogInformation("API Base Address set to: {ApiBaseAddress}", apiBaseAddress);
#else
            var apiBaseAddress = apiConfig.GetBaseAddressForPlatform(false);
            logger?.LogInformation("Using default HTTPS address for API connection: {ApiBaseAddress}", apiBaseAddress);
#endif

            ConfigureRefitClients(builder.Services, apiBaseAddress);
        }

        private static void RegisterServices(IServiceCollection services)
        {
            // Singleton Services
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<AppShellViewModel>();
            services.AddSingleton<AppShell>();

            // Transient Services
            services.AddTransient<AuthHeaderHandler>();
            services.AddValidatorsFromAssemblyContaining<LoginViewModelValidator>();

            // Auto-register ViewModels & Views
            RegisterViewModelsAndViews(services);
        }

        private static void ConfigureRefitClients(IServiceCollection services, string apiBaseAddress)
        {
            var refitSettings = new RefitSettings(new SystemTextJsonContentSerializer(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }));
            var baseUri = new Uri(apiBaseAddress);

            // Group API clients by authentication requirements
            var noAuthApis = new[] { typeof(IAuthApi), typeof(IDashboardApi) };
            var optionalAuthApis = new[] { typeof(IBooksApi), typeof(ICategoriesApi), typeof(IReviewApi) };
            var requiredAuthApis = new[]
            {
                typeof(IWishlistApi), typeof(ICartApi), typeof(IAddressApi), typeof(IOrderApi),
                typeof(IAdminDashboardApi), typeof(IAuthorApi), typeof(IAdminReportApi),
                typeof(ISupplierApi), typeof(IStockReceiptApi), typeof(IInventoryApi),
                typeof(IAdminPromotionApi), typeof(IAdminUserApi)
            };

            // Register APIs without auth
            foreach (var apiType in noAuthApis)
            {
                services.AddRefitClient(apiType, refitSettings)
                        .ConfigureHttpClient(c => c.BaseAddress = baseUri);
            }

            // Register APIs with optional auth
            foreach (var apiType in optionalAuthApis)
            {
                services.AddRefitClient(apiType, refitSettings)
                        .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                        .AddHttpMessageHandler<AuthHeaderHandler>();
            }

            // Register APIs requiring auth
            foreach (var apiType in requiredAuthApis)
            {
                services.AddRefitClient(apiType, refitSettings)
                        .ConfigureHttpClient(c => c.BaseAddress = baseUri)
                        .AddHttpMessageHandler<AuthHeaderHandler>();
            }
        }

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
    }
}
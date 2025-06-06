using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Bookstore.Mobile.ViewModels
{
    public partial class AdminDashboardViewModel : BaseViewModel
    {
        private readonly IAdminDashboardApi _dashboardApi;
        private readonly ILogger<AdminDashboardViewModel> _logger;
        // private readonly INavigationService _navigationService;

        public AdminDashboardViewModel(IAdminDashboardApi dashboardApi, ILogger<AdminDashboardViewModel> logger/*,...*/)
        {
            _dashboardApi = dashboardApi;
            _logger = logger;
            Title = "Dashboard";
            Summary = new AdminDashboardSummaryDto();
        }

        [ObservableProperty]
        private AdminDashboardSummaryDto _summary;

        [RelayCommand]
        private async Task LoadSummaryAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;
            try
            {
                _logger.LogInformation("Loading admin dashboard summary.");
                // Có thể lấy ngưỡng tồn kho thấp từ Settings hoặc để mặc định
                var response = await _dashboardApi.GetSummary(lowStockThreshold: 5);

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    Summary = response.Content;
                    _logger.LogInformation("Admin dashboard summary loaded successfully.");
                }
                else
                {
                    string errorContent = response.Error?.Content ?? response.ReasonPhrase ?? "Failed to load summary.";
                    ErrorMessage = $"Error: {errorContent}";
                    _logger.LogWarning("Failed to load admin summary. Status: {StatusCode}, Reason: {Reason}", response.StatusCode, ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while loading admin summary.");
                ErrorMessage = $"An unexpected error occurred: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(ShowContent));
            }
        }

        // Command điều hướng (ví dụ)
        [RelayCommand]
        private async Task GoToLowStockReportAsync()
        {
            _logger.LogInformation("Navigating to Low Stock Report page.");
            await Shell.Current.DisplaySnackbar("Navigate to Low Stock Report (To be implemented)", duration: TimeSpan.FromSeconds(2));
        }


        public void OnAppearing()
        {
            // Load dữ liệu khi trang xuất hiện
            LoadSummaryCommand.Execute(null);
        }
    }
}
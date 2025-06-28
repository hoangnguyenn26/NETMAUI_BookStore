// Bookstore.Mobile/ViewModels/HomeViewModel.cs
using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Interfaces.Services; // AuthService
using Bookstore.Mobile.Models;
using Bookstore.Mobile.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Bookstore.Mobile.ViewModels
{
    public partial class HomeViewModel : BaseViewModel
    {
        // Inject Services
        private readonly IDashboardApi _dashboardApi;
        private readonly IAuthService _authService;
        private readonly ILogger<HomeViewModel> _logger;
        // private readonly INavigationService _navigationService; // Sẽ dùng sau

        public HomeViewModel(IDashboardApi dashboardApi, IAuthService authService, /*INavigationService navigationService,*/ ILogger<HomeViewModel> logger)
        {
            _dashboardApi = dashboardApi;
            _authService = authService;
            _logger = logger;
            // _navigationService = navigationService;
            Title = "Home";
            DashboardData = new HomeDashboardDto();
            UpdateWelcomeMessage();
        }

        [ObservableProperty]
        private HomeDashboardDto _dashboardData;

        [ObservableProperty]
        private string _welcomeMessage = "Welcome!";

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string? _searchTerm;

        [RelayCommand]
        private async Task LoadDashboardAsync()
        {
            await RunSafeAsync(async () =>
            {
                _logger.LogInformation("Loading dashboard data");
                var response = await _dashboardApi.GetHomeDashboard();
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    DashboardData = response.Content;
                    UpdateWelcomeMessage();
                }
                else
                {
                    ErrorMessage = response.Error?.Content ?? "Failed to load dashboard data.";
                }
            }, nameof(ShowContent));
        }

        private void UpdateWelcomeMessage()
        {
            if (_authService.IsLoggedIn && _authService.CurrentUser != null)
            {
                WelcomeMessage = $"Welcome, {_authService.CurrentUser.FirstName ?? _authService.CurrentUser.UserName}!";
                IsLoggedIn = true;
            }
            else
            {
                WelcomeMessage = "Welcome!";
                IsLoggedIn = false;
            }
        }

        // Command để điều hướng đến chi tiết sách
        [RelayCommand]
        private async Task GoToBookDetailsAsync(Guid? bookId)
        {
            if (!bookId.HasValue || bookId.Value == Guid.Empty) return;
            _logger.LogInformation("Navigating to Book Details for Id: {BookId}", bookId.Value);
            // Dùng Shell Navigation để truyền tham số
            await Shell.Current.GoToAsync($"{nameof(BookDetailsPage)}?BookId={bookId.Value}");
            // await _navigationService.NavigateToAsync(nameof(BookDetailsPage), new Dictionary<string, object> { { "BookId", bookId.Value } });
        }

        // Command để điều hướng đến danh sách sách của danh mục
        [RelayCommand]
        private async Task GoToCategoryAsync(Guid? categoryId)
        {
            if (!categoryId.HasValue || categoryId.Value == Guid.Empty) return;
            _logger.LogInformation("Navigating to Books Page for Category Id: {CategoryId}", categoryId.Value);
            await Shell.Current.GoToAsync($"{nameof(BooksPage)}?CategoryId={categoryId.Value}");
            // await _navigationService.NavigateToAsync(nameof(BooksPage), new Dictionary<string, object> { { "CategoryId", categoryId.Value } });
        }

        [RelayCommand]
        private async Task SearchBooksAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchTerm)) return;
            _logger.LogInformation($"Searching books with term: {SearchTerm}");
            // Điều hướng sang BooksPage với SearchTerm
            await Shell.Current.GoToAsync($"{nameof(BooksPage)}?SearchTermQuery={Uri.EscapeDataString(SearchTerm)}");
        }

        public void OnAppearing()
        {
            UpdateWelcomeMessage();
            // Always load data when page appears
            LoadDashboardCommand.Execute(null);
        }
    }
}
using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Models;
using Bookstore.Mobile.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Bookstore.Mobile.ViewModels
{
    public partial class AdminUserListViewModel : BaseViewModel
    {
        private readonly IAdminUserApi _userApi;
        private readonly ILogger<AdminUserListViewModel> _logger;

        private int _currentPage = 1;
        private const int PageSize = 15;
        private int _totalUserCount = 0;
        private bool _isInitialized = false;

        public AdminUserListViewModel(IAdminUserApi userApi, ILogger<AdminUserListViewModel> logger)
        {
            _userApi = userApi ?? throw new ArgumentNullException(nameof(userApi));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Title = "Manage Users";
            Users = new ObservableCollection<UserDto>();
            AvailableRoles = new ObservableCollection<string> { "All", "Admin", "Staff", "User" };
            AvailableStatuses = new ObservableCollection<string> { "All", "Active", "Inactive" };
            SelectedRoleFilter = "All";
            SelectedStatusFilter = "All";
        }

        [ObservableProperty]
        private ObservableCollection<UserDto> _users;

        [ObservableProperty]
        private string? _searchTerm;

        [ObservableProperty]
        private string _selectedRoleFilter;

        [ObservableProperty]
        private string _selectedStatusFilter;

        [ObservableProperty]
        private string? _pagingInfo;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanLoadMore))]
        private bool _isLoadingMore;

        [ObservableProperty]
        private bool _canLoadMore = true;

        public ObservableCollection<string> AvailableRoles { get; }
        public ObservableCollection<string> AvailableStatuses { get; }

        async partial void OnSelectedRoleFilterChanged(string value) => await LoadUsersAsync(true);
        async partial void OnSelectedStatusFilterChanged(string value) => await LoadUsersAsync(true);

        [RelayCommand]
        private async Task LoadUsersAsync(bool isRefreshing = false)
        {
            if (IsBusy && !isRefreshing) return;

            await RunSafeAsync(async () =>
            {
                if (_isLoadingMore || (!isRefreshing && !_canLoadMore)) return;

                if (isRefreshing)
                {
                    _currentPage = 1;
                    Users.Clear();
                    _canLoadMore = true;
                    _totalUserCount = 0;
                }

                string? roleFilterParam = (SelectedRoleFilter == "All") ? null : SelectedRoleFilter;
                bool? statusFilterParam = null;
                if (SelectedStatusFilter == "Active")
                {
                    statusFilterParam = true;
                }
                else if (SelectedStatusFilter == "Inactive")
                {
                    statusFilterParam = false;
                }

                _logger.LogInformation("Loading admin users. Role: {Role}, Status: {Status}, Search: {Search}, Page: {Page}",
                    roleFilterParam ?? "All", statusFilterParam?.ToString() ?? "All", SearchTerm ?? "None", _currentPage);

                var response = await _userApi.GetUsers(_currentPage, PageSize, roleFilterParam, statusFilterParam, SearchTerm);

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    var items = response.Content.ToList();
                    foreach (var user in items)
                    {
                        if (!Users.Any(u => u.Id == user.Id))
                        {
                            Users.Add(user);
                        }
                    }

                    _totalUserCount = Users.Count;
                    _canLoadMore = items.Count == PageSize;
                    if (_canLoadMore) _currentPage++;

                    UpdatePagingInfo();
                    _logger.LogInformation("Loaded {Count} users. Total (estimated): {Total}. Can load more: {CanLoadMore}",
                        items.Count, _totalUserCount, _canLoadMore);
                }
                else
                {
                    var errorMessage = response.Error?.Content ?? "Failed to load users.";
                    await DisplayAlertAsync("Error", errorMessage);
                    ErrorMessage = errorMessage;
                    _logger.LogWarning("Failed to load admin users. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorMessage);
                }
            }, propertyName: nameof(ShowContent));
        }

        [RelayCommand]
        private async Task LoadMoreUsersAsync()
        {
            if (_isLoadingMore || !_canLoadMore || IsBusy) return;
            _isLoadingMore = true;
            await LoadUsersAsync(isRefreshing: false);
            _isLoadingMore = false;
        }

        [RelayCommand]
        private async Task SearchUsersAsync() => await LoadUsersAsync(true);

        [RelayCommand]
        private async Task GoToUserDetailsAsync(Guid? userId)
        {
            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                _logger.LogInformation("Navigating to Admin User Details for UserId: {UserId}", userId.Value);
                await Shell.Current.GoToAsync($"{nameof(AdminUserDetailsPage)}?UserId={userId.Value}");
            }
        }

        private void UpdatePagingInfo()
        {
            if (Users.Any())
            {
                var estimatedTotalPages = (_totalUserCount + PageSize - 1) / PageSize;
                PagingInfo = $"Page {_currentPage - 1} of {estimatedTotalPages}. Showing {Users.Count} users";
            }
            else
            {
                PagingInfo = "No users found.";
            }
        }

        public async void OnAppearing()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                await LoadUsersAsync(true);
            }
        }
    }
}
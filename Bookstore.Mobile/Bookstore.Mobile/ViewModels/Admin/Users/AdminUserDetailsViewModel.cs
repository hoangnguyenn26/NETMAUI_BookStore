using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Bookstore.Mobile.ViewModels
{
    [QueryProperty(nameof(UserIdString), "UserId")]
    public partial class AdminUserDetailsViewModel : BaseViewModel
    {
        private readonly IAdminUserApi _userApi;
        private readonly ILogger<AdminUserDetailsViewModel> _logger;

        private Guid _actualUserId = Guid.Empty;
        private string? _userIdString;

        public AdminUserDetailsViewModel(IAdminUserApi userApi, ILogger<AdminUserDetailsViewModel> logger)
        {
            _userApi = userApi ?? throw new ArgumentNullException(nameof(userApi));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Title = "User Details";
        }

        public string? UserIdString
        {
            get => _userIdString;
            set
            {
                if (_userIdString != value)
                {
                    _userIdString = value;
                    ProcessUserId(value);
                }
            }
        }

        [ObservableProperty] private UserDto? _userDetails;
        [ObservableProperty] private bool _isUserActive;

        public override bool ShowContent => !IsBusy && UserDetails != null && !HasError;
        public string UserFullName => $"{UserDetails?.FirstName} {UserDetails?.LastName}".Trim();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateStatusCommand))]
        private bool _isUpdatingStatus;
        [ObservableProperty] private string? _updateStatusMessage;
        [ObservableProperty] private Color? _updateStatusColor;
        public bool ShowUpdateStatusMessage => !string.IsNullOrEmpty(UpdateStatusMessage);
        public bool CanUpdateStatus => UserDetails != null && IsUserActive != UserDetails.IsActive && !IsBusy && !IsUpdatingStatus;

        private async void ProcessUserId(string? idString)
        {
            IsBusy = true;
            ErrorMessage = null;
            UserDetails = null;

            try
            {
                if (string.IsNullOrEmpty(idString))
                {
                    ErrorMessage = "User ID is required.";
                    return;
                }

                if (!Guid.TryParse(idString, out Guid parsedId) || parsedId == Guid.Empty)
                {
                    ErrorMessage = "Invalid User ID.";
                    return;
                }

                _actualUserId = parsedId;
                _logger.LogInformation("Processing User ID: {UserId}", _actualUserId);
                await LoadUserDetailsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing User ID: {UserId}", idString);
                ErrorMessage = "An error occurred while processing the User ID.";
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(ShowContent));
            }
        }

        [RelayCommand]
        private async Task LoadUserDetailsAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (_actualUserId == Guid.Empty)
                {
                    ErrorMessage = "Invalid User ID.";
                    return;
                }

                _logger.LogInformation("Loading details for user {UserId}", _actualUserId);
                var response = await _userApi.GetUserById(_actualUserId);

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    UserDetails = response.Content;
                    IsUserActive = UserDetails.IsActive;
                    _logger.LogInformation("Successfully loaded user details for {UserId}", _actualUserId);
                }
                else
                {
                    var errorMessage = response.Error?.Content ?? "Failed to load user details.";
                    ErrorMessage = errorMessage;
                    _logger.LogWarning("Failed to load user details. Status: {StatusCode}, Error: {Error}", 
                        response.StatusCode, errorMessage);
                }
            }, propertyName: nameof(ShowContent));
        }

        [RelayCommand(CanExecute = nameof(CanUpdateStatus))]
        private async Task UpdateStatusAsync()
        {
            if (UserDetails == null || _actualUserId == Guid.Empty) return;

            IsUpdatingStatus = true;
            UpdateStatusMessage = null;
            _logger.LogInformation("Admin attempting to update status for user {UserId} to IsActive={IsActive}", 
                _actualUserId, IsUserActive);

            try
            {
                var statusDto = new UpdateUserStatusDto { IsActive = IsUserActive };
                var response = await _userApi.UpdateUserStatus(_actualUserId, statusDto);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("User {UserId} status updated successfully to {IsActive}", 
                        _actualUserId, IsUserActive);
                    UpdateStatusMessage = "Status Updated Successfully!";
                    UpdateStatusColor = Colors.Green;
                    UserDetails.IsActive = IsUserActive;
                    OnPropertyChanged(nameof(UserDetails));
                }
                else
                {
                    string errorContent = response.Error?.Content ?? "Failed to update status.";
                    UpdateStatusMessage = $"Error: {errorContent}";
                    UpdateStatusColor = Colors.Red;
                    _logger.LogWarning("Failed to update status for user {UserId}. Status: {StatusCode}", 
                        _actualUserId, response.StatusCode);
                    IsUserActive = UserDetails.IsActive;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while updating status for user {UserId}", _actualUserId);
                UpdateStatusMessage = "An unexpected error occurred.";
                UpdateStatusColor = Colors.Red;
                IsUserActive = UserDetails.IsActive;
            }
            finally
            {
                IsUpdatingStatus = false;
                OnPropertyChanged(nameof(ShowUpdateStatusMessage));
                UpdateStatusCommand.NotifyCanExecuteChanged();
            }
        }

        public void OnAppearing()
        {
            if (UserDetails == null && _actualUserId != Guid.Empty && !IsBusy)
            {
                LoadUserDetailsCommand.Execute(null);
            }
            else if (UserDetails != null)
            {
                UpdateStatusMessage = null;
                OnPropertyChanged(nameof(ShowUpdateStatusMessage));
                IsUserActive = UserDetails.IsActive;
                UpdateStatusCommand.NotifyCanExecuteChanged();
            }
        }
    }
}
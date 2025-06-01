using Bookstore.Mobile.Enums;
using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Bookstore.Mobile.Helpers;

namespace Bookstore.Mobile.ViewModels
{
    public partial class InventoryAdjustmentViewModel : BaseViewModel
    {
        private readonly IBooksApi _booksApi;
        private readonly IInventoryApi _inventoryApi;
        private readonly ILogger<InventoryAdjustmentViewModel> _logger;
        // private readonly IAuthService _authService; // Inject if needed for UserId

        public InventoryAdjustmentViewModel(IBooksApi booksApi, IInventoryApi inventoryApi, ILogger<InventoryAdjustmentViewModel> logger/*, IAuthService authService*/)
        {
            _booksApi = booksApi ?? throw new ArgumentNullException(nameof(booksApi));
            _inventoryApi = inventoryApi ?? throw new ArgumentNullException(nameof(inventoryApi));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // _authService = authService;
            Title = "Inventory Adjustment";
            BookSearchResults = new ObservableCollection<BookDto>();
            AdjustmentReasons = new ObservableCollection<InventoryReason>(
                Enum.GetValues(typeof(InventoryReason))
                    .Cast<InventoryReason>()
                    .Where(r => r == InventoryReason.Adjustment)
                    .ToList()
            );
        }

        [ObservableProperty] private string? _bookSearchTerm;
        [ObservableProperty] private ObservableCollection<BookDto> _bookSearchResults;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsBookSelected))][NotifyPropertyChangedFor(nameof(SelectedBookDisplay))][NotifyCanExecuteChangedFor(nameof(AdjustStockCommand))] private BookDto? _selectedBookSearchResult;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(AdjustStockCommand))] private string? _changeQuantity;
        [ObservableProperty] private ObservableCollection<InventoryReason> _adjustmentReasons;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(AdjustStockCommand))] private InventoryReason? _selectedReason;
        [ObservableProperty] private string? _notes;

        public bool ShowSearchResults => BookSearchResults.Count > 0;
        public bool IsBookSelected => SelectedBookSearchResult != null;
        public string SelectedBookDisplay => SelectedBookSearchResult != null ? $"{SelectedBookSearchResult.Title} (Stock: {SelectedBookSearchResult.StockQuantity})" : string.Empty;
        public bool CanAdjustStock =>
            SelectedBookSearchResult != null &&
            int.TryParse(ChangeQuantity, out int qty) && qty != 0 &&
            SelectedReason.HasValue &&
            IsNotBusy;

        [RelayCommand]
        private async Task SearchBookAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(BookSearchTerm) || BookSearchTerm.Length < 2)
                {
                    MainThread.BeginInvokeOnMainThread(() => BookSearchResults.Clear());
                    OnPropertyChanged(nameof(ShowSearchResults));
                    return;
                }
                _logger.LogInformation("Searching books with term: {SearchTerm}", BookSearchTerm);
                var response = await _booksApi.GetBooks(null, null, BookSearchTerm, 1, 20);
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        BookSearchResults.Clear();
                        foreach (var book in response.Content) BookSearchResults.Add(book);
                        OnPropertyChanged(nameof(ShowSearchResults));
                    });
                }
                else
                {
                    string error = response.Error?.Content ?? "Search failed";
                    _logger.LogWarning("Book search failed. Status: {Status}", response.StatusCode);
                    ErrorMessage = $"Search Error: {error}";
                    MainThread.BeginInvokeOnMainThread(() => BookSearchResults.Clear());
                    OnPropertyChanged(nameof(ShowSearchResults));
                }
            }, nameof(ShowContent));
        }

        partial void OnSelectedBookSearchResultChanged(BookDto? value)
        {
            if (value != null)
            {
                MainThread.BeginInvokeOnMainThread(() => BookSearchResults.Clear());
                OnPropertyChanged(nameof(ShowSearchResults));
                OnPropertyChanged(nameof(SelectedBookDisplay));
                AdjustStockCommand.NotifyCanExecuteChanged();
            }
            else
            {
                OnPropertyChanged(nameof(SelectedBookDisplay));
            }
        }

        [RelayCommand(CanExecute = nameof(CanAdjustStock))]
        private async Task AdjustStockAsync()
        {
            IsBusy = true;
            ErrorMessage = null;
            _logger.LogInformation("Adjusting stock for Book {BookId}", SelectedBookSearchResult!.Id);

            try
            {
                // Guid userId = _authService?.CurrentUser?.Id ?? Guid.Empty; // Get current user Id

                var adjustDto = new AdjustInventoryRequestDto
                {
                    BookId = SelectedBookSearchResult!.Id,
                    ChangeQuantity = int.Parse(ChangeQuantity!),
                    Reason = SelectedReason!.Value,
                    Notes = Notes
                };

                var response = await _inventoryApi.AdjustStock(adjustDto);

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    var newQuantity = response.Content;
                    _logger.LogInformation("Stock adjusted successfully for Book {BookId}. New quantity: {NewQuantity}", SelectedBookSearchResult!.Id, newQuantity);
                    await Shell.Current.DisplaySnackbar($"Stock adjusted successfully. New quantity for '{SelectedBookSearchResult.Title}': {newQuantity}", duration: TimeSpan.FromSeconds(2));
                    ResetForm();
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    string errorContent = ErrorMessageHelper.ToFriendlyErrorMessage(response.Error?.Content) ?? response.ReasonPhrase ?? "Failed";
                    ErrorMessage = $"Adjustment Failed: {errorContent}";
                    _logger.LogWarning("Failed to adjust stock for Book {BookId}. Status: {StatusCode}, Reason: {Reason}", SelectedBookSearchResult!.Id, response.StatusCode, ErrorMessage);
                    await DisplayAlertAsync("Adjustment Failed", ErrorMessage);
                }
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error during stock adjustment for Book {BookId}", SelectedBookSearchResult?.Id);
                ErrorMessage = ex.Message;
                await DisplayAlertAsync("Validation Error", ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during stock adjustment for Book {BookId}", SelectedBookSearchResult?.Id);
                ErrorMessage = $"An unexpected error occurred: {ex.Message}";
                await DisplayAlertAsync("Error", ErrorMessage);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetForm()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BookSearchTerm = null;
                BookSearchResults.Clear();
                SelectedBookSearchResult = null;
                ChangeQuantity = null;
                SelectedReason = null;
                Notes = null;
                ErrorMessage = null;

                OnPropertyChanged(nameof(BookSearchTerm));
                OnPropertyChanged(nameof(ShowSearchResults));
                // SelectedBookSearchResult change triggers other OnPropertyChanged via partial method
                OnPropertyChanged(nameof(ChangeQuantity));
                OnPropertyChanged(nameof(SelectedReason));
                OnPropertyChanged(nameof(Notes));
                OnPropertyChanged(nameof(HasError));
                AdjustStockCommand.NotifyCanExecuteChanged();
            });
        }

        public void OnAppearing()
        {
            ResetForm();
        }
    }
}
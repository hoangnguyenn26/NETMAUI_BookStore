using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Interfaces.Services;
using Bookstore.Mobile.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Bookstore.Mobile.ViewModels
{
    public partial class CreateStockReceiptDetailViewModelDto : ObservableObject
    {
        [ObservableProperty]
        private CreateStockReceiptDetailDto _detailData;

        [ObservableProperty]
        private BookDto _bookSearchResult;

        public CreateStockReceiptDetailViewModelDto(CreateStockReceiptDetailDto detail, BookDto book)
        {
            DetailData = detail;
            BookSearchResult = book;
        }

        public int QuantityReceived
        {
            get => DetailData.QuantityReceived;
            set => SetProperty(DetailData.QuantityReceived, value, DetailData, (dto, val) => dto.QuantityReceived = val);
        }
        public decimal? PurchasePrice
        {
            get => DetailData.PurchasePrice;
            set => SetProperty(DetailData.PurchasePrice, value, DetailData, (dto, val) => dto.PurchasePrice = val);
        }
    }

    public partial class CreateStockReceiptViewModel : BaseViewModel
    {
        private readonly IStockReceiptApi _receiptApi;
        private readonly ISupplierApi _supplierApi;
        private readonly IBooksApi _booksApi;
        private readonly IAuthService _authService;
        private readonly ILogger<CreateStockReceiptViewModel> _logger;

        private System.Timers.Timer? _debounceTimer;
        private const int DebounceTimeMs = 500; // Thời gian chờ trước khi tìm kiếm

        public CreateStockReceiptViewModel(
            IStockReceiptApi receiptApi,
            ISupplierApi supplierApi,
            IBooksApi booksApi,
            IAuthService authService,
            ILogger<CreateStockReceiptViewModel> logger)
        {
            _receiptApi = receiptApi;
            _supplierApi = supplierApi;
            _booksApi = booksApi;
            _authService = authService;
            _logger = logger;
            Title = "New Stock Receipt";
            Suppliers = new ObservableCollection<SupplierDto>();
            ReceiptDetails = new ObservableCollection<CreateStockReceiptDetailViewModelDto>();
            BookSearchResults = new ObservableCollection<BookDto>();
            LoadSuppliersCommand.Execute(null);
        }

        [ObservableProperty] private SupplierDto? _selectedSupplier;
        [ObservableProperty] private DateTime _receiptDate = DateTime.Now;
        [ObservableProperty] private string? _notes;
        [ObservableProperty] private ObservableCollection<SupplierDto> _suppliers;
        [ObservableProperty] private string? _bookSearchTerm;
        [ObservableProperty] private ObservableCollection<BookDto> _bookSearchResults;
        [ObservableProperty] private BookDto? _selectedBookSearchResult;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(AddDetailCommand))] private string? _newDetailQuantity;
        [ObservableProperty] private string? _newDetailPrice;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SaveReceiptCommand))] private ObservableCollection<CreateStockReceiptDetailViewModelDto> _receiptDetails;
        [ObservableProperty] private bool _isSearchingBooks; // Cờ riêng cho tìm kiếm sách

        public bool ShowSearchResults => BookSearchResults.Count > 0 && !string.IsNullOrWhiteSpace(BookSearchTerm);
        public bool CanAddDetail => SelectedBookSearchResult != null && int.TryParse(NewDetailQuantity, out int qty) && qty > 0 && IsNotBusy;
        public bool CanSaveReceipt => ReceiptDetails.Count > 0 && IsNotBusy;


        partial void OnBookSearchTermChanged(string? value)
        {
            // Reset timer cũ nếu có
            _debounceTimer?.Stop();
            _debounceTimer?.Dispose();

            if (string.IsNullOrWhiteSpace(value) || value.Length < 2) // Chỉ tìm khi có ít nhất 2 ký tự
            {
                MainThread.BeginInvokeOnMainThread(() => BookSearchResults.Clear());
                OnPropertyChanged(nameof(ShowSearchResults));
                return;
            }

            // Tạo timer mới
            _debounceTimer = new System.Timers.Timer(DebounceTimeMs);
            _debounceTimer.Elapsed += async (sender, e) => await ExecuteSearchAsync(value);
            _debounceTimer.AutoReset = false; // Chỉ chạy 1 lần
            _debounceTimer.Start();
        }

        // Hàm thực thi tìm kiếm thực tế (sau debounce)
        private async Task ExecuteSearchAsync(string searchTerm)
        {
            // Dừng timer để tránh chạy lại
            _debounceTimer?.Stop();
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            MainThread.BeginInvokeOnMainThread(() => IsSearchingBooks = true); // Bật indicator tìm kiếm

            try
            {
                var response = await _booksApi.GetBooks(null, null, searchTerm, 1, 10); // Lấy 10 kết quả đầu
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BookSearchResults.Clear();
                    if (response.IsSuccessStatusCode && response.Content != null)
                    {
                        foreach (var book in response.Content)
                        {
                            BookSearchResults.Add(book);
                        }
                    }
                    OnPropertyChanged(nameof(ShowSearchResults));
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching books with term {SearchTerm}", searchTerm);
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() => IsSearchingBooks = false);
            }
        }


        [RelayCommand]
        private async Task LoadSuppliersAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var response = await _supplierApi.GetAllSuppliers();
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    Suppliers.Clear();
                    Suppliers.Add(new SupplierDto { Id = Guid.Empty, Name = "- None -" });
                    foreach (var sup in response.Content.OrderBy(s => s.Name)) Suppliers.Add(sup);
                    SelectedSupplier = Suppliers.FirstOrDefault();
                }
                else { ErrorMessage = response.Error?.Content ?? "Failed"; }
            }
            catch (Exception ex) { ErrorMessage = ex.Message; _logger.LogError(ex, "Error loading suppliers."); }
            finally { IsBusy = false; }
        }

        [RelayCommand(CanExecute = nameof(CanAddDetail))]
        private void AddDetail()
        {
            if (SelectedBookSearchResult == null || !int.TryParse(NewDetailQuantity, out int quantity) || quantity <= 0) return;
            decimal? price = decimal.TryParse(NewDetailPrice, out decimal p) ? p : null;

            if (ReceiptDetails.Any(d => d.BookSearchResult.Id == SelectedBookSearchResult.Id))
            {
                MainThread.BeginInvokeOnMainThread(() => DisplayAlertAsync("Duplicate", "This book is already added.", "OK"));
                return;
            }

            var newDetailDto = new CreateStockReceiptDetailDto
            {
                BookId = SelectedBookSearchResult.Id,
                QuantityReceived = quantity,
                PurchasePrice = price
            };
            var newItemVM = new CreateStockReceiptDetailViewModelDto(newDetailDto, SelectedBookSearchResult);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ReceiptDetails.Add(newItemVM);
                BookSearchTerm = string.Empty; OnPropertyChanged(nameof(BookSearchTerm));
                BookSearchResults.Clear(); OnPropertyChanged(nameof(ShowSearchResults));
                SelectedBookSearchResult = null; OnPropertyChanged(nameof(SelectedBookSearchResult));
                NewDetailQuantity = string.Empty; OnPropertyChanged(nameof(NewDetailQuantity));
                NewDetailPrice = string.Empty; OnPropertyChanged(nameof(NewDetailPrice));
                SaveReceiptCommand.NotifyCanExecuteChanged();
            });
            _logger.LogInformation("Added book {BookId} to receipt details.", newItemVM.BookSearchResult.Id);
        }

        [RelayCommand]
        private void RemoveDetail(CreateStockReceiptDetailViewModelDto? item)
        {
            if (item != null)
            {
                MainThread.BeginInvokeOnMainThread(() => ReceiptDetails.Remove(item));
                SaveReceiptCommand.NotifyCanExecuteChanged();
                _logger.LogInformation("Removed book {BookId} from receipt details.", item.BookSearchResult.Id);
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveReceipt))]
        private async Task SaveReceiptAsync()
        {
            IsBusy = true; ErrorMessage = null;
            _logger.LogInformation("Attempting to save stock receipt.");
            try
            {
                var createDto = new CreateStockReceiptDto
                {
                    SupplierId = (SelectedSupplier?.Id == Guid.Empty) ? null : SelectedSupplier?.Id,
                    ReceiptDate = ReceiptDate,
                    Notes = Notes,
                    Details = ReceiptDetails.Select(vmDto => vmDto.DetailData).ToList()
                };

                Guid userId = Guid.Empty; // Lấy UserId thực tế
                if (_authService.IsLoggedIn && _authService.CurrentUser != null)
                {
                    userId = _authService.CurrentUser.Id;
                }
                else
                {
                    // Không nên xảy ra vì trang này cần Auth Admin/Staff
                    throw new UnauthorizedAccessException("User must be logged in to create a stock receipt.");
                }


                var response = await _receiptApi.CreateReceipt(createDto); // Cần Auth

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Stock receipt saved successfully.");
                    await Shell.Current.DisplaySnackbar("Stock receipt saved successfully!", duration: TimeSpan.FromSeconds(2));
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    string errorContent = response.Error?.Content ?? "Failed";
                    ErrorMessage = $"Save Error: {errorContent}";
                    _logger.LogWarning("Failed to save stock receipt. Status: {StatusCode}, Reason: {Reason}", response.StatusCode, ErrorMessage);
                    await DisplayAlertAsync("Save Failed", ErrorMessage);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access during save stock receipt.");
                ErrorMessage = "Authorization error.";
                await DisplayAlertAsync("Error", "You are not authorized for this action.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while saving stock receipt.");
                ErrorMessage = $"An unexpected error occurred: {ex.Message}";
                await DisplayAlertAsync("Error", ErrorMessage);
            }
            finally { IsBusy = false; }
        }

        public void OnAppearing()
        {
            if (Suppliers.Count <= 1) LoadSuppliersCommand.Execute(null);
        }
    }
}
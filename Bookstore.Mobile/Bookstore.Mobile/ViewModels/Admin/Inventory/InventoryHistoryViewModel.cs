using Bookstore.Mobile.Enums;
using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Bookstore.Mobile.ViewModels
{
    public partial class InventoryHistoryViewModel : BaseViewModel
    {
        private readonly IInventoryApi _inventoryApi;
        private readonly IBooksApi _booksApi; // Để tìm sách
        private readonly ILogger<InventoryHistoryViewModel> _logger;

        private int _currentPage = 1;
        private const int PageSize = 20;
        private bool _isLoadingMore = false;
        private bool _canLoadMore = true;
        private int _totalLogCount = 0;

        public InventoryHistoryViewModel(IInventoryApi inventoryApi, IBooksApi booksApi, ILogger<InventoryHistoryViewModel> logger)
        {
            _inventoryApi = inventoryApi;
            _booksApi = booksApi;
            _logger = logger;
            Title = "Inventory History";
            InventoryLogs = new ObservableCollection<InventoryLogDto>();
            BookSearchResults = new ObservableCollection<BookDto>();
            AvailableReasons = new ObservableCollection<InventoryReason?>(
                Enum.GetValues(typeof(InventoryReason)).Cast<InventoryReason?>().ToList()
            );
            AvailableReasons.Insert(0, null); // "All" option
            EndDateFilter = DateTime.Now.Date;
            StartDateFilter = EndDateFilter.Value.AddDays(-30); // Mặc định 30 ngày
        }

        // --- Filters ---
        [ObservableProperty] private string? _bookSearchTerm;
        [ObservableProperty] private ObservableCollection<BookDto> _bookSearchResults;
        [ObservableProperty] private BookDto? _selectedBookFilter;
        public bool ShowBookSearchResults => BookSearchResults.Count > 0;
        public bool IsBookFilterApplied => SelectedBookFilter != null;
        public string SelectedBookFilterText => SelectedBookFilter != null ? $"Book: {SelectedBookFilter.Title}" : "Book: All";

        [ObservableProperty] private DateTime? _startDateFilter;
        [ObservableProperty] private DateTime? _endDateFilter;
        [ObservableProperty] private ObservableCollection<InventoryReason?> _availableReasons;
        [ObservableProperty] private InventoryReason? _selectedReasonFilter;

        // --- Data & State ---
        [ObservableProperty] private ObservableCollection<InventoryLogDto> _inventoryLogs;
        [ObservableProperty] private string? _pagingInfo;


        // --- Commands ---
        [RelayCommand]
        private async Task SearchBookAsync(string? searchTerm)
        {
            await RunSafeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2) { BookSearchResults.Clear(); return; }
                var response = await _booksApi.GetBooks(null, null, searchTerm, 1, 10); // Lấy ít kết quả
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    BookSearchResults.Clear();
                    foreach (var book in response.Content) BookSearchResults.Add(book);
                }
            }, showBusy: false); // Không hiện busy indicator chung cho search nhỏ
            OnPropertyChanged(nameof(ShowBookSearchResults));
        }

        partial void OnSelectedBookFilterChanged(BookDto? value)
        {
            // Ẩn kết quả search khi đã chọn, cập nhật text
            OnPropertyChanged(nameof(SelectedBookFilterText));
            OnPropertyChanged(nameof(IsBookFilterApplied));
            if (value != null) BookSearchResults.Clear(); // Xóa kết quả tìm kiếm
        }


        [RelayCommand]
        private async Task ApplyFiltersAsync()
        {
            // Đây là command chính để load/refresh dựa trên tất cả filter hiện tại
            await LoadHistoryAsync(true);
        }

        [RelayCommand]
        private void ClearFilters()
        {
            BookSearchTerm = null; OnPropertyChanged(nameof(BookSearchTerm));
            BookSearchResults.Clear(); OnPropertyChanged(nameof(ShowBookSearchResults));
            SelectedBookFilter = null;
            StartDateFilter = DateTime.Now.Date.AddDays(-30); OnPropertyChanged(nameof(StartDateFilter));
            EndDateFilter = DateTime.Now.Date; OnPropertyChanged(nameof(EndDateFilter));
            SelectedReasonFilter = null; OnPropertyChanged(nameof(SelectedReasonFilter));
            // Tự động load lại khi clear (hoặc yêu cầu nhấn Apply Filters)
            ApplyFiltersCommand.Execute(null);
        }


        [RelayCommand]
        private async Task LoadHistoryAsync(object? parameter)
        {
            bool isRefreshing = parameter is bool b && b;
            await RunSafeAsync(async () =>
            {
                if (_isLoadingMore || (!isRefreshing && !_canLoadMore)) return;
                if (!isRefreshing && IsBusy) return;

                if (isRefreshing) { _currentPage = 1; InventoryLogs.Clear(); _canLoadMore = true; _totalLogCount = 0; }

                _logger.LogInformation("Loading inventory history. Page: {Page}. Filters - BookId: {BookId}, Reason: {Reason}, Start: {Start}, End: {End}",
                                     _currentPage, SelectedBookFilter?.Id, SelectedReasonFilter, StartDateFilter, EndDateFilter);

                var response = await _inventoryApi.GetInventoryHistory(
                    bookId: SelectedBookFilter?.Id,
                    reason: SelectedReasonFilter,
                    startDate: StartDateFilter,
                    endDate: EndDateFilter,
                    page: _currentPage,
                    pageSize: PageSize
                );

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    var result = response.Content;
                    foreach (var log in result.Items) InventoryLogs.Add(log);

                    _totalLogCount = result.TotalCount;
                    _canLoadMore = InventoryLogs.Count < _totalLogCount;
                    _currentPage++;
                    UpdatePagingInfo();
                    _logger.LogInformation("Loaded {Count} logs. Total: {Total}. Can load more: {CanLoadMore}", result.Items.Count(), _totalLogCount, _canLoadMore);
                }
                else
                {
                    ErrorMessage = response.Error?.Content ?? "Failed to load history";
                    _logger.LogWarning("Failed to load inventory history. Status: {StatusCode}", response.StatusCode);
                }
            }, showBusy: true, propertyName: nameof(ShowContent));
            OnPropertyChanged(nameof(PagingInfo));
        }

        [RelayCommand]
        private async Task LoadMoreHistoryAsync()
        {
            if (_isLoadingMore || !_canLoadMore || IsBusy) return;
            _isLoadingMore = true;
            await LoadHistoryAsync(false);
            _isLoadingMore = false;
        }

        private void UpdatePagingInfo()
        {
            PagingInfo = $"Showing {InventoryLogs.Count} of {_totalLogCount} entries. Page {_currentPage - 1} / {(int)Math.Ceiling(_totalLogCount / (double)PageSize)}";
        }

        public void OnAppearing()
        {
            if (InventoryLogs.Count == 0)
            {
                ApplyFiltersCommand.Execute(null);
            }
        }
    }
}
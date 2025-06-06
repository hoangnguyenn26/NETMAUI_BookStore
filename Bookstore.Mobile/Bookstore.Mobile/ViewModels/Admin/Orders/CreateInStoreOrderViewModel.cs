using Bookstore.Mobile.Enums;
using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Interfaces.Services;
using Bookstore.Mobile.Models;
using Bookstore.Mobile.Models.Orders;
using Bookstore.Mobile.Views;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Bookstore.Mobile.ViewModels.Admin.Orders
{
    public partial class CreateInStoreOrderViewModel : BaseViewModel
    {
        private readonly IAdminUserApi _adminUserApi;
        private readonly IBooksApi _booksApi;
        private readonly IOrderApi _orderApi;
        private readonly ILogger<CreateInStoreOrderViewModel> _logger;
        private readonly IAuthService _authService;

        public CreateInStoreOrderViewModel(IAdminUserApi adminUserApi, IBooksApi booksApi, IOrderApi staffOrderApi, ILogger<CreateInStoreOrderViewModel> logger, IAuthService authService)
        {
            _adminUserApi = adminUserApi;
            _booksApi = booksApi;
            _orderApi = staffOrderApi;
            _logger = logger;
            _authService = authService;
            Title = "Create In-Store Order";

            CurrentOrderDetails = new ObservableCollection<InStoreOrderDetailViewModel>();
            CustomerSearchResults = new ObservableCollection<UserDto>();
            BookSearchResults = new ObservableCollection<BookDto>();

            AvailablePaymentMethods = new ObservableCollection<PaymentMethod>(
                Enum.GetValues(typeof(PaymentMethod)).Cast<PaymentMethod>()
                    .Where(pm => pm == PaymentMethod.Cash || pm == PaymentMethod.Card || pm == PaymentMethod.Transfer)
                    .ToList());
            SelectedPaymentMethod = AvailablePaymentMethods.FirstOrDefault();
        }

        // Customer Search
        [ObservableProperty] private string? _customerSearchTerm;
        [ObservableProperty] private ObservableCollection<UserDto> _customerSearchResults;
        [ObservableProperty] private UserDto? _selectedCustomer;
        public bool ShowCustomerSearchResults => CustomerSearchResults.Count > 0 && SelectedCustomer == null;
        public bool IsCustomerSelected => SelectedCustomer != null;
        public string SelectedCustomerText => SelectedCustomer != null ? $"Customer: {SelectedCustomer.UserName} ({SelectedCustomer.Email})" : "Customer: Guest";

        // Book Search for Detail
        [ObservableProperty] private string? _bookSearchTerm;
        [ObservableProperty] private ObservableCollection<BookDto> _bookSearchResults;
        [ObservableProperty] private BookDto? _selectedBookToAdd;
        public bool ShowBookSearchResults => BookSearchResults.Count > 0 && SelectedBookToAdd == null;
        public bool IsBookSelectedForDetail => SelectedBookToAdd != null;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddItemToOrderCommand))]
        private string _newItemQuantity = "1";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateOrderCommand))]
        private ObservableCollection<InStoreOrderDetailViewModel> _currentOrderDetails;
        public bool HasOrderDetails => CurrentOrderDetails.Any();

        [ObservableProperty] private ObservableCollection<PaymentMethod> _availablePaymentMethods;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateOrderCommand))]
        private PaymentMethod? _selectedPaymentMethod;
        [ObservableProperty] private string? _staffNotes;

        [ObservableProperty] private decimal _orderTotalAmount;

        public bool CanAddItem => SelectedBookToAdd != null && int.TryParse(NewItemQuantity, out int qty) && qty > 0 && !IsBusy;

        // --- Commands ---
        [RelayCommand]
        private async Task SearchCustomerAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(CustomerSearchTerm) || CustomerSearchTerm.Length < 2)
                {
                    CustomerSearchResults.Clear();
                    return;
                }
                _logger.LogInformation("Searching customer: {Term}", CustomerSearchTerm);

                var response = await _adminUserApi.GetUsers(search: CustomerSearchTerm, pageSize: 5, role: "User");

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    CustomerSearchResults.Clear();
                    foreach (var user in response.Content)
                    {
                        CustomerSearchResults.Add(user);
                    }
                    _logger.LogInformation("Found {Count} customers matching '{Term}'", CustomerSearchResults.Count, CustomerSearchTerm);
                }
                else
                {
                    CustomerSearchResults.Clear();
                    _logger.LogWarning("Customer search failed for '{Term}'. Status: {StatusCode}", CustomerSearchTerm, response.StatusCode);
                }
            }, showBusy: false, propertyName: nameof(ShowCustomerSearchResults));
        }

        [RelayCommand]
        private void ClearCustomer()
        {
            SelectedCustomer = null;
            CustomerSearchTerm = null;
            CustomerSearchResults.Clear();
            OnPropertyChanged(nameof(SelectedCustomerText));
            OnPropertyChanged(nameof(ShowCustomerSearchResults));
            _logger.LogInformation("Cleared selected customer.");
        }

        partial void OnSelectedCustomerChanged(UserDto? value) => OnPropertyChanged(nameof(SelectedCustomerText));

        partial void OnSelectedBookToAddChanged(BookDto? value)
        {
            OnPropertyChanged(nameof(IsBookSelectedForDetail));
            AddItemToOrderCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task SearchBookAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(BookSearchTerm) || BookSearchTerm.Length < 2)
                {
                    BookSearchResults.Clear();
                    return;
                }
                _logger.LogInformation("Searching book for order detail: {Term}", BookSearchTerm);
                var response = await _booksApi.GetBooks(null, null, BookSearchTerm, 1, 10);
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    BookSearchResults.Clear();
                    foreach (var book in response.Content)
                        BookSearchResults.Add(book);
                }
                else
                {
                    ErrorMessage = response.Error?.Content ?? "Book search failed";
                }
            }, showBusy: false, propertyName: nameof(ShowBookSearchResults));
        }

        [RelayCommand(CanExecute = nameof(CanAddItem))]
        private void AddItemToOrder()
        {
            if (SelectedBookToAdd == null || !int.TryParse(NewItemQuantity, out int quantity) || quantity <= 0)
                return;

            var existingDetail = CurrentOrderDetails.FirstOrDefault(d => d.BookId == SelectedBookToAdd.Id);
            if (existingDetail != null)
            {
                existingDetail.Quantity += quantity;
                OnPropertyChanged(nameof(existingDetail.Quantity));
                _logger.LogInformation("Updated quantity for existing Book {BookId} in order details.", SelectedBookToAdd.Id);
            }
            else
            {
                var newDetailVM = new InStoreOrderDetailViewModel(SelectedBookToAdd, quantity);
                CurrentOrderDetails.Add(newDetailVM);
                _logger.LogInformation("Added new Book {BookId} with quantity {Quantity} to order details.", SelectedBookToAdd.Id, quantity);
            }

            CalculateOrderTotal();
            BookSearchTerm = null;
            OnPropertyChanged(nameof(BookSearchTerm));
            BookSearchResults.Clear();
            OnPropertyChanged(nameof(ShowBookSearchResults));
            SelectedBookToAdd = null;
            NewItemQuantity = "1";
            OnPropertyChanged(nameof(NewItemQuantity));
            CreateOrderCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void ClearSelectedBook() => SelectedBookToAdd = null;

        [RelayCommand]
        private async Task RemoveItemWithConfirmation(InStoreOrderDetailViewModel? item)
        {
            if (item == null)
                return;

            bool confirm = await Application.Current.MainPage.DisplayAlert(
                "Confirm Remove",
                $"Remove '{item.BookTitle}' from the order?",
                "Yes",
                "No");

            if (confirm)
            {
                CurrentOrderDetails.Remove(item);
                CalculateOrderTotal();
                CreateOrderCommand.NotifyCanExecuteChanged();
                _logger.LogInformation("Removed Book {BookId} from order details.", item.BookId);
            }
        }

        private void CalculateOrderTotal()
        {
            OrderTotalAmount = CurrentOrderDetails.Sum(d => d.TotalItemPrice);
        }

        private bool CanExecuteCreateOrder() => CurrentOrderDetails.Any() && SelectedPaymentMethod.HasValue && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanExecuteCreateOrder))]
        private async Task CreateOrderAsync()
        {
            await RunSafeAsync(async () =>
            {
                _logger.LogInformation("Attempting to create in-store order.");

                Guid staffUserId = _authService.CurrentUser?.Id ?? Guid.Empty;
                if (staffUserId == Guid.Empty)
                    throw new UnauthorizedAccessException("Staff not identified.");

                var requestDto = new CreateInStoreOrderRequestDto
                {
                    CustomerUserId = SelectedCustomer?.Id,
                    PaymentMethod = SelectedPaymentMethod!.Value,
                    StaffNotes = StaffNotes,
                    OrderDetails = CurrentOrderDetails.Select(vm => new InStoreOrderDetailDto
                    {
                        BookId = vm.BookId,
                        Quantity = vm.Quantity
                    }).ToList()
                };

                var response = await _orderApi.CreateInStoreOrder(requestDto);

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    var createdOrder = response.Content;
                    _logger.LogInformation("In-Store Order {OrderId} created successfully.", createdOrder.Id);
                    await Shell.Current.DisplaySnackbar($"Order #{createdOrder.Id.ToString().Substring(0, 8).ToUpper()} created successfully!", duration: TimeSpan.FromSeconds(2));
                    ResetAllForm();
                    ErrorMessage = null; // Clear error on success
                    await Shell.Current.GoToAsync($"//{nameof(AdminOrderListPage)}");
                }
                else
                {
                    string errorContent = response.Error?.Content ?? response.ReasonPhrase ?? "Failed";
                    ErrorMessage = $"Order Creation Failed: {errorContent}";
                    _logger.LogWarning("Failed to create in-store order. Status: {StatusCode}, Reason: {Reason}", response.StatusCode, ErrorMessage);
                    await DisplayAlertAsync("Order Failed", ErrorMessage);
                }
            }, showBusy: true, nameof(ShowContent));
        }

        private void ResetAllForm()
        {
            CustomerSearchTerm = null;
            OnPropertyChanged(nameof(CustomerSearchTerm));
            CustomerSearchResults.Clear();
            OnPropertyChanged(nameof(CustomerSearchResults));
            SelectedCustomer = null;
            BookSearchTerm = null;
            OnPropertyChanged(nameof(BookSearchTerm));
            BookSearchResults.Clear();
            OnPropertyChanged(nameof(ShowBookSearchResults));
            SelectedBookToAdd = null;
            NewItemQuantity = "1";
            OnPropertyChanged(nameof(NewItemQuantity));
            CurrentOrderDetails.Clear();
            OnPropertyChanged(nameof(CurrentOrderDetails));
            SelectedPaymentMethod = AvailablePaymentMethods.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedPaymentMethod));
            StaffNotes = null;
            OnPropertyChanged(nameof(StaffNotes));
            OrderTotalAmount = 0;
            OnPropertyChanged(nameof(OrderTotalAmount));
            CreateOrderCommand.NotifyCanExecuteChanged();
            _logger.LogInformation("In-Store order form reset.");
        }

        public void OnAppearing()
        {
            ResetAllForm();
        }
    }
}
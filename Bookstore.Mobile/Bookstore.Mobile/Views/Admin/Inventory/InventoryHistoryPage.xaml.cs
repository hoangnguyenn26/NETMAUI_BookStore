using Bookstore.Mobile.ViewModels;

namespace Bookstore.Mobile.Views;

public partial class InventoryHistoryPage : ContentPage
{
    private readonly InventoryHistoryViewModel _viewModel;
    public InventoryHistoryPage(InventoryHistoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }
}
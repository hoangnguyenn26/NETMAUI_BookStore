using Bookstore.Mobile.ViewModels;

namespace Bookstore.Mobile.Views;

public partial class CreateInStoreOrderPage : ContentPage
{
    private readonly CreateInStoreOrderViewModel _viewModel;
    public CreateInStoreOrderPage(CreateInStoreOrderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CreateInStoreOrderViewModel vm)
        {
            vm.OnAppearing();
        }
    }
}
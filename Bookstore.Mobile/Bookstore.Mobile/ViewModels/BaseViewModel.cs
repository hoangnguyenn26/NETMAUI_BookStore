using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bookstore.Mobile.ViewModels
{
    // Kế thừa ObservableObject để tự động thông báo thay đổi thuộc tính
    public abstract partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        [ObservableProperty]
        private string? _title;

        [ObservableProperty]
        private string? _errorMessage;

        public bool IsNotBusy => !IsBusy;
        public virtual bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public virtual bool ShowContent => !IsBusy && !HasError;

        //Hàm xử lý lỗi chung
        protected virtual async Task DisplayAlertAsync(string title, string message, string cancel = "OK")
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, cancel);
            }
        }

        // Helper for safe async execution
        protected async Task RunSafeAsync(Func<Task> action, [CallerMemberName] string? propertyName = null)
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                await DisplayAlertAsync("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(HasError));
                if (propertyName != null) OnPropertyChanged(propertyName);
            }
        }
        protected async Task RunSafeAsync(Func<Task> action, bool showBusy = true, [CallerMemberName] string? propertyName = null)
        {
            if (IsBusy && showBusy) return;

            if (showBusy)
            {
                IsBusy = true;
            }

            ErrorMessage = null;

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                await DisplayAlertAsync("Error", ex.Message);
            }
            finally
            {
                if (showBusy)
                {
                    IsBusy = false;
                }
                OnPropertyChanged(nameof(HasError));
                if (propertyName != null) OnPropertyChanged(propertyName);
            }
        }

        // (Optional) Hàm điều hướng chung (sẽ cần inject INavigationService sau)
        // protected readonly INavigationService _navigationService;
        // public BaseViewModel(INavigationService navigationService) { _navigationService = navigationService; }
    }
}
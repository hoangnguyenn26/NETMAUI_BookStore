using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Bookstore.Mobile.ViewModels
{
    // Change the QueryProperty to use BookIdString instead of BookId
    [QueryProperty(nameof(BookIdString), "BookId")]
    [QueryProperty(nameof(BookTitle), "BookTitle")]
    public partial class SubmitReviewViewModel : BaseViewModel
    {
        private readonly IReviewApi _reviewApi;
        private readonly ILogger<SubmitReviewViewModel> _logger;
        // private readonly INavigationService _navigationService;

        public SubmitReviewViewModel(IReviewApi reviewApi, ILogger<SubmitReviewViewModel> logger)
        {
            _reviewApi = reviewApi;
            _logger = logger;
        }

        private string _bookIdString;

        [ObservableProperty]
        private Guid? _bookId;

        // Property to handle the string representation of the BookId
        public string BookIdString
        {
            get => _bookIdString;
            set
            {
                _bookIdString = value;
                if (Guid.TryParse(value, out Guid parsedGuid))
                {
                    BookId = parsedGuid;
                    _logger.LogInformation("Successfully parsed BookId from string: {BookIdString} to Guid: {BookId}", value, parsedGuid);
                }
                else
                {
                    BookId = null;
                    _logger.LogWarning("Failed to parse BookId from string value: {BookIdString}", value);
                }
            }
        }

        [ObservableProperty]
        private string? _bookTitle;

        partial void OnBookTitleChanged(string? value)
        {
            Title = $"Review '{value ?? "Book"}'";
        }


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentRating))]
        [NotifyCanExecuteChangedFor(nameof(SubmitReviewCommand))]
        private int _selectedRating = 0;

        [ObservableProperty]
        private string? _comment;

        public List<int> RatingOptions { get; } = new List<int> { 1, 2, 3, 4, 5 };

        public int CurrentRating => SelectedRating;

        public bool CanSubmitReview => SelectedRating > 0 && IsNotBusy;


        // --- Commands ---
        [RelayCommand]
        private void SetRating(object ratingParam)
        {
            if (int.TryParse(ratingParam?.ToString(), out int rating) && rating >= 1 && rating <= 5)
            {
                SelectedRating = rating;
                OnPropertyChanged(nameof(CurrentRating));
                _logger.LogInformation("Rating set to: {Rating}", rating);
                (SubmitReviewCommand as Command)?.ChangeCanExecute();
            }
        }


        [RelayCommand(CanExecute = nameof(CanSubmitReview))]
        private async Task SubmitReviewAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;
            _logger.LogInformation("Submitting review for Book {BookId}", BookId);

            try
            {
                var createDto = new CreateReviewDto
                {
                    Rating = SelectedRating,
                    Comment = Comment
                };

                if (!BookId.HasValue)
                {
                    ErrorMessage = "Invalid book ID.";
                    _logger.LogWarning("Attempted to submit review with invalid BookId. BookIdString was: {BookIdString}", BookIdString);
                    await DisplayAlertAsync("Error", ErrorMessage);
                    return;
                }

                var response = await _reviewApi.SubmitReview(BookId.Value, createDto);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Review submitted successfully for Book {BookId}", BookId);
                    // Thành công, quay lại trang chi tiết sách
                    await Shell.Current.DisplaySnackbar("Thank you for your feedback!", duration: TimeSpan.FromSeconds(2));
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    string errorContent = response.Error?.Content ?? response.ReasonPhrase ?? "Failed to submit review.";
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && errorContent.Contains("already reviewed"))
                    {
                        ErrorMessage = "You have already reviewed this book.";
                    }
                    else
                    {
                        ErrorMessage = $"Error: {errorContent}";
                    }
                    _logger.LogWarning("Failed to submit review for Book {BookId}. Status: {StatusCode}, Reason: {Reason}", BookId, response.StatusCode, ErrorMessage);
                    await DisplayAlertAsync("Submission Failed", ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while submitting review for Book {BookId}", BookId);
                ErrorMessage = $"An unexpected error occurred: {ex.Message}";
                await DisplayAlertAsync("Error", ErrorMessage);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
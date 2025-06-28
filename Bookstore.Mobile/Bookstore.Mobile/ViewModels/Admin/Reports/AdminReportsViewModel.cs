using Bookstore.Mobile.Interfaces.Apis;
using Bookstore.Mobile.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace Bookstore.Mobile.ViewModels
{
    public partial class AdminReportsViewModel : BaseViewModel
    {
        private readonly IAdminReportApi _reportApi;
        private readonly ILogger<AdminReportsViewModel> _logger;

        public AdminReportsViewModel(IAdminReportApi reportApi, ILogger<AdminReportsViewModel> logger)
        {
            _reportApi = reportApi ?? throw new ArgumentNullException(nameof(reportApi));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Title = "Reports";
            EndDate = DateTime.Now.Date;
            StartDate = EndDate.AddDays(-6);
            RevenueReport = new RevenueReportDto();
            BestsellersData = new List<BestsellerDto>();
            LowStockBooks = new ObservableCollection<LowStockBookDto>();

            RevenueChart = CreateEmptyChart();
            BestsellersChart = CreateEmptyChart(isBarChart: true);
        }

        [ObservableProperty] private DateTime _startDate;
        [ObservableProperty] private DateTime _endDate;
        [ObservableProperty] private RevenueReportDto _revenueReport;
        [ObservableProperty] private Chart? _revenueChart;
        [ObservableProperty] private bool _showRevenueReport = false;
        [ObservableProperty] private Chart? _bestsellersChart;
        [ObservableProperty] private bool _showBestsellersReport = false;
        [ObservableProperty] private List<BestsellerDto> _bestsellersData;
        [ObservableProperty] private ObservableCollection<LowStockBookDto> _lowStockBooks;
        [ObservableProperty] private bool _showLowStockReport = false;
        [ObservableProperty] private int _lowStockThreshold = 5;

        [RelayCommand]
        private async Task LoadAllReportsAsync()
        {
            await LoadAllReportsInternalAsync(false);
        }

        private async Task LoadAllReportsInternalAsync(bool isRefreshing = false)
        {
            await RunSafeAsync(async () =>
            {
                if (isRefreshing)
                {
                    _logger.LogInformation("Refreshing all reports.");
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ShowRevenueReport = false;
                    ShowBestsellersReport = false;
                    ShowLowStockReport = false;
                    RevenueChart = CreateEmptyChart();
                    BestsellersChart = CreateEmptyChart(isBarChart: true);
                    LowStockBooks.Clear();
                });

                var revenueTask = LoadRevenueReportInternalAsync();
                var bestsellersTask = LoadBestsellersReportInternalAsync();
                var lowStockTask = LoadLowStockReportInternalAsync();

                await Task.WhenAll(revenueTask, bestsellersTask, lowStockTask);

                if (HasError)
                {
                    _logger.LogWarning("One or more reports failed to load. The error message is displayed.");
                }

            }, null);
        }

        private async Task LoadRevenueReportInternalAsync()
        {
            try
            {
                var response = await _reportApi.GetRevenueReport(StartDate.Date, EndDate.Date);
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        RevenueReport = response.Content;
                        CreateRevenueChart();
                        ShowRevenueReport = true;
                        ErrorMessage = null;
                    });
                }
                else
                {
                    var error = response.Error?.Content ?? "Revenue report failed";
                    _logger.LogWarning("Revenue report failed: {Error}", error);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ErrorMessage = error;
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception loading revenue report.");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ErrorMessage = ex.Message;
                });
            }
        }

        private void CreateRevenueChart()
        {
            if (RevenueReport?.DailyRevenue == null || !RevenueReport.DailyRevenue.Any())
            {
                RevenueChart = CreateEmptyChart();
                return;
            }
            var entries = RevenueReport.DailyRevenue.Select(dailyData => new ChartEntry((float)dailyData.TotalRevenue)
            {
                Label = dailyData.Date.ToString("dd/MM"),
                ValueLabel = dailyData.TotalRevenue.ToString("N0"),
                Color = SKColor.Parse("#2196F3")
            }).ToList();
            RevenueChart = new LineChart
            {
                Entries = entries,
                LineMode = LineMode.Spline,
                PointMode = PointMode.Circle,
                PointSize = 10f,
                LabelTextSize = 30f,
                LabelOrientation = Orientation.Horizontal,
                ValueLabelOrientation = Orientation.Horizontal,
                ValueLabelTextSize = 25f,
                MinValue = 0
            };
        }

        private async Task LoadBestsellersReportInternalAsync()
        {
            try
            {
                var response = await _reportApi.GetBestsellersReport(StartDate.Date, EndDate.Date, top: 7);
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        BestsellersData = response.Content.ToList();
                        CreateBestsellersChart();
                        ShowBestsellersReport = BestsellersData.Any();
                        ErrorMessage = null;
                    });
                }
                else
                {
                    var error = $"Failed to load bestsellers. Status: {response.StatusCode}";
                    _logger.LogWarning(error);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ErrorMessage = error;
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception loading bestsellers report.");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ErrorMessage = ex.Message;
                });
            }
        }

        private void CreateBestsellersChart()
        {
            if (BestsellersData == null || !BestsellersData.Any())
            {
                BestsellersChart = CreateEmptyChart(isBarChart: true);
                return;
            }
            var entries = BestsellersData.Select(book => new ChartEntry(book.TotalQuantitySold)
            {
                Label = book.BookTitle.Length > 15 ? book.BookTitle.Substring(0, 12) + "..." : book.BookTitle,
                ValueLabel = book.TotalQuantitySold.ToString(),
                Color = SKColor.Parse("#4CAF50")
            }).ToList();
            BestsellersChart = new BarChart
            {
                Entries = entries,
                LabelTextSize = 25f,
                ValueLabelOrientation = Orientation.Horizontal,
                LabelOrientation = Orientation.Horizontal,
                IsAnimated = true
            };
        }

        private async Task LoadLowStockReportInternalAsync()
        {
            try
            {
                var response = await _reportApi.GetLowStockReport(LowStockThreshold);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LowStockBooks.Clear();
                    if (response.IsSuccessStatusCode && response.Content != null)
                    {
                        foreach (var book in response.Content) LowStockBooks.Add(book);
                        ErrorMessage = null;
                    }
                    else
                    {
                        var error = $"Failed to load low stock report. Status: {response.StatusCode}";
                        _logger.LogWarning(error);
                        ErrorMessage = error;
                    }
                    ShowLowStockReport = true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception loading low stock report.");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ErrorMessage = ex.Message;
                });
            }
        }

        [RelayCommand]
        private async Task RefreshAllReports()
        {
            await LoadAllReportsInternalAsync(true);
        }

        [RelayCommand]
        private async Task ReloadLowStock()
        {
            await RunSafeAsync(LoadLowStockReportInternalAsync, null);
        }

        public void OnAppearing()
        {
            if (!ShowRevenueReport)
            {
                LoadAllReportsCommand.Execute(null);
            }
        }

        private Chart CreateEmptyChart(bool isBarChart = false)
        {
            var placeholderEntry = new List<ChartEntry>
    {
        new ChartEntry(0.001f) { Label = "No Data", ValueLabel = " ", Color = SKColors.LightGray }
    };

            if (isBarChart)
            {
                var barChart = new BarChart
                {
                    Entries = placeholderEntry,
                    LabelTextSize = 25f,
                    IsAnimated = false,
                    MinValue = 0,
                    MaxValue = 1
                };
                return barChart;
            }
            else
            {
                var lineChart = new LineChart
                {
                    Entries = placeholderEntry,
                    LabelTextSize = 25f,
                    IsAnimated = false,
                    MinValue = 0,
                    MaxValue = 1
                };
                return lineChart;
            }
        }
    }
}
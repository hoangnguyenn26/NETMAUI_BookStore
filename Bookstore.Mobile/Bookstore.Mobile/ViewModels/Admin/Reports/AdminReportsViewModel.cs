
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

        // ErrorMessage đã có trong BaseViewModel

        [RelayCommand]
        private async Task LoadAllReportsAsync(bool isRefreshing = false)
        {
            // *** SỬA Ở ĐÂY: Xóa tham số errorMessage ***
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

                // Kiểm tra lại xem có lỗi nào được ghi nhận bởi các task con không
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
                    });
                }
                else
                {
                    var error = response.Error?.Content ?? "Revenue report failed";
                    _logger.LogWarning("Revenue report failed: {Error}", error);
                    // Không throw lỗi để các task khác tiếp tục
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception loading revenue report.");
                // Không throw lỗi
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
                    });
                }
                else
                {
                    _logger.LogWarning("Failed to load bestsellers. Status: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception loading bestsellers report.");
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
                    }
                    else
                    {
                        _logger.LogWarning("Failed to load low stock report. Status: {StatusCode}", response.StatusCode);
                    }
                    ShowLowStockReport = true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception loading low stock report.");
            }
        }

        [RelayCommand]
        private async Task RefreshAllReports()
        {
            await LoadAllReportsAsync(true);
        }

        [RelayCommand]
        private async Task ReloadLowStock()
        {
            // *** SỬA Ở ĐÂY: Xóa tham số errorMessage ***
            await RunSafeAsync(LoadLowStockReportInternalAsync, null);
        }

        public void OnAppearing()
        {
            if (!ShowRevenueReport)
            {
                LoadAllReportsCommand.Execute(false);
            }
        }

        private Chart CreateEmptyChart(bool isBarChart = false)
        {
            var placeholderEntry = new List<ChartEntry>
    {
        // Thêm một entry với giá trị 0.001f để thư viện có MaxValue > MinValue
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
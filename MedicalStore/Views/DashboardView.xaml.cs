using MedicalStore.Common.Helpers;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace MedicalStore.Views
{
    public partial class DashboardView : Page
    {
        private readonly DashboardService _dashboardService = new();
        private readonly SaleService _saleService = new();

        public DashboardView()
        {
            InitializeComponent();
            Loaded += DashboardView_Loaded;
        }

        private void DashboardView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            LoadAll();
        }

        private void BtnRefresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            LoadAll();
        }

        private void LoadAll()
        {
            try
            {
                if (runLastUpdated != null)
                    runLastUpdated.Text = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");

                LoadSummaryCards();
                LoadDailySalesChart();
                LoadCategoryChart();
                LoadSalesVsPurchasesChart();
                LoadRecentSales();
            }
            catch { }
        }

        private void LoadSummaryCards()
        {
            try
            {
                var todaySales = _dashboardService.GetTodaySales();
                var todayProfit = _dashboardService.GetTodayProfit();
                var todayPurchases = _dashboardService.GetTodayPurchases();
                var todayReturns = _dashboardService.GetTodayReturns();
                var totalDues = _dashboardService.GetTotalDues();

                lblTodaySales.Text = $"{(todaySales).FormatRs()}";
                lblTodayProfit.Text = $"{(todayProfit).FormatRs()}";
                lblTodayPurchases.Text = $"{(todayPurchases).FormatRs()}";
                lblTodayReturns.Text = $"{(todayReturns).FormatRs()}";
                lblDues.Text = $"{(totalDues).FormatRs()}";

                lblTotalProducts.Text = _dashboardService.GetTotalProducts().ToString();
                lblLowStock.Text = _dashboardService.GetLowStockCount().ToString();
                lblNearExpiry.Text = _dashboardService.GetNearExpiryCount().ToString();

                // Count today's sales for the subtitle
                if (lblTodaySalesCount != null)
                {
                    try
                    {
                        var today = DateTime.Now.Date;
                        var count = _saleService.GetAll().Count(s => s.Date.Date == today);
                        lblTodaySalesCount.Text = $"{count} transaction{(count == 1 ? "" : "s")}";
                    }
                    catch { lblTodaySalesCount.Text = ""; }
                }
            }
            catch { }
        }

        private void LoadDailySalesChart()
        {
            try
            {
                var data = _dashboardService.GetDailySales(30);
                chartDailySales.Series = new ISeries[]
                {
                    new LineSeries<decimal>
                    {
                        Values = data.Select(d => d.Amount).ToArray(),
                        Name = "Sales",
                        Fill = new SolidColorPaint(SKColor.Parse("#1A73E8").WithAlpha(30)),
                        Stroke = new SolidColorPaint(SKColor.Parse("#1A73E8"), 2),
                        GeometrySize = 6,
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColor.Parse("#1A73E8"), 2)
                    }
                };
                chartDailySales.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = data.Select(d => d.Date.ToString("dd")).ToArray(),
                        LabelsRotation = 0,
                        TextSize = 10
                    }
                };
                chartDailySales.YAxes = new Axis[]
                {
                    new Axis { TextSize = 10, Labeler = v => $"Rs {v/1000:N0}k" }
                };
            }
            catch { }
        }

        private void LoadCategoryChart()
        {
            try
            {
                var data = _dashboardService.GetCategorySales();
                if (data.Count == 0)
                    data = new List<(string Category, decimal Amount)> { ("No Data", 1) };

                var colors = new[] {
                    "#1A73E8", "#FF9800", "#4CAF50", "#E91E63", "#9C27B0",
                    "#00BCD4", "#FF5722", "#795548", "#607D8B", "#F44336"
                };
                int ci = 0;
                chartCategory.Series = data.Select(d =>
                    new PieSeries<decimal>
                    {
                        Values = new[] { d.Amount },
                        Name = d.Category,
                        Fill = new SolidColorPaint(SKColor.Parse(colors[ci++ % colors.Length]))
                    } as ISeries).ToArray();
            }
            catch { }
        }

        private void LoadSalesVsPurchasesChart()
        {
            try
            {
                var data = _dashboardService.GetMonthlySalesVsPurchases(6);
                chartSalesVsPurchases.Series = new ISeries[]
                {
                    new ColumnSeries<decimal>
                    {
                        Values = data.Select(d => d.Sales).ToArray(),
                        Name = "Sales",
                        Fill = new SolidColorPaint(SKColor.Parse("#1A73E8"))
                    },
                    new ColumnSeries<decimal>
                    {
                        Values = data.Select(d => d.Purchases).ToArray(),
                        Name = "Purchases",
                        Fill = new SolidColorPaint(SKColor.Parse("#FF9800"))
                    }
                };
                chartSalesVsPurchases.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = data.Select(d => d.Month).ToArray(),
                        TextSize = 11
                    }
                };
            }
            catch { }
        }

        private void LoadRecentSales()
        {
            try
            {
                var sales = _saleService.GetAll().Take(10).ToList();
                dgRecentSales.ItemsSource = sales;
            }
            catch { }
        }
    }
}

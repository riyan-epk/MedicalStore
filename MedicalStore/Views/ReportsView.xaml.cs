using MedicalStore.Common.Helpers;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Threading.Tasks;
using MedicalStore.Common.Models;
using MedicalStore.Helpers;

namespace MedicalStore.Views
{
    public partial class ReportsView : Page
    {
        private readonly SaleService     _saleService     = new();
        private readonly ProductService  _productService  = new();
        private readonly PurchaseService _purchaseService = new();
        private readonly CustomerService _customerService = new();
        private readonly ReturnService   _returnService   = new();
        private readonly ReportService   _reportService   = new();
        private readonly ExportService   _exportService   = new();
        private readonly FinancialService _financialService = new();

        private readonly SupplierService _supplierService = new();

        private List<Sale>     _currentSales     = new();
        private List<Return>   _currentReturns   = new();
        private List<Customer> _currentCustomers = new();
        private List<Supplier> _currentSuppliers = new();

        public ReportsView()
        {
            InitializeComponent();
            // Default: first day of current month today
            dpFrom.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            dpTo.SelectedDate   = DateTime.Now;

            LoadComboBoxes();
        }

        private void LoadComboBoxes()
        {
            cbCustomerDetail.ItemsSource = _customerService.GetAll();
            cbSupplierDetail.ItemsSource = _supplierService.GetAll();
        }

        // Quick Date-Range Shortcuts
        private void QuickToday_Click(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = DateTime.Today;
            dpTo.SelectedDate   = DateTime.Today;
            Generate_Click(sender, e);
        }

        private void QuickWeek_Click(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            dpTo.SelectedDate   = DateTime.Today;
            Generate_Click(sender, e);
        }

        private void QuickMonth_Click(object sender, RoutedEventArgs e)
        {
            var now = DateTime.Now;
            dpFrom.SelectedDate = new DateTime(now.Year, now.Month, 1);
            dpTo.SelectedDate   = DateTime.Today;
            Generate_Click(sender, e);
        }

        private void QuickYear_Click(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = new DateTime(DateTime.Now.Year, 1, 1);
            dpTo.SelectedDate   = DateTime.Today;
            Generate_Click(sender, e);
        }

        // Generate Report
        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            HideBanner();
            try
            {
                var from = dpFrom.SelectedDate ?? DateTime.Today;
                var to   = (dpTo.SelectedDate ?? DateTime.Today).AddDays(1);

                // Sales
                _currentSales = _saleService.GetByDateRange(from, to);
                dgSalesReport.ItemsSource = _currentSales;

                lblReportSales.Text        = $"{(_currentSales.Sum(s => s.NetAmount)).FormatRs()}";
                lblReportDiscounts.Text    = $"{(_currentSales.Sum(s => s.Discount)).FormatRs()}";
                lblReportTransactions.Text = _currentSales.Count.ToString();

                // Purchases
                var purchases = _purchaseService.GetByDateRange(from, to);
                dgPurchaseReport.ItemsSource = purchases;

                if (lblPurchTotal != null)
                {
                    lblPurchTotal.Text = $"{(purchases.Sum(p => p.TotalAmount)).FormatRs()}";
                    lblPurchPaid.Text  = $"{(purchases.Sum(p => p.PaidAmount)).FormatRs()}";
                    lblPurchDue.Text   = $"{(purchases.Sum(p => p.DueAmount)).FormatRs()}";
                }

                // Inventory
                dgInventoryReport.ItemsSource = _productService.GetAll();

                // Financial P&L
                var pl = _financialService.GetProfitLoss(from, to);
                lblFinSales.Text     = $"{(pl.Sales).FormatRs()}";
                lblFinPurchases.Text = $"{(pl.Purchases).FormatRs()}";
                lblFinRefunds.Text   = $"{(pl.Refunds).FormatRs()}";
                lblFinDiscounts.Text = $"{(pl.Discounts).FormatRs()}";
                
                if (lblFinExpenses != null)
                    lblFinExpenses.Text = $"{(pl.Expenses).FormatRs()}";
                
                lblFinProfit.Text    = $"{(pl.Profit).FormatRs()}";

                // Customer Balances
                // Show all customers (including 0 balance) ordered by amount owed
                _currentCustomers = _customerService.GetAll().OrderByDescending(c => Math.Abs(c.Balance)).ToList();
                dgCustomerReport.ItemsSource = _currentCustomers;

                decimal totalDue    = _currentCustomers.Where(c => c.Balance > 0).Sum(c => c.Balance);
                decimal totalCredit = _currentCustomers.Where(c => c.Balance < 0).Sum(c => Math.Abs(c.Balance));

                if (lblCustTotalDue    != null) lblCustTotalDue.Text    = $"{(totalDue).FormatRs()}";
                if (lblCustTotalCredit != null) lblCustTotalCredit.Text = $"{(totalCredit).FormatRs()}";
                if (lblCustTotalPaid   != null) lblCustTotalPaid.Text   = $"{_currentCustomers.Count(c => c.Balance == 0)} customers";

                // Supplier Balances
                _currentSuppliers = _supplierService.GetAll().OrderByDescending(s => Math.Abs(s.Balance)).ToList();
                if (dgSupplierReport != null)
                    dgSupplierReport.ItemsSource = _currentSuppliers;
                
                decimal supTotalDue    = _currentSuppliers.Where(s => s.Balance > 0).Sum(s => s.Balance);
                decimal supTotalCredit = _currentSuppliers.Where(s => s.Balance < 0).Sum(s => Math.Abs(s.Balance));

                if (lblSupTotalDue    != null) lblSupTotalDue.Text    = $"{(supTotalDue).FormatRs()}";
                if (lblSupTotalCredit != null) lblSupTotalCredit.Text = $"{(supTotalCredit).FormatRs()}";
                if (lblSupTotalPaid   != null) lblSupTotalPaid.Text   = $"{_currentSuppliers.Count(s => s.Balance == 0)} suppliers";

                // Returns
                _currentReturns = _returnService.GetByDateRange(from, to);
                dgReturnReport.ItemsSource = _currentReturns;

                if (lblRetCount != null)
                {
                    lblRetCount.Text   = _currentReturns.Count.ToString();
                    lblRetTotal.Text   = $"{(_currentReturns.Sum(r => r.TotalAmount)).FormatRs()}";
                    lblRetReplace.Text = $"{(_currentReturns.Sum(r => r.ReplaceAmount)).FormatRs()}";
                    lblRetRefund.Text  = $"{(_currentReturns.Sum(r => r.RefundAmount)).FormatRs()}";
                }

                ShowBanner($"Report generated for {from:dd/MM/yyyy} {(to.AddDays(-1)):dd/MM/yyyy}", isSuccess: true);
            }
            catch (Exception ex)
            {
                // Show friendly inline banner instead of a raw popup
                ShowBanner($"Error generating report: {ex.Message}", isSuccess: false);
            }
        }

        // Inventory Filters
        private void AllStock_Click(object sender, RoutedEventArgs e)
            => dgInventoryReport.ItemsSource = _productService.GetAll();

        private void LowStockReport_Click(object sender, RoutedEventArgs e)
            => dgInventoryReport.ItemsSource = _productService.GetLowStock();

        private void NearExpiryReport_Click(object sender, RoutedEventArgs e)
            => dgInventoryReport.ItemsSource = _productService.GetNearExpiry();

        private void ExpiredReport_Click(object sender, RoutedEventArgs e)
            => dgInventoryReport.ItemsSource = _productService.GetExpired();

        private void ExportSalesCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSales == null || !_currentSales.Any()) { ShowBanner("No sales data to export.", false); return; }
            var ledgerData = _currentSales.Select(s => new MedicalStore.Common.Models.LedgerEntry 
            { 
                Date = s.Date, Type = "Sale", Reference = s.InvoiceNo, Debit = s.NetAmount, Credit = s.PaidAmount, 
                Notes = string.Join(", ", s.Items.Select(i => i.Product?.Name)) 
            });
            _exportService.ExportToCsv(ledgerData, "SalesReport");
            ShowBanner("Sales report exported.", true);
        }

        private void ExportInventoryCsv_Click(object sender, RoutedEventArgs e)
        {
            var products = dgInventoryReport.ItemsSource as IEnumerable<Product>;
            if (products == null) return;
            var ledgerData = products.Select(p => new MedicalStore.Common.Models.LedgerEntry 
            { 
                Date = DateTime.Now, Type = "Stock", Reference = p.Id.ToString(), Debit = 0, Credit = 0, 
                Notes = $"Name: {p.Name}, Stock: {p.StockUnits}, Price: {p.UnitPrice}" 
            });
            _exportService.ExportToCsv(ledgerData, "InventoryReport");
            ShowBanner("Inventory exported.", true);
        }

        private void ExportLowStockCsv_Click(object sender, RoutedEventArgs e)
        {
            var products = _productService.GetLowStock();
            var ledgerData = products.Select(p => new MedicalStore.Common.Models.LedgerEntry 
            { 
                Date = DateTime.Now, Type = "LowStock", Reference = p.Id.ToString(), Debit = 0, Credit = 0, 
                Notes = $"Name: {p.Name}, Current Stock: {p.StockUnits}" 
            });
            _exportService.ExportToCsv(ledgerData, "LowStockReport");
            ShowBanner("Low stock report exported.", true);
        }

        // Banner Helpers
        private int _bannerId = 0;

        private async void ShowBanner(string message, bool isSuccess)
        {
            if (bannerStatus == null || txtBannerMsg == null) return;
            txtBannerMsg.Text = message;
            
            if (bannerIcon != null)
            {
                bannerIcon.Kind = isSuccess ? MahApps.Metro.IconPacks.PackIconMaterialKind.CheckCircle : MahApps.Metro.IconPacks.PackIconMaterialKind.AlertCircle;
                bannerIcon.Foreground = isSuccess ? Brushes.DarkGreen : Brushes.DarkRed;
            }

            bannerStatus.Background = isSuccess
                ? new SolidColorBrush(Color.FromRgb(232, 245, 233))   // light green
                : new SolidColorBrush(Color.FromRgb(255, 235, 238));  // light red
            
            bannerStatus.BorderBrush = isSuccess
                ? new SolidColorBrush(Color.FromRgb(165, 214, 167))
                : new SolidColorBrush(Color.FromRgb(239, 154, 154));
            
            bannerStatus.Visibility = Visibility.Visible;

            _bannerId++;
            int currentId = _bannerId;

            await Task.Delay(3500);
            if (_bannerId == currentId && bannerStatus != null)
            {
                bannerStatus.Visibility = Visibility.Collapsed;
            }
        }

        private void HideBanner()
        {
            if (bannerStatus != null) bannerStatus.Visibility = Visibility.Collapsed;
        }

        // ── Download PDF Handlers ──────────────────────────────────────────────
        private string GetPeriodText() 
            => $"{dpFrom.SelectedDate:dd/MM/yyyy} - {dpTo.SelectedDate:dd/MM/yyyy}";

        private void DownloadSalesPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSales == null || !_currentSales.Any()) { ShowBanner("No data to export.", false); return; }
            ShowBanner("Generating PDF...", true);
            _exportService.ExportSalesPdf(_currentSales, GetPeriodText());
        }

        private void DownloadInventoryPdf_Click(object sender, RoutedEventArgs e)
        {
            var products = dgInventoryReport.ItemsSource as IEnumerable<Product>;
            if (products == null || !products.Any()) { ShowBanner("No data to export.", false); return; }
            ShowBanner("Generating PDF...", true);
            _exportService.ExportInventoryPdf(products);
        }

        private void DownloadFinancialPdf_Click(object sender, RoutedEventArgs e)
        {
            var from = dpFrom.SelectedDate ?? DateTime.Today;
            var to   = (dpTo.SelectedDate ?? DateTime.Today).AddDays(1);
            var pl = _financialService.GetProfitLoss(from, to);
            
            ShowBanner("Generating PDF...", true);
            _exportService.ExportFinancialPdf(pl.Sales, pl.Purchases, pl.Refunds, pl.Discounts, pl.Expenses, pl.Profit, GetPeriodText());
        }

        private void DownloadPurchasesPdf_Click(object sender, RoutedEventArgs e)
        {
            var purchases = dgPurchaseReport.ItemsSource as IEnumerable<Purchase>;
            if(purchases == null || !purchases.Any()) { ShowBanner("No data to export.", false); return; }
            ShowBanner("Generating PDF...", true);
            _exportService.ExportPurchasesPdf(purchases, GetPeriodText());
        }

        private void DownloadReturnsPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReturns == null || !_currentReturns.Any()) { ShowBanner("No data to export.", false); return; }
            ShowBanner("Generating PDF...", true);
            _exportService.ExportReturnsPdf(_currentReturns, GetPeriodText());
        }


        // ── Customer Detail Report ───────────────────────────────────────────
        private void CbCustomerDetail_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbCustomerDetail.SelectedItem is Customer c)
            {
                var from = dpFrom.SelectedDate ?? DateTime.Today;
                var to   = (dpTo.SelectedDate ?? DateTime.Today).AddDays(1);
                var data = _reportService.GetCustomerTransactionReport(c.Id, from, to);
                dgCustomerReport.ItemsSource = data;

                decimal netOwed = c.Balance;
                lblCustTotalDue.Text    = netOwed > 0 ? netOwed.FormatRs() : "Rs 0";
                lblCustTotalCredit.Text = netOwed < 0 ? Math.Abs(netOwed).FormatRs() : "Rs 0";
                lblCustTotalPaid.Text   = (data.Where(x => x.Type == "Payment").Sum(x => x.Credit)).FormatRs();
            }
        }

        private void ExportCustomerCsv_Click(object sender, RoutedEventArgs e)
        {
            if (cbCustomerDetail.SelectedItem is Customer c && dgCustomerReport.ItemsSource is IEnumerable<LedgerEntry> data)
            {
                _exportService.ExportToCsv(data, $"CustomerDetail_{c.Name}");
                ShowBanner("Report exported successfully.", true);
            }
            else ShowBanner("Please select a customer first.", false);
        }

        private void ExportCustomerExcel_Click(object sender, RoutedEventArgs e) => ExportCustomerCsv_Click(sender, e);

        private void DownloadCustomerPdf_Click(object sender, RoutedEventArgs e)
        {
            if (cbCustomerDetail.SelectedItem is Customer c)
            {
                var from = dpFrom.SelectedDate ?? DateTime.Today;
                var to   = (dpTo.SelectedDate ?? DateTime.Today).AddDays(1);
                var sales = _saleService.GetByDateRange(from, to).Where(s => s.CustomerId == c.Id).ToList();
                using var db = new MedicalStore.DAL.AppDbContext();
                var payments = db.Payments.Where(p => p.CustomerId == c.Id && p.Date >= from && p.Date < to).ToList();

                ShowBanner("Generating PDF...", true);
                _exportService.ExportCustomerPdf(c, sales, payments, GetPeriodText());
            }
            else ShowBanner("Please select a customer first.", false);
        }

        // ── Supplier Detail Report ───────────────────────────────────────────
        private void CbSupplierDetail_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbSupplierDetail.SelectedItem is Supplier s)
            {
                var from = dpFrom.SelectedDate ?? DateTime.Today;
                var to   = (dpTo.SelectedDate ?? DateTime.Today).AddDays(1);
                var data = _reportService.GetSupplierTransactionReport(s.Id, from, to);
                dgSupplierReport.ItemsSource = data;

                decimal netOwed = s.Balance;
                lblSupTotalDue.Text    = netOwed > 0 ? netOwed.FormatRs() : "Rs 0";
                lblSupTotalCredit.Text = netOwed < 0 ? Math.Abs(netOwed).FormatRs() : "Rs 0";
                lblSupTotalPaid.Text   = (data.Where(x => x.Type == "Payment").Sum(x => x.Debit)).FormatRs();
            }
        }

        private void ExportSupplierCsv_Click(object sender, RoutedEventArgs e)
        {
            if (cbSupplierDetail.SelectedItem is Supplier s && dgSupplierReport.ItemsSource is IEnumerable<LedgerEntry> data)
            {
                _exportService.ExportToCsv(data, $"SupplierDetail_{s.Name}");
                ShowBanner("Report exported successfully.", true);
            }
            else ShowBanner("Please select a supplier first.", false);
        }

        private void ExportSupplierExcel_Click(object sender, RoutedEventArgs e) => ExportSupplierCsv_Click(sender, e);

        private void DownloadSupplierPdf_Click(object sender, RoutedEventArgs e)
        {
            if (cbSupplierDetail.SelectedItem is Supplier s)
            {
                var from = dpFrom.SelectedDate ?? DateTime.Today;
                var to   = (dpTo.SelectedDate ?? DateTime.Today).AddDays(1);
                var purchases = _purchaseService.GetByDateRange(from, to).Where(p => p.SupplierId == s.Id).ToList();
                using var db = new MedicalStore.DAL.AppDbContext();
                var payments = db.SupplierPayments.Where(p => p.SupplierId == s.Id && p.Date >= from && p.Date < to).ToList();

                ShowBanner("Generating PDF...", true);
                _exportService.ExportSupplierPdf(s, purchases, payments, GetPeriodText());
            }
            else ShowBanner("Please select a supplier first.", false);
        }
    }
}

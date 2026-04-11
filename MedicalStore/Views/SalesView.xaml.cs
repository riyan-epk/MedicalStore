using MedicalStore.Common.Helpers;
using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class SalesView : Page
    {
        private readonly SaleService _saleService = new();
        private readonly CustomerService _customerService = new();

        public SalesView()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            var customers = _customerService.GetAll();
            customers.Insert(0, new Customer { Id = 0, Name = "All Customers" });
            cmbCustomer.ItemsSource = customers;
            cmbCustomer.SelectedIndex = 0;

            dpFrom.SelectedDate = DateTime.Now.Date;
            dpTo.SelectedDate = DateTime.Now.Date;

            SearchSales();
            LoadTodayTotals();
        }

        private void LoadTodayTotals()
        {
            var today = DateTime.Now.Date;
            var salesToday = _saleService.GetByDateRange(today, today.AddDays(1).AddTicks(-1));

            lblTodaySales.Text = $"{(salesToday.Sum(s => s.NetAmount)).FormatRs()}";
            lblTodayProfit.Text = $"{(salesToday.Sum(s => s.Profit)).FormatRs()}";
            lblTodayDue.Text = $"{(salesToday.Sum(s => s.DueAmount)).FormatRs()}";
        }

        private void SearchSales()
        {
            var from = dpFrom.SelectedDate ?? DateTime.MinValue;
            var to = dpTo.SelectedDate ?? DateTime.MaxValue;
            if (to != DateTime.MaxValue) to = to.AddDays(1).AddTicks(-1);

            var sales = _saleService.GetByDateRange(from, to);

            if (cmbCustomer.SelectedValue != null && (int)cmbCustomer.SelectedValue > 0)
            {
                int customerId = (int)cmbCustomer.SelectedValue;
                sales = sales.Where(s => s.CustomerId == customerId).ToList();
            }

            var invoice = txtInvoice.Text.Trim();
            if (!string.IsNullOrEmpty(invoice))
            {
                sales = sales.Where(s => s.InvoiceNo.Contains(invoice, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            dgSales.ItemsSource = sales;
            if (lblSalesCount != null)
                lblSalesCount.Text = $"{sales.Count} record{(sales.Count == 1 ? "" : "s")}";
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchSales();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = DateTime.Now.Date;
            dpTo.SelectedDate = DateTime.Now.Date;
            cmbCustomer.SelectedIndex = 0;
            txtInvoice.Text = "";
            SearchSales();
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Sale sale)
            {
                if (Window.GetWindow(this) is MainWindow mw)
                {
                    var returnsView = new ReturnsView(sale.InvoiceNo);
                    mw.MainFrame.Navigate(returnsView);
                }
            }
        }
    }
}

using System.Windows;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;
using MedicalStore.DAL;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.Views
{
    public partial class SupplierHistoryDialog : Window
    {
        public SupplierHistoryDialog(Supplier supplier)
        {
            InitializeComponent();
            lblSupplierName.Text = supplier.Name;
            lblTotalDue.Text = $"Total Outstanding: Rs {supplier.Balance:N2}";
            LoadHistory(supplier.Id);
        }

        private void LoadHistory(int supplierId)
        {
            using var db = new AppDbContext();
            dgInvoices.ItemsSource = db.Purchases
                .Where(p => p.SupplierId == supplierId)
                .OrderByDescending(p => p.Date)
                .ToList();

            dgPayments.ItemsSource = db.SupplierPayments
                .Where(p => p.SupplierId == supplierId)
                .OrderByDescending(p => p.Date)
                .ToList();
        }
    }
}

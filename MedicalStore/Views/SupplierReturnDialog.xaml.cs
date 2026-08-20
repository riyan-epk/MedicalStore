using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class SupplierReturnDialog : Window
    {
        private readonly SupplierService _supplierService = new();
        private readonly ProductService _productService = new();
        private readonly Supplier _supplier;

        public class ReturnRow
        {
            public int ProductId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int StockUnits { get; set; }
            public decimal Cost { get; set; }
            public int ReturnQty { get; set; }
        }

        private readonly ObservableCollection<ReturnRow> _rows = new();

        public SupplierReturnDialog(Supplier supplier)
        {
            InitializeComponent();
            _supplier = supplier;
            lblSupplierName.Text = supplier.Name;

            var products = _productService.GetAll()
                .Where(p => p.SupplierId == supplier.Id && (p.StockUnits > 0 || p.Quantity > 0))
                .OrderBy(p => p.Name);

            foreach (var p in products)
            {
                _rows.Add(new ReturnRow
                {
                    ProductId  = p.Id,
                    Name       = p.Name,
                    StockUnits = p.StockUnits > 0 ? p.StockUnits : p.Quantity,
                    Cost       = p.PurchasePrice,
                    ReturnQty  = 0
                });
            }

            dgItems.ItemsSource = _rows;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            dgItems.CommitEdit();

            var items = _rows
                .Where(r => r.ReturnQty > 0)
                .Select(r => (r.ProductId, r.ReturnQty))
                .ToList();

            if (items.Count == 0)
            {
                MessageBox.Show("Enter a Return Qty for at least one item.", "Nothing to Return");
                return;
            }

            var tooMany = _rows.FirstOrDefault(r => r.ReturnQty > r.StockUnits);
            if (tooMany != null)
            {
                MessageBox.Show($"Return Qty for {tooMany.Name} exceeds stock ({tooMany.StockUnits}).", "Invalid Quantity");
                return;
            }

            var result = _supplierService.ReturnToSupplier(_supplier.Id, items, txtNotes.Text);
            if (result.Success)
            {
                MessageBox.Show(result.Message, "Done");
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(result.Message, "Error");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

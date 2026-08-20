using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class ReceivePurchaseDialog : Window
    {
        private readonly PurchaseService _purchaseService = new();
        private readonly int _purchaseId;

        public class Row
        {
            public int ItemId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Ordered { get; set; }
            public int Received { get; set; }
            public int Remaining { get; set; }
            public int ReceiveNow { get; set; }
        }

        private readonly ObservableCollection<Row> _rows = new();

        public ReceivePurchaseDialog(Purchase purchase)
        {
            InitializeComponent();
            _purchaseId = purchase.Id;
            lblInfo.Text = $"{purchase.Supplier?.Name}  •  Order #{purchase.Id}  •  Status: {purchase.Status}";

            foreach (var it in purchase.Items)
            {
                int remaining = it.TotalUnits - it.ReceivedUnits;
                _rows.Add(new Row
                {
                    ItemId     = it.Id,
                    Name       = it.Product?.Name ?? $"#{it.ProductId}",
                    Ordered    = it.TotalUnits,
                    Received   = it.ReceivedUnits,
                    Remaining  = remaining,
                    ReceiveNow = remaining > 0 ? remaining : 0
                });
            }

            dgItems.ItemsSource = _rows;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            dgItems.CommitEdit();

            var over = _rows.FirstOrDefault(r => r.ReceiveNow > r.Remaining);
            if (over != null)
            {
                MessageBox.Show($"'Receive Now' for {over.Name} exceeds the remaining {over.Remaining}.", "Invalid Quantity");
                return;
            }

            var receipts = _rows
                .Where(r => r.ReceiveNow > 0)
                .Select(r => (r.ItemId, r.ReceiveNow))
                .ToList();

            if (receipts.Count == 0)
            {
                MessageBox.Show("Enter a quantity to receive for at least one line.", "Nothing to Receive");
                return;
            }

            var result = _purchaseService.ReceivePurchase(_purchaseId, receipts);
            if (result.Success)
            {
                MessageBox.Show(result.Message, "Received");
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

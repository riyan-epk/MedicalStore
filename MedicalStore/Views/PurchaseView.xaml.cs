using MedicalStore.Common.Constants;
using MedicalStore.Common.Helpers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    /// <summary>
    /// Cart item model for the purchase form.
    /// Supports both pack-based and direct-quantity entry.
    /// </summary>
    public class PurchaseCartItem
    {
        public int     ProductId     { get; set; }
        public string  ProductName   { get; set; } = "";

        // Pack fields
        /// <summary>Number of packs/strips purchased (0 = direct qty entry).</summary>
        public int     NumberOfPacks { get; set; }
        /// <summary>Units per pack snapshot.</summary>
        public int     UnitsPerPack  { get; set; } = 1;
        /// <summary>Price paid per pack.</summary>
        public decimal PackPrice     { get; set; }

        // Derived / overrideable fields
        /// <summary>Total units = NumberOfPacks UnitsPerPack (or manual qty when no packs).</summary>
        public int     TotalUnits    { get; set; }
        /// <summary>Calculated unit purchase price = PackPrice UnitsPerPack.</summary>
        public decimal UnitPrice     { get; set; }
        /// <summary>Optional sale price override stored at purchase time.</summary>
        public decimal NewSalePrice  { get; set; }

        // Display helpers
        public decimal LineTotal => TotalUnits * UnitPrice;

        // Legacy compat
        // PurchaseService uses PackQty
        public int     PackQty       => NumberOfPacks > 0 ? NumberOfPacks : TotalUnits;
    }

    public partial class PurchaseView : Page
    {
        private readonly SupplierService  _supplierService  = new();
        private readonly ProductService   _productService   = new();
        private readonly PurchaseService  _purchaseService  = new();
        private ObservableCollection<PurchaseCartItem> _cart = new();

        // Flag to prevent recursive TextChanged loops
        private bool _isCalculating = false;

        public PurchaseView()
        {
            InitializeComponent();
            dgPurchaseItems.ItemsSource = _cart;
            LoadData();
        }

        // Data loading
        private void LoadData()
        {
            cmbSupplier.ItemsSource = _supplierService.GetAll();
            cmbProduct.ItemsSource  = _productService.GetAll();

            if (cmbSupplier.Items.Count > 0 && cmbSupplier.SelectedIndex == -1) cmbSupplier.SelectedIndex = 0;
            if (cmbProduct.Items.Count  > 0 && cmbProduct.SelectedIndex  == -1) cmbProduct.SelectedIndex  = 0;

            RefreshHistory();
            UpdateSupplierBalance();
        }

        private void RefreshHistory()
        {
            dgPurchaseHistory.ItemsSource = _purchaseService.GetAll();
            dgRecentItems.ItemsSource     = _purchaseService.GetRecentPurchaseItems(60);

            var today     = DateTime.Now.Date;
            var tomorrow  = today.AddDays(1).AddTicks(-1);
            var todayList = _purchaseService.GetByDateRange(today, tomorrow);
            lblTodayCount.Text  = todayList.Count.ToString();
            lblTodayAmount.Text = todayList.Sum(p => p.TotalAmount).FormatRs();
        }

        private void ReceivePurchase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is MedicalStore.DAL.Entities.Purchase row)
            {
                if (row.Status == "Received")
                {
                    MessageBox.Show("This order is already fully received.", "Already Received");
                    return;
                }
                if (row.Status == "Cancelled")
                {
                    MessageBox.Show("This order was cancelled.", "Cancelled");
                    return;
                }

                // Reload with items included for the dialog.
                var full = _purchaseService.GetPurchaseById(row.Id);
                if (full == null) { MessageBox.Show("Purchase not found.", "Error"); return; }

                var dialog = new ReceivePurchaseDialog(full) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() == true)
                {
                    RefreshHistory();
                    UpdateSupplierBalance();
                }
            }
        }

        private void CancelPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is MedicalStore.DAL.Entities.Purchase purchase)
            {
                var confirm = MessageBox.Show(
                    $"Cancel this purchase from {purchase.Supplier?.Name}?\n\n" +
                    $"Total: Rs {purchase.TotalAmount:N2}\n\n" +
                    "This removes the received units from stock and reverses the unpaid supplier balance. " +
                    "This cannot be undone.",
                    "Cancel Purchase", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                var result = _purchaseService.CancelPurchase(purchase.Id);
                MessageBox.Show(result.Message, result.Success ? "Done" : "Error",
                    MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
                if (result.Success)
                {
                    RefreshHistory();
                    UpdateSupplierBalance();
                }
            }
        }

        // Selection changed
        private void CmbSupplier_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateSupplierBalance();

        private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbProduct.SelectedItem is not Product p) return;

            _isCalculating = true;
            try
            {
                // Pre-fill units-per-pack and pack price from product master
                if (p.UnitsPerPack > 0)
                    txtUnitsPerPack.Text = p.UnitsPerPack.ToString();

                if (p.PackPrice > 0)
                    txtPackPrice.Text = p.PackPrice.ToString("N2");
                else if (p.PurchasePrice > 0)
                    txtPackPrice.Text = p.PurchasePrice.ToString("N2");

                // Reset number-of-packs to 1 as default
                txtNumberOfPacks.Text = "1";
            }
            finally
            {
                _isCalculating = false;
            }

            RecalculateAll();
        }

        private void UpdateSupplierBalance()
        {
            if (lblSupplierBalance == null) return;
            lblSupplierBalance.Text = cmbSupplier.SelectedItem is Supplier s
                ? $"Outstanding balance: Rs {s.Balance:N2}"
                : "";
        }

        // Live calculation handlers
        /// <summary>
        /// Fired when No. of Packs, Units/Pack, or Pack Price change.
        /// Recalculates Total Qty and Unit Price.
        /// </summary>
        private void PackCalc_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isCalculating) return;
            RecalculateAll();
        }

        /// <summary>
        /// Fired when the user manually edits Total Qty.
        /// Only recalculates line total does NOT re-derive packs.
        /// </summary>
        private void TotalQty_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isCalculating) return;
            RefreshInfoBar();
        }

        /// <summary>
        /// Fired when Unit Cost is manually edited (override mode).
        /// </summary>
        private void UnitCost_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isCalculating) return;
            RefreshInfoBar();
        }

        /// <summary>
        /// Master recalculation:
        ///   1. If NumberOfPacks AND UnitsPerPack are valid TotalQty = Packs UnitsPP
        ///   2. If PackPrice AND UnitsPerPack valid UnitPrice = PackPrice UnitsPP
        ///   3. Always refresh the info bar.
        /// </summary>
        private void RecalculateAll()
        {
            if (_isCalculating) return;
            _isCalculating = true;

            try
            {
                bool hasPacks = int.TryParse(txtNumberOfPacks?.Text, out int packs)  && packs > 0;
                bool hasUpp   = int.TryParse(txtUnitsPerPack?.Text,  out int upp)    && upp   > 0;
                bool hasPackP = decimal.TryParse(txtPackPrice?.Text,  out decimal packP) && packP > 0;

                // Step 1: Auto-fill Total Qty
                if (hasPacks && hasUpp)
                {
                    int totalQty = packs * upp;
                    if (txtTotalQty != null)
                        txtTotalQty.Text = totalQty.ToString();
                }

                // Step 2: Auto-fill Unit Price
                if (hasPackP && hasUpp && upp > 0)
                {
                    decimal unitPrice = packP / upp;
                    if (txtUnitCost != null)
                        txtUnitCost.Text = unitPrice.ToString("N4");
                }
                else if (!hasPackP && hasUpp)
                {
                    // Clear unit price when pack price is cleared
                    // (only if user hasn't typed something manually)
                }
            }
            finally
            {
                _isCalculating = false;
            }

            RefreshInfoBar();
        }

        /// <summary>Updates the three info badges (Unit Price, Total Units, Line Total).</summary>
        private void RefreshInfoBar()
        {
            if (lblCalcUnitPrice == null) return;

            int.TryParse(txtNumberOfPacks?.Text, out int packs);
            int.TryParse(txtUnitsPerPack?.Text,  out int upp);    if (upp < 1) upp = 1;
            decimal.TryParse(txtPackPrice?.Text,  out decimal packP);
            int.TryParse(txtTotalQty?.Text,       out int totalQty);
            decimal.TryParse(txtUnitCost?.Text,   out decimal unitPrice);

            // Effective unit price
            decimal effectiveUnitPrice = unitPrice > 0 ? unitPrice
                : (packP > 0 && upp > 0 ? packP / upp : 0);

            // Effective total units
            int effectiveTotalQty = totalQty > 0 ? totalQty
                : (packs > 0 && upp > 0 ? packs * upp : 0);

            decimal lineTotal = effectiveTotalQty * effectiveUnitPrice;

            lblCalcUnitPrice.Text   = effectiveUnitPrice > 0  ? $"Rs {effectiveUnitPrice:N2}" : "";
            lblCalcTotalUnits.Text  = effectiveTotalQty  > 0  ? effectiveTotalQty.ToString()  : "";
            lblCalcLineTotal.Text   = lineTotal           > 0  ? $"Rs {lineTotal:N2}"          : "";
        }

        // Cart operations
        private void AddPurchaseItem_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProduct.SelectedItem is not Product product)
            {
                MessageBox.Show("Please select a product.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse all fields
            int.TryParse(txtNumberOfPacks?.Text, out int numPacks);
            int.TryParse(txtUnitsPerPack?.Text,  out int upp);
            decimal.TryParse(txtPackPrice?.Text,  out decimal packPrice);
            int.TryParse(txtTotalQty?.Text,       out int totalQty);
            decimal.TryParse(txtUnitCost?.Text,   out decimal unitCost);
            decimal.TryParse(txtNewSalePrice?.Text, out decimal newSalePrice);

            // Determine effective values
            if (upp < 1) upp = 1;

            // Effective total units: prefer pack calculation, fall back to manual
            int effectiveTotalUnits;
            if (numPacks > 0 && upp > 0)
                effectiveTotalUnits = numPacks * upp;
            else if (totalQty > 0)
                effectiveTotalUnits = totalQty;
            else
            {
                MessageBox.Show(
                    "Enter either:\nNumber of Packs + Units/Pack\nOR a Total Qty directly.",
                    "Quantity Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Effective unit price: prefer pack unit derivation, fall back to manual
            decimal effectiveUnitPrice;
            if (packPrice > 0 && upp > 0)
                effectiveUnitPrice = packPrice / upp;
            else if (unitCost > 0)
                effectiveUnitPrice = unitCost;
            else
            {
                MessageBox.Show(
                    "Enter either:\nPack Price (auto-derives unit price)\nOR Unit Purchase Price directly.",
                    "Price Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build cart item
            _cart.Add(new PurchaseCartItem
            {
                ProductId     = product.Id,
                ProductName   = product.Name,
                NumberOfPacks = numPacks,
                UnitsPerPack  = upp,
                PackPrice     = packPrice,
                TotalUnits    = effectiveTotalUnits,
                UnitPrice     = effectiveUnitPrice,
                NewSalePrice  = newSalePrice
            });

            UpdateCartTotals();

            // Reset input fields
            _isCalculating = true;
            try
            {
                if (txtNumberOfPacks != null) txtNumberOfPacks.Text = "";
                if (txtPackPrice     != null) txtPackPrice.Text     = "";
                if (txtTotalQty      != null) txtTotalQty.Text      = "";
                if (txtUnitCost      != null) txtUnitCost.Text      = "";
                if (txtNewSalePrice  != null) txtNewSalePrice.Text  = "";
                // Keep txtUnitsPerPack pre-filled from product for convenience
            }
            finally
            {
                _isCalculating = false;
            }

            RefreshInfoBar();
        }

        private void RemovePurchaseItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PurchaseCartItem item)
            {
                _cart.Remove(item);
                UpdateCartTotals();
            }
        }

        private void UpdateCartTotals()
        {
            decimal total = _cart.Sum(i => i.LineTotal);
            lblPurchaseTotal.Text = $"Total: {total.FormatRs()}";
            lblItemCount.Text     = $"{_cart.Count} item{(_cart.Count == 1 ? "" : "s")}";
            dgPurchaseItems.Items.Refresh();
        }

        // Save
        private void SavePurchase_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSupplier.SelectedValue == null)
            {
                MessageBox.Show("Please select a supplier.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_cart.Count == 0)
            {
                MessageBox.Show("Add at least one item before saving.",
                    "Empty Cart", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal paidAmount = 0;
            if (txtPaidAmount != null && !string.IsNullOrWhiteSpace(txtPaidAmount.Text))
                decimal.TryParse(txtPaidAmount.Text, out paidAmount);

            // Build service tuple always use the new 4-field signature
            // For direct-qty items (NumberOfPacks=0): PackQty = TotalUnits, UnitsPerPack = 1
            var items = _cart.Select(i =>
            (
                i.ProductId,
                PackQty:      i.PackQty,        // NumberOfPacks or TotalUnits as fallback
                PackPrice:    i.PackPrice > 0 ? i.PackPrice : i.UnitPrice,
                UnitsPerPack: i.UnitsPerPack
            )).ToList();

            bool orderOnly = chkOrderOnly?.IsChecked == true;

            (bool Success, string Message) result;
            if (orderOnly)
            {
                var r = _purchaseService.CreatePurchaseOrder((int)cmbSupplier.SelectedValue, items, null);
                result = (r.Success, r.Message);
            }
            else
            {
                result = _purchaseService.CreatePurchase((int)cmbSupplier.SelectedValue, items, paidAmount, null);
            }

            if (result.Success)
            {
                // Apply sale price overrides if provided
                ApplySalePriceOverrides();

                MessageBox.Show(result.Message, orderOnly ? "Purchase Order Created" : "Purchase Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                if (chkOrderOnly != null) chkOrderOnly.IsChecked = false;
                _cart.Clear();
                UpdateCartTotals();
                if (txtPaidAmount != null) txtPaidAmount.Text = "0";
                ClearInputFields();
                RefreshHistory();
            }
            else
            {
                MessageBox.Show(result.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Updates product sale prices where the user provided an override.</summary>
        private void ApplySalePriceOverrides()
        {
            foreach (var item in _cart.Where(i => i.NewSalePrice > 0))
            {
                var product = _productService.GetById(item.ProductId);
                if (product == null) continue;
                product.SalePrice = item.NewSalePrice;
                product.UnitPrice = item.NewSalePrice;
                _productService.Update(product);
            }
        }

        private void ClearInputFields()
        {
            _isCalculating = true;
            try
            {
                txtNumberOfPacks.Text = "";
                txtUnitsPerPack.Text  = "";
                txtPackPrice.Text     = "";
                txtTotalQty.Text      = "";
                txtUnitCost.Text      = "";
                txtNewSalePrice.Text  = "";
            }
            finally
            {
                _isCalculating = false;
            }
            RefreshInfoBar();
        }

        // Supplier / Product inline creation helpers
        private void AddSupplier_Click(object sender, RoutedEventArgs e)
            => AddSupplierInline_Click(sender, e);

        private void AddSupplierInline_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SupplierDialog(null) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                var result = _supplierService.Create(dialog.Supplier);
                if (result.Success)
                {
                    cmbSupplier.ItemsSource  = _supplierService.GetAll();
                    cmbSupplier.SelectedValue = dialog.Supplier.Id;
                    UpdateSupplierBalance();
                }
                else
                    MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddProductInline_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProductDialog(null) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                var result = _productService.Create(dialog.Product);
                if (result.Success)
                {
                    cmbProduct.ItemsSource   = _productService.GetAll();
                    cmbProduct.SelectedValue = dialog.Product.Id;
                }
                else
                    MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Input validation
        private void NumberValidation(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[0-9.]+$");
        }
    }
}

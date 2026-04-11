using MedicalStore.Common.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    // -------------------------------------------------------------------
    // Refund Item ViewModel (ManualRefund-specific)
    // -------------------------------------------------------------------
    public class ManualRefundItemVM : INotifyPropertyChanged
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Discount { get; set; }

        // Tracks which replacement item is linked to this refund row (by ReplacementItemVM.Id)
        public Guid? LinkedReplacementId { get; set; }

        // Display helper shown in the Refund table "Replacement" column
        private string _replacementLabel = "";
        public string ReplacementLabel
        {
            get => _replacementLabel;
            set { _replacementLabel = value; OnPropChanged(nameof(ReplacementLabel)); OnPropChanged(nameof(HasReplacement)); }
        }
        public bool HasReplacement => !string.IsNullOrEmpty(ReplacementLabel);

        public decimal GrossRefund => UnitPrice * Quantity;
        public decimal NetRefund => GrossRefund - Discount;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public void CallPropChanged()
        {
            OnPropChanged(nameof(GrossRefund));
            OnPropChanged(nameof(NetRefund));
        }
    }

    // -------------------------------------------------------------------
    // ManualRefundView Page
    // -------------------------------------------------------------------
    public partial class ManualRefundView : Page
    {
        private readonly ProductService _productService = new();
        private readonly ReturnService _returnService = new();
        private readonly CustomerService _customerService = new();
        private Product? _selectedProduct;
        private ObservableCollection<ManualRefundItemVM> _refundItems = new();
        private ObservableCollection<ReplacementItemVM> _replacementItems = new();

        public ManualRefundView()
        {
            InitializeComponent();
            dgRefundItems.ItemsSource = _refundItems;
            dgReplacementItems.ItemsSource = _replacementItems;

            var customers = _customerService.GetAll();
            cmbCustomer.ItemsSource = customers;
            cmbCustomer.SelectedIndex = 0;

            _replacementItems.CollectionChanged += (s, e) => UpdateTotals();
        }

        // Product Search
        private void txtProductSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = txtProductSearch.Text.Trim();
            if (query.Length < 2) { popupSearch.IsOpen = false; return; }

            var results = _productService.Search(query);
            if (results.Any())
            {
                lstSearchResults.ItemsSource = results;
                popupSearch.IsOpen = true;
            }
            else
            {
                popupSearch.IsOpen = false;
            }
        }

        private void ProductList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && lstSearchResults.SelectedItem is Product product)
            {
                SelectProductFromList(product);
                e.Handled = true;
            }
        }

        private void ProductList_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstSearchResults.SelectedItem is Product product)
                SelectProductFromList(product);
        }

        private void SelectProductFromList(Product product)
        {
            _selectedProduct = product;
            txtProductSearch.Text = product.Name;
            lblSelectedProduct.Text = $"{product.Name}  |  Price: Rs {product.SalePrice:N2}  |  Stock after refund: {product.Quantity + 1}+";
            popupSearch.IsOpen = false;
            lstSearchResults.SelectedItem = null;
            txtQty.Focus();
            txtQty.SelectAll();
        }

        private void txtProductSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Down && popupSearch.IsOpen)
            {
                lstSearchResults.Focus();
                if (lstSearchResults.Items.Count > 0 && lstSearchResults.SelectedIndex < 0)
                    lstSearchResults.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Enter && popupSearch.IsOpen && lstSearchResults.Items.Count > 0)
            {
                SelectProductFromList((Product)lstSearchResults.Items[0]);
                e.Handled = true;
            }
        }

        private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (sender == txtQty) { txtDiscount.Focus(); txtDiscount.SelectAll(); }
                else if (sender == txtDiscount) AddItem_Click(this, new RoutedEventArgs());
            }
        }

        private void CalculateTotal_Event(object sender, TextChangedEventArgs e) { /* No-op UpdateTotals is event-driven */ }

        // Add Refund Item
        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProduct == null)
            {
                MessageBox.Show("Please select a valid product first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtQty.Text, out int qty) || qty <= 0)
            {
                MessageBox.Show("Please enter a valid refund quantity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtDiscount.Text, out decimal discount) || discount < 0)
                discount = 0;

            if (discount > _selectedProduct.SalePrice * qty)
            {
                MessageBox.Show("Discount cannot exceed the total refund value of the product.", "Limit Exceeded", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check duplicate
            if (_refundItems.Any(i => i.ProductId == _selectedProduct.Id))
            {
                var confirm = MessageBox.Show(
                    $"{_selectedProduct.Name} is already in the refund list. Add anyway (as a separate entry)?",
                    "Duplicate Product", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }

            var item = new ManualRefundItemVM
            {
                ProductId = _selectedProduct.Id,
                ProductName = _selectedProduct.Name,
                UnitPrice = _selectedProduct.SalePrice,
                Quantity = qty,
                Discount = discount
            };

            _refundItems.Add(item);
            UpdateTotals();
            ClearInput();
        }

        private void ClearInput()
        {
            _selectedProduct = null;
            txtProductSearch.Text = "";
            lblSelectedProduct.Text = "No product selected";
            txtQty.Text = "1";
            txtDiscount.Text = "0";
        }

        // Remove Refund Item
        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ManualRefundItemVM item)
            {
                // Also remove any linked replacement
                if (item.LinkedReplacementId.HasValue)
                {
                    var linked = _replacementItems.FirstOrDefault(r => r.Id == item.LinkedReplacementId.Value);
                    if (linked != null) _replacementItems.Remove(linked);
                }
                _refundItems.Remove(item);
                UpdateTotals();
            }
        }

        // Replace Button Opens the Replacement Selection Dialog
        private void ReplaceItemBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not ManualRefundItemVM refundItem)
                return;

            // If already has a replacement, ask user whether to change it
            if (refundItem.LinkedReplacementId.HasValue)
            {
                var confirm = MessageBox.Show(
                    $"'{refundItem.ProductName}' already has a replacement assigned ({refundItem.ReplacementLabel}).\n\nDo you want to change it?",
                    "Change Replacement", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                // Remove old replacement
                var oldReplacement = _replacementItems.FirstOrDefault(r => r.Id == refundItem.LinkedReplacementId.Value);
                if (oldReplacement != null) _replacementItems.Remove(oldReplacement);
                refundItem.LinkedReplacementId = null;
                refundItem.ReplacementLabel = "";
            }

            // Open replacement selection dialog
            var dialog = new ReplacementSelectionDialog(refundItem.ProductName, refundItem.Quantity)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                var result = dialog.Result;

                // Create a new replacement entry linked to this refund item
                var replacement = new ReplacementItemVM
                {
                    Id = Guid.NewGuid(),
                    ProductId = result.ProductId,
                    ProductName = result.ProductName,
                    UnitPrice = result.UnitPrice,
                    Quantity = result.Quantity,
                    LinkedRefundProductId = refundItem.ProductId,
                    LinkedRefundProductName = refundItem.ProductName,
                    StockAvailable = result.StockAvailable
                };
                replacement.OnPropChanged(nameof(replacement.Total));

                // Wire total change
                replacement.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(ReplacementItemVM.Quantity))
                        UpdateTotals();
                };

                _replacementItems.Add(replacement);

                // Link back to refund item
                refundItem.LinkedReplacementId = replacement.Id;
                refundItem.ReplacementLabel = $"{result.ProductName} (x{result.Quantity})";

                UpdateTotals();
            }
        }

        // Replacement Section Search (free addition, not per-item)
        private void txtReplaceSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = txtReplaceSearch.Text.Trim();
            if (query.Length < 2) { popupReplace.IsOpen = false; return; }

            var results = _productService.Search(query);
            if (results.Any()) { lstReplaceResults.ItemsSource = results; popupReplace.IsOpen = true; }
            else popupReplace.IsOpen = false;
        }

        private void txtReplaceSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Down && popupReplace.IsOpen)
            {
                lstReplaceResults.Focus();
                if (lstReplaceResults.Items.Count > 0 && lstReplaceResults.SelectedIndex < 0)
                    lstReplaceResults.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Enter && popupReplace.IsOpen && lstReplaceResults.Items.Count > 0)
            {
                SelectReplaceProductFromList((Product)lstReplaceResults.Items[0]);
                e.Handled = true;
            }
        }

        private void ReplaceList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && lstReplaceResults.SelectedItem is Product product)
            {
                SelectReplaceProductFromList(product);
                e.Handled = true;
            }
        }

        private void ReplaceList_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstReplaceResults.SelectedItem is Product product)
                SelectReplaceProductFromList(product);
        }

        private void SelectReplaceProductFromList(Product product)
        {
            if (product.Quantity <= 0)
            {
                MessageBox.Show($"'{product.Name}' is out of stock and cannot be added as a replacement.",
                    "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = _replacementItems.FirstOrDefault(p => p.ProductId == product.Id && p.LinkedRefundProductId == 0);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                var vm = new ReplacementItemVM
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.SalePrice,
                    Quantity = 1,
                    StockAvailable = product.Quantity
                };
                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(ReplacementItemVM.Quantity))
                        UpdateTotals();
                };
                _replacementItems.Add(vm);
            }

            txtReplaceSearch.Text = "";
            popupReplace.IsOpen = false;
            lstReplaceResults.SelectedItem = null;
            UpdateTotals();
        }

        private void RemoveReplacementItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ReplacementItemVM item)
            {
                // Unlink from refund item if linked
                var linkedRefund = _refundItems.FirstOrDefault(r => r.LinkedReplacementId == item.Id);
                if (linkedRefund != null)
                {
                    linkedRefund.LinkedReplacementId = null;
                    linkedRefund.ReplacementLabel = "";
                }
                _replacementItems.Remove(item);
                UpdateTotals();
            }
        }

        // Totals
        private void CmbCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTotals();
        }

        private void CmbPaymentAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lblPaymentAmount == null) return;
            if (cmbPaymentAction.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "StoreRefunds")
                lblPaymentAmount.Text = "Amount Refunded to Customer";
            else
                lblPaymentAmount.Text = "Amount Received by Customer";
        }

        private void txtPaidByCustomer_TextChanged(object sender, TextChangedEventArgs e) { }

        private void UpdateTotals()
        {
            if (lblGrossRefund == null) return;

            decimal retValue = _refundItems.Sum(i => i.NetRefund);
            decimal repValue = _replacementItems.Sum(i => i.Total);
            decimal netReturnDiff = repValue - retValue;
            decimal existingDue = 0;

            if (cmbCustomer?.SelectedItem is Customer c && !c.IsWalkIn)
                existingDue = c.Balance;

            decimal totalPayable = existingDue + netReturnDiff;

            if (txtPaidByCustomer != null && !txtPaidByCustomer.IsFocused)
            {
                txtPaidByCustomer.TextChanged -= txtPaidByCustomer_TextChanged;
                txtPaidByCustomer.Text = Math.Abs(totalPayable).ToString("0.00");
                txtPaidByCustomer.TextChanged += txtPaidByCustomer_TextChanged;
            }

            lblGrossRefund.Text = $"{(retValue).FormatRs()}";
            lblReplacementTotal.Text = $"{(repValue).FormatRs()}";
            lblNetDiffValue.Text = $"{(netReturnDiff).FormatRs()}";

            // Colour-code net difference
            lblNetDiffValue.Foreground = netReturnDiff >= 0
                ? new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28))
                : new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));

            if (totalPayable > 0)
            {
                lblNetDifference.Text = $"Rs {totalPayable:N2}  Customer Owes";
                lblNetDifference.Foreground = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                if (cmbPaymentAction != null && cmbPaymentAction.SelectedIndex != 0)
                    cmbPaymentAction.SelectedIndex = 0;
            }
            else if (totalPayable < 0)
            {
                lblNetDifference.Text = $"Rs {Math.Abs(totalPayable):N2}  Store Owes";
                lblNetDifference.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                if (cmbPaymentAction != null && cmbPaymentAction.SelectedIndex != 1)
                    cmbPaymentAction.SelectedIndex = 1;
            }
            else
            {
                lblNetDifference.Text = "Rs 0.00  Balanced";
                lblNetDifference.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x73, 0xE8));
                if (cmbPaymentAction != null && cmbPaymentAction.SelectedIndex != 1)
                    cmbPaymentAction.SelectedIndex = 1;
            }

            if (existingDue != 0)
            {
                gridPreviousDues.Visibility = Visibility.Visible;
                lblPreviousDuesVal.Text = $"{(existingDue).FormatRs()}";
                lblAdjustmentHint.Visibility = Visibility.Visible;
            }
            else
            {
                gridPreviousDues.Visibility = Visibility.Collapsed;
                lblAdjustmentHint.Visibility = Visibility.Collapsed;
            }

            // Update replacement panel header count
            if (lblReplacementCount != null)
                lblReplacementCount.Text = _replacementItems.Count > 0
                    ? $"Replacement Items  ({_replacementItems.Count})"
                    : "Replacement Items";
        }

        // Process Refund
        private void ProcessRefund_Click(object sender, RoutedEventArgs e)
        {
            if (!_refundItems.Any())
            {
                MessageBox.Show("No refund items added to process.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbCustomer.SelectedValue == null)
            {
                MessageBox.Show("Please select a customer.", "Validation Error");
                return;
            }

            // Per-item validation: replacement qty cannot exceed refund qty for linked items
            foreach (var refundItem in _refundItems)
            {
                if (!refundItem.LinkedReplacementId.HasValue) continue;
                var linked = _replacementItems.FirstOrDefault(r => r.Id == refundItem.LinkedReplacementId.Value);
                if (linked == null) continue;
                if (linked.Quantity > refundItem.Quantity)
                {
                    MessageBox.Show(
                        $"Replacement quantity for '{linked.ProductName}' ({linked.Quantity}) " +
                        $"exceeds the refund quantity of '{refundItem.ProductName}' ({refundItem.Quantity}).\n\n" +
                        $"Please adjust the replacement quantity.",
                        "Quantity Mismatch", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Validate stock availability for all replacement items
            foreach (var rep in _replacementItems)
            {
                if (rep.Quantity > rep.StockAvailable)
                {
                    MessageBox.Show(
                        $"Insufficient stock for replacement product '{rep.ProductName}'.\n" +
                        $"Available: {rep.StockAvailable}, Requested: {rep.Quantity}",
                        "Stock Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            int customerId = (int)cmbCustomer.SelectedValue;

            decimal retValue = _refundItems.Sum(i => i.NetRefund);
            decimal repValue = _replacementItems.Sum(i => i.Total);
            decimal netReturnDiff = repValue - retValue;
            decimal existingDue = (cmbCustomer.SelectedItem as Customer)?.Balance ?? 0;
            decimal totalPayable = existingDue + netReturnDiff;

            decimal amountInput = 0;
            if (!string.IsNullOrWhiteSpace(txtPaidByCustomer.Text))
                decimal.TryParse(txtPaidByCustomer.Text, out amountInput);

            bool isStoreRefunding = cmbPaymentAction.SelectedItem is ComboBoxItem cbi &&
                                    cbi.Tag?.ToString() == "StoreRefunds";

            decimal refundPaid = isStoreRefunding ? amountInput : 0;
            decimal extraReceived = !isStoreRefunding ? amountInput : 0;

            bool isWalkIn = (cmbCustomer.SelectedItem as Customer)?.IsWalkIn ?? true;

            decimal salesAdjustment = 0;

            if (totalPayable >= 0)
            {
                if (!isStoreRefunding)
                {
                    if (amountInput > totalPayable)
                    {
                        var overDialog = new OverpaymentDialog(totalPayable, amountInput, amountInput - totalPayable, isWalkIn) { Owner = Window.GetWindow(this) };
                        if (overDialog.ShowDialog() != true || overDialog.SelectedAction == OverpaymentAction.Cancelled) return;
                        if (overDialog.SelectedAction == OverpaymentAction.ReturnExcess)
                            extraReceived = amountInput - overDialog.ReturnedAmount;
                        else if (overDialog.SelectedAction == OverpaymentAction.AdjustSale)
                        {
                            salesAdjustment = overDialog.ReturnedAmount;
                            extraReceived = amountInput;
                        }
                        else
                            extraReceived = amountInput;
                    }
                    else if (amountInput < totalPayable)
                    {
                        var underDialog = new UnderpaymentDialog(totalPayable, amountInput, totalPayable - amountInput, isWalkIn) { Owner = Window.GetWindow(this) };
                        if (underDialog.ShowDialog() != true || underDialog.SelectedAction == UnderpaymentAction.Cancelled) return;
                        if (underDialog.SelectedAction == UnderpaymentAction.AdjustSale)
                        {
                            salesAdjustment = -(totalPayable - amountInput);
                        }
                        extraReceived = amountInput;
                    }
                }
                else if (isStoreRefunding && refundPaid > 0)
                {
                    MessageBox.Show("Cannot process refund. Customer has no credit balance.", "Validation Error");
                    return;
                }
            }
            else
            {
                decimal refundDue = Math.Abs(totalPayable);
                if (isStoreRefunding)
                {
                    if (amountInput < refundDue)
                    {
                        var overpayDialog = new OverpaymentDialog(refundDue, amountInput, refundDue - amountInput, isWalkIn) { Owner = Window.GetWindow(this) };
                        if (overpayDialog.ShowDialog() != true || overpayDialog.SelectedAction == OverpaymentAction.Cancelled) return;
                        if (overpayDialog.SelectedAction == OverpaymentAction.ReturnExcess)
                            refundPaid = amountInput + overpayDialog.ReturnedAmount;
                        else if (overpayDialog.SelectedAction == OverpaymentAction.AdjustSale)
                        {
                            salesAdjustment = overpayDialog.ReturnedAmount;
                            refundPaid = amountInput;
                        }
                        else
                            refundPaid = amountInput;
                    }
                    else if (amountInput > refundDue)
                    {
                        var underpayDialog = new UnderpaymentDialog(refundDue, amountInput, amountInput - refundDue, isWalkIn) { Owner = Window.GetWindow(this) };
                        if (underpayDialog.ShowDialog() != true || underpayDialog.SelectedAction == UnderpaymentAction.Cancelled) return;
                        if (underpayDialog.SelectedAction == UnderpaymentAction.AdjustSale)
                        {
                            salesAdjustment = -(amountInput - refundDue);
                        }
                        refundPaid = amountInput;
                    }
                }
            }

            decimal remainingBalance = totalPayable - extraReceived + refundPaid;

            // Build refund items summary lines
            string refundLines = string.Join("\n",
                _refundItems.Select(i =>
                {
                    string replLabel = i.HasReplacement ? $"  {i.ReplacementLabel}" : "";
                    return $"  {i.ProductName} {i.Quantity} @ Rs {i.UnitPrice:N2} (Disc: Rs {i.Discount:N2}) = Rs {i.NetRefund:N2}{replLabel}";
                }));

            string replacementLines = _replacementItems.Any()
                ? "\n\nReplacement Items:\n" + string.Join("\n",
                    _replacementItems.Select(i => $"  {i.ProductName} {i.Quantity} @ Rs {i.UnitPrice:N2} = Rs {i.Total:N2}"))
                : "";

            string balanceMsg = remainingBalance > 0
                ? $"Remaining Due: Rs {remainingBalance:N2} (added to account)"
                : remainingBalance < 0
                    ? $"Credit Balance: Rs {Math.Abs(remainingBalance):N2} (store owes customer)"
                    : "Fully Settled ";

            var confirm = MessageBox.Show(
                $"Manual Refund Summary \n" +
                $"\nRefund Items:\n{refundLines}" +
                $"{replacementLines}\n\n" +
                $"\n" +
                $"Gross Refund    : Rs {retValue:N2}\n" +
                $"Replacement Val : Rs {repValue:N2}\n" +
                $"Net Difference  : Rs {netReturnDiff:N2}\n" +
                $"Existing Due    : Rs {existingDue:N2}\n" +
                $"Total Payable   : Rs {totalPayable:N2}\n" +
                $"Payment Action  : {(isStoreRefunding ? "Store Refunds" : "Customer Pays")}\n" +
                $"Amount          : Rs {amountInput:N2}\n" +
                $"{balanceMsg}\n\n" +
                $"Process refund and update stock?",
                "Confirm Transaction", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var returnedItems = _refundItems
                    .Select(i => (i.ProductId, i.Quantity, i.UnitPrice - (i.Quantity > 0 ? i.Discount / i.Quantity : 0)))
                    .ToList();

                var replacedItems = _replacementItems
                    .Select(i => (i.ProductId, i.Quantity, i.UnitPrice))
                    .ToList();

                string notes = "Manual Refund: " + txtReturnNotes.Text;
                string invoiceNo = txtOriginalInvoice.Text.Trim();

                var result = _returnService.ProcessDirectReturn(
                    customerId,
                    returnedItems,
                    replacedItems,
                    refundPaid,
                    extraReceived,
                    salesAdjustment,
                    replacedItems.Any() ? "Manual Replace" : "Manual Refund",
                    notes,
                    string.IsNullOrEmpty(invoiceNo) ? null : invoiceNo
                );

                if (result.Success && result.Return != null)
                {
                    new Helpers.ReceiptPrinter().Print(result.Return);

                    MessageBox.Show(
                        "Manual Refund Processed Successfully!\n\nInventory and financial accounts have been updated.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    _refundItems.Clear();
                    _replacementItems.Clear();
                    ClearInput();
                    txtReturnNotes.Text = "";
                    txtOriginalInvoice.Text = "";
                    txtPaidByCustomer.Text = "0";

                    // Refresh customer list to reflect updated balance
                    var customers = _customerService.GetAll();
                    cmbCustomer.ItemsSource = customers;
                    cmbCustomer.SelectedValue = customerId;

                    UpdateTotals();
                }
                else
                {
                    MessageBox.Show(result.Message, "Error Processing", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process refund: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecordCustomerPayment_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCustomer.SelectedItem is Customer customer)
            {
                if (customer.IsWalkIn)
                {
                    MessageBox.Show("Cannot record payment for Walk-in customer.", "Not Allowed");
                    return;
                }

                var dialog = new CustomerPaymentDialog(customer);
                if (dialog.ShowDialog() == true)
                {
                    var updated = _customerService.GetById(customer.Id);
                    if (updated != null)
                    {
                        var list = cmbCustomer.ItemsSource as List<Customer>;
                        if (list != null)
                        {
                            var index = list.FindIndex(c => c.Id == updated.Id);
                            if (index >= 0) list[index] = updated;
                        }
                        cmbCustomer.SelectedItem = updated;
                        UpdateTotals();
                    }
                }
            }
        }

        private void ViewCustomerHistory_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCustomer.SelectedItem is Customer customer)
            {
                if (customer.IsWalkIn)
                {
                    MessageBox.Show("Walk-in customers do not have history.", "Not Allowed");
                    return;
                }
                var dialog = new CustomerLedgerDialog(customer);
                dialog.ShowDialog();
            }
        }

        private void NumberValidation(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9.]+$");
        }

        // Prevent Enter key from bubbling up to Frame navigation (causing "page refresh")
        private void Page_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (e.OriginalSource is System.Windows.Controls.TextBox)
                {
                    e.Handled = true;
                }
            }
        }

        // +/- buttons for replacement quantity
        private void IncreaseReplaceQty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ReplacementItemVM vm)
            {
                vm.Quantity++;
                UpdateTotals();
            }
        }

        private void DecreaseReplaceQty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ReplacementItemVM vm)
            {
                if (vm.Quantity > 1)
                {
                    vm.Quantity--;
                    UpdateTotals();
                }
            }
        }
    }
}

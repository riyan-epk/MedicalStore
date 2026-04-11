using MedicalStore.Common.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Constants;

namespace MedicalStore.Views
{
    public class CartItem : INotifyPropertyChanged
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }  // always per-unit
        public int UnitsPerPack { get; set; } = 1;

        private int _quantity = 1;

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (AvailableStock > 0 && value > AvailableStock) value = AvailableStock;
                if (value < 1) value = 1;
                _quantity = value;
                NotifyAll();
            }
        }

        public int Packs
        {
            get => UnitsPerPack > 0 ? _quantity / UnitsPerPack : 0;
            set
            {
                if (UnitsPerPack > 0)
                {
                    int currentLoose = _quantity % UnitsPerPack;
                    _quantity = (value * UnitsPerPack) + currentLoose;
                    NotifyAll();
                }
            }
        }

        public int LooseUnits
        {
            get => UnitsPerPack > 0 ? _quantity % UnitsPerPack : _quantity;
            set
            {
                if (UnitsPerPack > 0)
                {
                    int currentPacks = _quantity / UnitsPerPack;
                    _quantity = (currentPacks * UnitsPerPack) + value;
                    NotifyAll();
                }
                else
                {
                    _quantity = value;
                    NotifyAll();
                }
            }
        }

        public int TotalUnits => _quantity;
        public decimal Total => _quantity * UnitPrice;
        public int AvailableStock { get; set; }   // in units

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private void NotifyAll()
        {
            OnPropertyChanged(nameof(Quantity));
            OnPropertyChanged(nameof(Packs));
            OnPropertyChanged(nameof(LooseUnits));
            OnPropertyChanged(nameof(TotalUnits));
            OnPropertyChanged(nameof(Total));
        }
    }

    public partial class PosView : Page
    {
        private readonly ProductService _productService = new();
        private readonly CustomerService _customerService = new();
        private readonly SaleService _saleService = new();
        private readonly ReturnService _returnService = new();
        private ObservableCollection<CartItem> _cart = new();

        public PosView()
        {
            InitializeComponent();
            dgCart.ItemsSource = _cart;
            LoadCustomers();
            LoadTodaySales();
            _cart.CollectionChanged += (s, e) => UpdateCalculations();
            if (lblBillingDateTime != null)
                lblBillingDateTime.Text = DateTime.Now.ToString("dd/MM/yyyy  hh:mm tt");
        }

        public void RefreshData()
        {
            var oldVal = cmbCustomer.SelectedValue;
            LoadCustomers();
            LoadTodaySales();
            
            if (oldVal != null)
            {
                cmbCustomer.SelectedValue = oldVal;
                if (cmbCustomer.SelectedItem == null && cmbCustomer.Items.Count > 0)
                {
                    cmbCustomer.SelectedIndex = 0;
                }
            }
            UpdateCalculations();
        }

        private void LoadTodaySales()
        {
            try
            {
                var today = DateTime.Now.Date;
                var sales = _saleService.GetByDateRange(today, today.AddDays(1).AddTicks(-1));
                
                dgTodaySales.ItemsSource = sales;
                lblTodayTotal.Text = $"{(sales.Sum(s => s.NetAmount)).FormatRs()}";
                lblTodayProfit.Text = $"{(sales.Sum(s => s.Profit)).FormatRs()}";
                lblTodayDue.Text = $"{(sales.Sum(s => s.DueAmount)).FormatRs()}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading today sales: {ex.Message}");
            }
        }

        private void LoadCustomers()
        {
            try
            {
                var customers = _customerService.GetAll();
                cmbCustomer.ItemsSource = customers;
                
                // Select Walk-in by default, if not found, select first item
                var walkin = customers.FirstOrDefault(c => c.IsWalkIn);
                if (walkin != null) 
                    cmbCustomer.SelectedValue = walkin.Id;
                else if (customers.Any()) 
                    cmbCustomer.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading customers in POS: {ex.Message}", "POS Error");
            }
        }

        private void txtProductSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtProductSearch == null || lstSearchResults == null || popupSearch == null)
                return;

            var query = txtProductSearch.Text?.Trim();

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                popupSearch.IsOpen = false;
                lstSearchResults.ItemsSource = null;
                return;
            }

            var results = _productService?.Search(query);

            if (results != null && results.Any())
            {
                lstSearchResults.ItemsSource = results;
                popupSearch.IsOpen = true;
            }
            else
            {
                lstSearchResults.ItemsSource = null;
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
            {
                SelectProductFromList(product);
            }
        }

        private void SelectProductFromList(Product product)
        {
            AddToCart(product);
            popupSearch.IsOpen = false;
            lstSearchResults.SelectedItem = null;
            txtProductSearch.Text = "";
            txtProductSearch.Focus();
        }

        private void txtProductSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Down && popupSearch.IsOpen)
            {
                lstSearchResults.Focus();
                if (lstSearchResults.Items.Count > 0 && lstSearchResults.SelectedIndex < 0)
                {
                    lstSearchResults.SelectedIndex = 0;
                }
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Enter && popupSearch.IsOpen && lstSearchResults.Items.Count > 0)
            {
                SelectProductFromList((Product)lstSearchResults.Items[0]);
                e.Handled = true;
            }
        }

        private void NumberValidation(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9.%]+$");
        }

        private void AddToCart(Product product)
        {
            var existing = _cart.FirstOrDefault(c => c.ProductId == product.Id);
            int availStock = product.StockUnits > 0 ? product.StockUnits : product.Quantity;

            if (existing != null)
            {
                if (existing.Quantity < existing.AvailableStock)
                    existing.Quantity++;
                else
                    MessageBox.Show($"Only {existing.AvailableStock} units available in stock.", "Stock Limit");
            }
            else
            {
                if (availStock <= 0)
                {
                    MessageBox.Show("This product is out of stock.", "Out of Stock");
                    return;
                }
                
                if (product.ExpiryDate <= DateTime.Now.AddDays(AppConstants.NearExpiryDays))
                {
                    var result = MessageBox.Show($"Warning: This product expires soon ({product.ExpiryDate:dd/MM/yyyy}).\n\nDo you still want to sell it?", "Expiry Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        return;
                    }
                }
                
                var newItem = new CartItem
                {
                    ProductId    = product.Id,
                    ProductName  = product.Name,
                    UnitPrice    = product.SalePrice,
                    UnitsPerPack = product.UnitsPerPack > 0 ? product.UnitsPerPack : 1,
                    AvailableStock = availStock,
                    Quantity     = 1
                };
                newItem.PropertyChanged += (s, e) => { UpdateCalculations(); };
                _cart.Add(newItem);
            }
            UpdateCalculations();
            dgCart.Items.Refresh();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CartItem item)
            {
                _cart.Remove(item);
                UpdateCalculations();
            }
        }

        private void DecreasePack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CartItem item)
            {
                if (item.Quantity >= item.UnitsPerPack)
                {
                    item.Quantity -= item.UnitsPerPack;
                    UpdateCalculations();
                }
            }
        }

        private void IncreasePack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CartItem item)
            {
                if (item.Quantity + item.UnitsPerPack <= item.AvailableStock)
                {
                    item.Quantity += item.UnitsPerPack;
                    UpdateCalculations();
                }
                else
                {
                    MessageBox.Show($"Cannot add pack. Only {item.AvailableStock} units available in stock.", "Stock Limit");
                }
            }
        }

        private void DecreaseLoose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CartItem item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                    UpdateCalculations();
                }
            }
        }

        private void IncreaseLoose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CartItem item)
            {
                if (item.Quantity + 1 <= item.AvailableStock)
                {
                    item.Quantity++;
                    UpdateCalculations();
                }
                else
                {
                    MessageBox.Show($"Cannot add unit. Only {item.AvailableStock} units available in stock.", "Stock Limit");
                }
            }
        }

        private void DgCart_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                dgCart.Items.Refresh();
                UpdateCalculations();
            }));
        }

        private void CmbCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCustomer.SelectedItem is Customer customer)
            {
                decimal bal = customer.Balance;
                if (borderCustBalance != null)
                {
                    if (bal != 0)
                    {
                        borderCustBalance.Visibility = Visibility.Visible;
                        if (bal > 0)
                        {
                            borderCustBalance.Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(255, 235, 238)); // light red
                            if (lblBalanceType != null)
                            {
                                lblBalanceType.Text = "Previous Due Balance:";
                                lblBalanceType.Foreground = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(198, 40, 40));
                            }
                            if (lblPreviousBalance != null)
                            {
                                lblPreviousBalance.Text = $"{(bal).FormatRs()}";
                                lblPreviousBalance.Foreground = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(198, 40, 40));
                                lblPreviousBalance.Tag = "Due";
                            }
                        }
                        else
                        {
                            borderCustBalance.Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(232, 245, 233)); // light green
                            if (lblBalanceType != null)
                            {
                                lblBalanceType.Text = "Credit Balance (We owe customer):";
                                lblBalanceType.Foreground = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(46, 125, 50));
                            }
                            if (lblPreviousBalance != null)
                            {
                                lblPreviousBalance.Text = $"{(Math.Abs(bal)).FormatRs()}";
                                lblPreviousBalance.Foreground = new System.Windows.Media.SolidColorBrush(
                                    System.Windows.Media.Color.FromRgb(46, 125, 50));
                                lblPreviousBalance.Tag = "Credit";
                            }
                        }
                    }
                    else
                    {
                        borderCustBalance.Visibility = Visibility.Collapsed;
                    }
                }
                UpdateCalculations();
            }
        }

        private void Calculation_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateCalculations();
        }
        private void UpdateCalculations()
        {
            decimal subTotal = _cart?.Sum(c => c?.Total ?? 0) ?? 0;
            decimal discountAmount = 0;
            string discText = txtDiscount?.Text?.Trim() ?? "";

            if (!string.IsNullOrEmpty(discText))
            {
                if (AppConstants.DiscountType == "Percentage")
                {
                    if (decimal.TryParse(discText.Replace("%", ""), out decimal percent))
                        discountAmount = subTotal * (percent / 100);
                }
                else
                {
                    decimal.TryParse(discText.Replace("%", ""), out discountAmount);
                }
            }

            if (discountAmount > subTotal) discountAmount = subTotal;

            decimal netAfterDiscount = subTotal - discountAmount;

            // Tax
            decimal taxAmount = 0;
            if (AppConstants.TaxRate > 0)
            {
                taxAmount = netAfterDiscount * (AppConstants.TaxRate / 100);
                if (gridTax != null) gridTax.Visibility = Visibility.Visible;
                if (lblTaxLabel != null) lblTaxLabel.Text = $"Tax ({AppConstants.TaxRate}%):";
                if (lblTaxAmount != null) lblTaxAmount.Text = $"{(taxAmount).FormatRs()}";
            }
            else
            {
                if (gridTax != null) gridTax.Visibility = Visibility.Collapsed;
            }

            decimal netAmount = netAfterDiscount + taxAmount;
            if (netAmount < 0) netAmount = 0;

            // Customer existing balance
            // (+) positive = customer owes us (previous due)  adds to bill
            // (-) negative = we owe customer (credit/advance)  reduces bill
            decimal existingBalance = 0;
            if (cmbCustomer?.SelectedItem is Customer cust)
                existingBalance = cust?.Balance ?? 0;

            // FinalPayable = CurrentBill + ExistingBalance
            decimal finalPayable = netAmount + existingBalance;
            
            if (txtPaidAmount != null && !txtPaidAmount.IsFocused)
            {
                txtPaidAmount.TextChanged -= Calculation_Changed;
                txtPaidAmount.Text = Math.Abs(finalPayable).ToString("0.00");
                txtPaidAmount.TextChanged += Calculation_Changed;
            }

            // Show previous dues row when customer has a debt
            if (existingBalance > 0)
            {
                if (gridPreviousDues != null) gridPreviousDues.Visibility = Visibility.Visible;
                if (lblPreviousDuesVal != null) lblPreviousDuesVal.Text = $"{(existingBalance).FormatRs()}";
                if (gridAdvance != null) gridAdvance.Visibility = Visibility.Collapsed;
            }
            else if (existingBalance < 0)
            {
                // Credit balance
                if (gridPreviousDues != null) gridPreviousDues.Visibility = Visibility.Collapsed;
                if (gridAdvance != null) gridAdvance.Visibility = Visibility.Visible;
                decimal creditUsed = Math.Min(netAmount, Math.Abs(existingBalance));
                decimal creditLeft  = Math.Abs(existingBalance) - creditUsed;
                if (lblAdvanceAmount != null)
                    lblAdvanceAmount.Text = creditLeft > 0
                        ? $"-Rs {creditUsed:N2}  (Rs {creditLeft:N2} credit left)"
                        : $"-Rs {creditUsed:N2}  (fully applied)";
            }
            else
            {
                if (gridPreviousDues != null) gridPreviousDues.Visibility = Visibility.Collapsed;
                if (gridAdvance != null) gridAdvance.Visibility = Visibility.Collapsed;
            }

            // Divider & Grand Total
            if (borderBal != null) borderBal.Visibility = existingBalance != 0 ? Visibility.Visible : Visibility.Collapsed;
            if (gridFinalBal != null) gridFinalBal.Visibility = Visibility.Visible;
            if (lblGrandTotal != null) lblGrandTotal.Text = $"{(finalPayable).FormatRs()}";

            // paidAmount entered by user
            decimal inputAmount = 0;
            if (txtPaidAmount != null && !string.IsNullOrWhiteSpace(txtPaidAmount.Text))
                decimal.TryParse(txtPaidAmount.Text, out inputAmount);

            bool isRefund = cmbPaymentAction?.SelectedItem is ComboBoxItem cbi && cbi.Tag?.ToString() == "Refunds";
            decimal effectivePaid = isRefund ? -inputAmount : inputAmount;

            decimal change = 0;
            decimal remainingDue = 0;

            if (finalPayable >= 0)
            {
                if (effectivePaid > finalPayable)
                {
                    // Overpayment: show the excess as "change pending" in the summary.
                    // The actual action (return cash / add to credit) will be decided
                    // via the OverpaymentDialog shown at Save time.
                    change = effectivePaid - finalPayable;

                    if (gridChange != null) gridChange.Visibility = Visibility.Visible;
                    if (gridNewDues != null) gridNewDues.Visibility = Visibility.Collapsed;
                    if (gridAdvanceAdded != null) gridAdvanceAdded.Visibility = Visibility.Collapsed;
                }
                else if (effectivePaid == finalPayable)
                {
                    if (gridChange != null) gridChange.Visibility = Visibility.Collapsed;
                    if (gridNewDues != null) gridNewDues.Visibility = Visibility.Collapsed;
                    if (gridAdvanceAdded != null) gridAdvanceAdded.Visibility = Visibility.Collapsed;
                }
                else
                {
                    remainingDue = finalPayable - effectivePaid;
                    if (gridChange != null) gridChange.Visibility = Visibility.Collapsed;
                    if (gridNewDues != null) gridNewDues.Visibility = Visibility.Visible;
                    if (gridAdvanceAdded != null) gridAdvanceAdded.Visibility = Visibility.Collapsed;
                    if (lblDuePrefix != null) lblDuePrefix.Text = "Remaining Due:";
                }
            }
            else
            {
                // finalPayable < 0 meaning Store owes customer
                // effectivePaid is negative if store is refunding
                if (chkReturnChange != null) chkReturnChange.Visibility = Visibility.Collapsed;
                if (gridAdvanceAdded != null) gridAdvanceAdded.Visibility = Visibility.Collapsed;
                decimal owingCredit = Math.Abs(finalPayable);
                decimal amountRefunded = isRefund ? inputAmount : -inputAmount;
                
                remainingDue = owingCredit - amountRefunded;
                if (remainingDue < 0)
                {
                     if (gridChange != null) gridChange.Visibility = Visibility.Collapsed;
                     if (gridNewDues != null) gridNewDues.Visibility = Visibility.Collapsed;
                }
                else
                {
                     if (gridChange != null) gridChange.Visibility = Visibility.Collapsed;
                     if (gridNewDues != null) gridNewDues.Visibility = Visibility.Collapsed;
                }
            }

            if (lblSubTotal != null) lblSubTotal.Text = $"{(subTotal).FormatRs()}";
            if (lblNetAmount != null) lblNetAmount.Text = $"{(netAmount).FormatRs()}";
            if (lblChange != null) lblChange.Text = $"{(change).FormatRs()}";
            if (lblDue != null) lblDue.Text = $"{(remainingDue).FormatRs()}";

            if (btnSavePrint != null)
                btnSavePrint.IsEnabled = _cart?.Any() ?? false;
            
            if (btnSaveOnly != null)
                btnSaveOnly.IsEnabled = _cart?.Any() ?? false;
        }

        // ChkReturnChange removed overpayment is now handled via OverpaymentDialog.

        private void ViewCustomerHistory_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCustomer.SelectedItem is Customer customer && !customer.IsWalkIn)
            {
                var dialog = new CustomerLedgerDialog(customer);
                dialog.Owner = Window.GetWindow(this);
                dialog.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a registered customer to view history.", "Selection Required");
            }
        }

        private void txtPaidAmount_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && btnSavePrint != null && btnSavePrint.IsEnabled)
            {
                ProcessSale(true);
                e.Handled = true;
            }
        }

        private void SaveAndPrint_Click(object sender, RoutedEventArgs e) => ProcessSale(true);
        private void SaveOnly_Click(object sender, RoutedEventArgs e) => ProcessSale(false);

        private void ProcessSale(bool printInvoice)
        {
            try
            {
                if (_cart.Count == 0)
                {
                    MessageBox.Show("Cart is empty. Add items first.", "Empty Cart");
                    return;
                }

                var transType = (cmbTransType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Sale";

                if (cmbCustomer.SelectedValue == null)
                {
                    MessageBox.Show("Please select a customer.", "Customer Required");
                    return;
                }

                // Discount
                decimal subTotal = _cart.Sum(c => c.Total);
                decimal discountValue = 0;
                string discText = txtDiscount?.Text?.Trim() ?? "";

                if (!string.IsNullOrEmpty(discText))
                {
                    if (AppConstants.DiscountType == "Percentage")
                    {
                        if (decimal.TryParse(discText.Replace("%", ""), out decimal percent))
                            discountValue = subTotal * (percent / 100);
                    }
                    else
                    {
                        decimal.TryParse(discText.Replace("%", ""), out discountValue);
                    }
                }

                if (discountValue > subTotal)
                {
                    MessageBox.Show("Discount cannot be greater than subtotal.", "Error");
                    return;
                }

                // Payment amount
                decimal inputAmount = 0;
                decimal.TryParse(txtPaidAmount.Text, out inputAmount);
                bool isRefund = cmbPaymentAction?.SelectedItem is ComboBoxItem cbi && cbi.Tag?.ToString() == "Refunds";
                decimal effectivePaid = isRefund ? -inputAmount : inputAmount;

                var items      = _cart.Select(c => (c.ProductId, c.Packs, c.LooseUnits, c.UnitPrice)).ToList();
                int customerId = (int)cmbCustomer.SelectedValue;

                if (transType == "Sale")
                {
                    // Calculate the bill total (NetAmount)
                    decimal netAfterDiscount = subTotal - discountValue;
                    decimal taxAmount = AppConstants.TaxRate > 0
                        ? netAfterDiscount * (AppConstants.TaxRate / 100m)
                        : 0m;
                    decimal billTotal = netAfterDiscount + taxAmount;

                    // Customer existing balance (positive = owes us, negative = we owe them)
                    decimal existingBalance = 0;
                    bool isWalkIn = true;
                    if (cmbCustomer.SelectedItem is Customer selCust)
                    {
                        existingBalance = selCust.Balance;
                        isWalkIn = selCust.IsWalkIn;
                    }

                    decimal finalPayable = billTotal + existingBalance;

                    // Overpayment check
                    // The excess is what the customer paid above the TOTAL PAYABLE
                    // (bill + prior dues). We must never record this as revenue.
                    decimal changeAmount = 0;
                    bool addExcessToCredit = false;

                    if (effectivePaid > finalPayable && finalPayable >= 0)
                    {
                        decimal excessAmount = effectivePaid - finalPayable;

                        var overpayDialog = new OverpaymentDialog(
                            billTotal:       finalPayable,     // what customer was supposed to pay
                            amountReceived:  effectivePaid,
                            excessAmount:    excessAmount,
                            isWalkInCustomer: isWalkIn)
                        {
                            Owner = Window.GetWindow(this)
                        };

                        bool? answered = overpayDialog.ShowDialog();

                        if (answered != true || overpayDialog.SelectedAction == OverpaymentAction.Cancelled)
                            return; // user dismissed do NOT proceed

                        if (overpayDialog.SelectedAction == OverpaymentAction.AddToCredit)
                        {
                            addExcessToCredit = true;
                            changeAmount = 0;
                        }
                        else
                        {
                            changeAmount = overpayDialog.ReturnedAmount;
                            addExcessToCredit = false;
                        }
                    }
                    else if (effectivePaid < finalPayable && finalPayable >= 0)
                    {
                        decimal remainingAmount = finalPayable - effectivePaid;
                        var underpayDialog = new UnderpaymentDialog(
                            billTotal: finalPayable,
                            amountReceived: effectivePaid,
                            remainingAmount: remainingAmount,
                            isWalkIn: isWalkIn)
                        {
                            Owner = Window.GetWindow(this)
                        };

                        bool? answered = underpayDialog.ShowDialog();

                        if (answered != true || underpayDialog.SelectedAction == UnderpaymentAction.Cancelled)
                            return; // User cancelled sale

                        if (underpayDialog.SelectedAction == UnderpaymentAction.AdjustSale)
                        {
                            decimal targetNetAmount = effectivePaid - existingBalance;
                            
                            if (targetNetAmount < 0)
                            {
                                MessageBox.Show("Cannot adjust sale: Paid amount is less than previous dues.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            decimal targetNetAfterDiscount = AppConstants.TaxRate > 0
                                ? targetNetAmount / (1 + (AppConstants.TaxRate / 100m))
                                : targetNetAmount;

                            decimal newDiscountValue = subTotal - targetNetAfterDiscount;

                            if (newDiscountValue > subTotal)
                            {
                                MessageBox.Show("Cannot adjust sale: Required discount is greater than subtotal.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            discountValue = newDiscountValue;
                        }
                    }

                    var result = _saleService.CreateSale(
                        customerId:        customerId,
                        items:             items,
                        discount:          discountValue,
                        paidAmount:        effectivePaid,
                        changeAmount:      changeAmount,
                        addExcessToCredit: addExcessToCredit);

                    if (result.Success && result.Sale != null)
                    {
                        if (printInvoice)
                        {
                            var receiptPrinter = new Helpers.ReceiptPrinter();
                            receiptPrinter.Print(result.Sale);
                        }

                        string msg = $"Sale saved!\nInvoice: {result.Sale.InvoiceNo}";
                        if (result.Sale.ChangeAmount > 0)
                            msg += $"\nChange to return: Rs {result.Sale.ChangeAmount:N2}";
                        if (addExcessToCredit)
                        {
                            decimal excess = effectivePaid - finalPayable;
                            msg += $"\nRs {excess:N2} added to customer credit balance.";
                        }

                        MessageBox.Show(msg, "Transaction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        ClearCart();
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    decimal netAfterDiscount = subTotal - discountValue;
                    decimal taxAmount = AppConstants.TaxRate > 0
                        ? netAfterDiscount * (AppConstants.TaxRate / 100m)
                        : 0m;
                    decimal refundDue = netAfterDiscount + taxAmount;
                    
                    decimal salesAdjustment = discountValue;
                    decimal refundPaid = inputAmount;
                    
                    bool isWalkIn = true;
                    if (cmbCustomer.SelectedItem is Customer selCust)
                    {
                        isWalkIn = selCust.IsWalkIn;
                    }

                    if (inputAmount < refundDue)
                    {
                        var overpayDialog = new OverpaymentDialog(refundDue, inputAmount, refundDue - inputAmount, isWalkIn) { Owner = Window.GetWindow(this) };
                        if (overpayDialog.ShowDialog() != true || overpayDialog.SelectedAction == OverpaymentAction.Cancelled) return;
                        
                        if (overpayDialog.SelectedAction == OverpaymentAction.ReturnExcess)
                            refundPaid = inputAmount + overpayDialog.ReturnedAmount;
                        else if (overpayDialog.SelectedAction == OverpaymentAction.AdjustSale)
                            salesAdjustment += overpayDialog.ReturnedAmount;
                    }
                    else if (inputAmount > refundDue && !isWalkIn)
                    {
                        var underpayDialog = new UnderpaymentDialog(refundDue, inputAmount, inputAmount - refundDue) { Owner = Window.GetWindow(this) };
                        if (underpayDialog.ShowDialog() != true || underpayDialog.SelectedAction == UnderpaymentAction.Cancelled) return;
                        
                        if (underpayDialog.SelectedAction == UnderpaymentAction.AdjustSale)
                            salesAdjustment -= (inputAmount - refundDue);
                            
                        refundPaid = inputAmount;
                    }

                    // Convert to the 3-tuple (qty = TotalUnits) expected by ReturnService
                    var returnItems = _cart.Select(c =>
                        (c.ProductId, c.TotalUnits, c.UnitPrice)).ToList();

                    var result = _returnService.ProcessDirectReturn(
                        customerId:    customerId,
                        returnedItems: returnItems,
                        replacedItems: new List<(int, int, decimal)>(),
                        refundPaid:    refundPaid,
                        extraReceived: 0,
                        salesAdjustment: salesAdjustment,
                        returnType:    transType);

                    if (result.Success && result.Return != null)
                    {
                        var receiptPrinter = new Helpers.ReceiptPrinter();
                        receiptPrinter.Print(result.Return);

                        MessageBox.Show($"{transType} processed successfully.",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        ClearCart();
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Billing failed: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbTransType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cart != null && _cart.Count > 0)
            {
                var result = MessageBox.Show("Changing transaction type will clear the cart. Continue?", 
                    "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _cart.Clear();
                    UpdateCalculations();
                }
                else
                {
                    // Revert selection if No
                    if (e.RemovedItems.Count > 0)
                    {
                        cmbTransType.SelectionChanged -= CmbTransType_SelectionChanged;
                        cmbTransType.SelectedItem = e.RemovedItems[0];
                        cmbTransType.SelectionChanged += CmbTransType_SelectionChanged;
                    }
                }
            }
            UpdateCalculations();
        }

        private void ClearCart_Click(object sender, RoutedEventArgs e)
        {
            ClearCart();
        }

        private void ClearCart()
        {
            _cart.Clear();
            txtDiscount.Text = "0";
            txtPaidAmount.Text = "0";
            UpdateCalculations();
            LoadCustomers();
            LoadTodaySales();
        }
        private void QuickReturn_Click(object sender, RoutedEventArgs e)
        {
            if (this.Parent is Frame frame)
            {
                frame.Navigate(new ReturnsView());
            }
            else if (Window.GetWindow(this) is MainWindow mw)
            {
                mw.MainFrame.Navigate(new ReturnsView());
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
                    // Reload everything so dues are reflected everywhere
                    // 1. Refresh the customer dropdown list from DB
                    LoadCustomers();

                    // 2. Re-select the same customer (balance has changed in DB)
                    var updated = new CustomerService().GetById(customer.Id);
                    if (updated != null)
                    {
                        // Find and select the refreshed object in the new list
                        if (cmbCustomer.ItemsSource is List<Customer> list)
                        {
                            var match = list.FirstOrDefault(c => c.Id == updated.Id);
                            if (match != null)
                                cmbCustomer.SelectedItem = match;
                        }
                    }

                    // 3. Reload today's sales table (DueAmount columns will show new values)
                    LoadTodaySales();

                    // 4. Recalculate billing panel (grand total / due labels update)
                    UpdateCalculations();
                }
            }
            else
            {
                MessageBox.Show("Please select a customer first.", "Selection Required");
            }
        }

        private void CmbPaymentAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lblPaymentAmount == null) return;
            if (cmbPaymentAction.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "Refunds")
            {
                lblPaymentAmount.Text = "Amount Refunded to Customer";
            }
            else
            {
                lblPaymentAmount.Text = "Amount Received from Customer";
            }
            UpdateCalculations();
        }

        private void BalanceLabel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewCustomerHistory_Click(sender, e);
        }
    }
}

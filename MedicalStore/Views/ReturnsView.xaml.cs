using MedicalStore.Common.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public class ReturnItemVM : INotifyPropertyChanged
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int SoldQty { get; set; }
        private int _returnQty;
        public int ReturnQty
        {
            get => _returnQty;
            set { _returnQty = value; OnPropertyChanged(nameof(ReturnQty)); }
        }
        public decimal UnitPrice { get; set; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ReplacementItemVM : INotifyPropertyChanged
    {
        // Unique id used by ManualRefund to link replacement back to refund row
        public Guid Id { get; set; } = Guid.NewGuid();

        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(nameof(Quantity)); OnPropertyChanged(nameof(Total)); }
        }
        public decimal UnitPrice { get; set; }
        public decimal Total => Quantity * UnitPrice;

        // Used by ManualRefund per-item linking
        public int LinkedRefundProductId { get; set; }
        public string LinkedRefundProductName { get; set; } = "";
        // Snapshot of stock at the time of selection (for validation)
        public int StockAvailable { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        // Alias so ManualRefundView code can call .OnPropChanged(...)
        public void OnPropChanged(string name) => OnPropertyChanged(name);
    }

    public partial class ReturnsView : Page
    {
        private readonly SaleService _saleService = new();
        private readonly ReturnService _returnService = new();
        private readonly ProductService _productService = new();
        
        private Sale? _currentSale;
        private ObservableCollection<ReturnItemVM> _returnItems = new();
        private ObservableCollection<ReplacementItemVM> _replacementItems = new();

        public ReturnsView(string? invoiceNo = null)
        {
            InitializeComponent();
            dgInvoiceItems.ItemsSource = _returnItems;
            dgReplacement.ItemsSource = _replacementItems;
            
            _returnItems.CollectionChanged += Items_Changed;
            _replacementItems.CollectionChanged += Items_Changed;

            LoadInitialData();

            if (!string.IsNullOrEmpty(invoiceNo))
            {
                cmbInvoiceNo.Text = invoiceNo;
                // Since cmbInvoiceNo might be bound or loaded asynchronously, 
                // we try to find the invoice after a small delay or directly if data is there
                Dispatcher.BeginInvoke(new Action(() => {
                    cmbInvoiceNo.Text = invoiceNo;
                    FindInvoice_Click(this, new RoutedEventArgs());
                }));
            }
        }

        private void BackToPos_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
            {
                mw.MainFrame.Navigate(new PosView());
            }
        }

        private void LoadInitialData()
        {
            var sales = _saleService.GetAll();
            var returns = _returnService.GetAll();
            
            // Filter fully refunded invoices out of search parameters
            var refundableSales = new List<Sale>();
            foreach(var s in sales)
            {
                var relatedReturns = returns.Where(r => r.SaleId == s.Id).ToList();
                int totalSoldQty = s.Items.Sum(i => i.Quantity);
                int totalReturnedQty = relatedReturns.SelectMany(r => r.Items).Sum(i => i.Quantity);
                
                if (totalSoldQty > totalReturnedQty) 
                {
                    refundableSales.Add(s);
                }
            }
            
            cmbInvoiceNo.ItemsSource = refundableSales;
            
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);
            var todayReturns = returns.Where(r => r.Date >= today && r.Date < tomorrow).ToList();
            
            lblTodayCount.Text = todayReturns.Count.ToString();
            
            // Calculate actual total return value processed today
            decimal dailyReturnsValue = todayReturns.Sum(r => r.TotalAmount);
            decimal dailyRefundValue = todayReturns.Sum(r => r.RefundAmount);
            
            lblTodayRefundAmount.Text = $"{(dailyReturnsValue).FormatRs()}";
        }

        private void Items_Changed(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateTotals();
        }

        private void FindInvoice_Click(object sender, RoutedEventArgs e)
        {
            string? invoiceNo = cmbInvoiceNo.SelectedValue as string ?? cmbInvoiceNo.Text;
            if (string.IsNullOrEmpty(invoiceNo)) return;

            _currentSale = _saleService.GetByInvoiceNo(invoiceNo);
            if (_currentSale == null)
            {
                MessageBox.Show("Invoice not found.");
                return;
            }

            lblInvoiceInfo.Text = $"{_currentSale.InvoiceNo}  |  Customer: {_currentSale.Customer?.Name ?? "Walk-in"}  |  Date: {_currentSale.Date:dd/MM/yyyy}";
            if (borderInvoiceInfo != null) borderInvoiceInfo.Visibility = System.Windows.Visibility.Visible;
            // Pay/History panel removed no XAML element to show/hide
            
            var existingReturns = _returnService.GetAll().Where(r => r.SaleId == _currentSale.Id).ToList();
            _returnItems.Clear();
            _replacementItems.Clear();

            foreach (var item in _currentSale.Items)
            {
                int alreadyReturned = existingReturns.SelectMany(r => r.Items).Where(i => i.ProductId == item.ProductId).Sum(i => i.Quantity);
                int available = item.Quantity - alreadyReturned;

                if (available > 0)
                {
                    decimal proportionalDiscount = _currentSale.SubTotal > 0 ? (item.Total / _currentSale.SubTotal) * _currentSale.Discount : 0;
                    decimal actualUnitPriceAfterDiscount = item.Quantity > 0 ? (item.Total - proportionalDiscount) / item.Quantity : 0;

                    var vm = new ReturnItemVM
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product?.Name ?? "Unknown",
                        SoldQty = available,
                        ReturnQty = 0,
                        UnitPrice = actualUnitPriceAfterDiscount,
                        IsSelected = false
                    };
                    vm.PropertyChanged += (s, ev) => UpdateTotals();
                    _returnItems.Add(vm);
                }
            }
            UpdateTotals();
        }

        private void txtReplaceSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = txtReplaceSearch.Text.Trim();
            if (query.Length < 2) { popupReplace.IsOpen = false; return; }

            var results = _productService.Search(query);
            if (results.Any())
            {
                lstReplaceResults.ItemsSource = results;
                popupReplace.IsOpen = true;
            }
            else popupReplace.IsOpen = false;
        }

        private void txtReplaceSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Down && popupReplace.IsOpen)
            {
                lstReplaceResults.Focus();
                if (lstReplaceResults.Items.Count > 0 && lstReplaceResults.SelectedIndex < 0)
                {
                    lstReplaceResults.SelectedIndex = 0;
                }
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
            {
                SelectReplaceProductFromList(product);
            }
        }

        private void SelectReplaceProductFromList(Product product)
        {
            var existing = _replacementItems.FirstOrDefault(i => i.ProductId == product.Id);
            if (existing != null) existing.Quantity++;
            else
            {
                var vm = new ReplacementItemVM
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.SalePrice,
                    Quantity = 1
                };
                vm.PropertyChanged += (s, ev) => UpdateTotals();
                _replacementItems.Add(vm);
            }
            txtReplaceSearch.Text = "";
            lstReplaceResults.SelectedItem = null;
            popupReplace.IsOpen = false;
            txtReplaceSearch.Focus();
        }

        private void RemoveReplacementItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ReplacementItemVM vm)
            {
                _replacementItems.Remove(vm);
                UpdateTotals();
            }
        }

        private void UpdateTotals()
        {
            decimal returnedTotal = _returnItems.Where(i => i.IsSelected).Sum(i => i.ReturnQty * i.UnitPrice);
            decimal replacementTotal = _replacementItems.Sum(i => i.Total);
            decimal diff = replacementTotal - returnedTotal;

            if (lblNetDiffValue != null)
            {
                lblNetDiffValue.Text = (diff >= 0 ? "+" : "") + $"{(diff).FormatRs()}";
                lblNetDiffValue.Foreground = diff >= 0 ? System.Windows.Media.Brushes.DarkRed : System.Windows.Media.Brushes.DarkGreen;
            }

            decimal prevBal = _currentSale?.Customer?.Balance ?? 0;
            decimal totalPayable = diff + prevBal;

            if (lblReturnedTotal != null) lblReturnedTotal.Text = $"{(returnedTotal).FormatRs()}";
            if (lblReplacementTotal != null) lblReplacementTotal.Text = $"{(replacementTotal).FormatRs()}";

            if (txtPaidByCustomer != null && !txtPaidByCustomer.IsFocused)
            {
                txtPaidByCustomer.TextChanged -= txtPaidByCustomer_TextChanged;
                txtPaidByCustomer.Text = Math.Abs(totalPayable).ToString("0.00");
                txtPaidByCustomer.TextChanged += txtPaidByCustomer_TextChanged;
            }

            if (prevBal != 0)
            {
                if (gridPreviousDues != null) gridPreviousDues.Visibility = Visibility.Visible;
                if (lblPreviousDuesVal != null) lblPreviousDuesVal.Text = $"{(prevBal).FormatRs()}";
                if (lblAdjustmentHint != null) lblAdjustmentHint.Visibility = Visibility.Visible;
            }
            else
            {
                if (gridPreviousDues != null) gridPreviousDues.Visibility = Visibility.Collapsed;
                if (lblAdjustmentHint != null) lblAdjustmentHint.Visibility = Visibility.Collapsed;
            }

            if (totalPayable > 0)
            {
                lblNetDifference.Text = $"Net Due: Rs {totalPayable:N2}";
                lblNetDifference.Foreground = System.Windows.Media.Brushes.Red;
                if (cmbPaymentAction != null && cmbPaymentAction.SelectedIndex != 0)
                    cmbPaymentAction.SelectedIndex = 0;
            }
            else if (totalPayable < 0)
            {
                lblNetDifference.Text = $"Total Credit: Rs {Math.Abs(totalPayable):N2}";
                lblNetDifference.Foreground = System.Windows.Media.Brushes.Green;
                if (cmbPaymentAction != null && cmbPaymentAction.SelectedIndex != 1)
                    cmbPaymentAction.SelectedIndex = 1;
            }
            else
            {
                lblNetDifference.Text = "Rs 0.00 (Balanced)";
                lblNetDifference.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
                if (cmbPaymentAction != null && cmbPaymentAction.SelectedIndex != 1)
                    cmbPaymentAction.SelectedIndex = 1;
            }
            
            if (btnConfirmProcess != null)
                btnConfirmProcess.Content = totalPayable >= 0 ? "Confirm & Process" : "Process Refund & Complete";
        }

        private void CmbPaymentAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lblPaymentAmount == null) return;
            if (cmbPaymentAction.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "StoreRefunds")
            {
                lblPaymentAmount.Text = "Amount Refunded to Customer";
            }
            else
            {
                lblPaymentAmount.Text = "Amount Received by Customer";
            }
        }

        private void txtPaidByCustomer_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void ProcessReturn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSale == null) return;

            var returned = _returnItems.Where(i => i.IsSelected && i.ReturnQty > 0)
                           .Select(i => (i.ProductId, i.ReturnQty, i.UnitPrice)).ToList();
            
            if (!returned.Any())
            {
                MessageBox.Show("Please select at least one item to return.");
                return;
            }

            foreach (var item in _returnItems.Where(i => i.IsSelected))
            {
                if (item.ReturnQty > item.SoldQty)
                {
                    MessageBox.Show($"Return quantity for {item.ProductName} cannot exceed sold quantity.");
                    return;
                }
            }

            var replaced = _replacementItems.Where(i => i.Quantity > 0)
                           .Select(i => (i.ProductId, i.Quantity, i.UnitPrice)).ToList();

            decimal retValue = _returnItems.Where(i => i.IsSelected).Sum(i => i.ReturnQty * i.UnitPrice);
            decimal repValue = _replacementItems.Sum(i => i.Total);
            // netReturnDiff: positive = customer owes more, negative = store owes customer (refund)
            decimal netReturnDiff = repValue - retValue;
            decimal existingDue = _currentSale?.Customer?.Balance ?? 0;
            // totalPayable = ExistingDue + (ReplacementTotal - ReturnedTotal)
            decimal totalPayable = existingDue + netReturnDiff;

            decimal amountInput = 0;
            if (!string.IsNullOrWhiteSpace(txtPaidByCustomer.Text))
                decimal.TryParse(txtPaidByCustomer.Text, out amountInput);

            bool isStoreRefunding = cmbPaymentAction.SelectedItem is ComboBoxItem cbi && 
                                    cbi.Tag?.ToString() == "StoreRefunds";

            decimal refundPaid = isStoreRefunding ? amountInput : 0;
            decimal extraReceived = !isStoreRefunding ? amountInput : 0;

            bool isWalkIn = _currentSale?.Customer?.IsWalkIn ?? true;

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
                            salesAdjustment = overDialog.ReturnedAmount; // The excess kept
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
                            salesAdjustment = -(totalPayable - amountInput); // Loss
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

            string balanceMsg = remainingBalance > 0
                ? $"Remaining Due: Rs {remainingBalance:N2} (added to account)"
                : remainingBalance < 0
                    ? $"Credit Balance: Rs {Math.Abs(remainingBalance):N2} (store owes customer)"
                    : "Fully Settled ";

            var confirm = MessageBox.Show(
                $"Return Transaction Summary \n" +
                $"Returned Value  : Rs {retValue:N2}\n" +
                $"Replacement Val : Rs {repValue:N2}\n" +
                $"Net Difference  : Rs {netReturnDiff:N2}\n" +
                $"Existing Due    : Rs {existingDue:N2}\n" +
                $"Total Payable   : Rs {totalPayable:N2}\n" +
                $"Payment Action  : {(isStoreRefunding ? "Store Refunds" : "Customer Pays")}\n" +
                $"Amount          : Rs {amountInput:N2}\n" +
                $"{balanceMsg}\n\n" +
                $"Confirm & Process?", "Confirm Transaction", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var result = _returnService.ProcessReturn(
                _currentSale.Id,
                _currentSale.CustomerId,
                returned,
                replaced,
                refundPaid,
                extraReceived,
                salesAdjustment,
                replaced.Any() ? "Replace" : "Refund",
                txtReturnNotes.Text);

            if (result.Success && result.Return != null)
            {
                new Helpers.ReceiptPrinter().Print(result.Return);
                MessageBox.Show("Transaction completed successfully.");
                ResetView();
            }
            else
            {
                MessageBox.Show(result.Message, "Error");
            }
        }

        private void RecordCustomerPayment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSale?.Customer is Customer customer)
            {
                if (customer.IsWalkIn)
                {
                    MessageBox.Show("Cannot record payment for Walk-in customer.", "Not Allowed");
                    return;
                }

                var dialog = new CustomerPaymentDialog(customer);
                if (dialog.ShowDialog() == true)
                {
                    var updated = new CustomerService().GetById(customer.Id);
                    if (updated != null)
                    {
                        _currentSale.Customer = updated;
                        UpdateTotals();
                    }
                }
            }
        }

        private void ViewCustomerHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSale?.Customer is Customer customer)
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

        private void ResetView()
        {
            _currentSale = null;
            _returnItems.Clear();
            _replacementItems.Clear();
            txtReturnNotes.Text = "";
            txtPaidByCustomer.Text = "";
            cmbInvoiceNo.Text = "";
            lblInvoiceInfo.Text = "";
            if (borderInvoiceInfo != null) borderInvoiceInfo.Visibility = System.Windows.Visibility.Collapsed;
            // panelCustomerActions removed from UI
            LoadInitialData();
            UpdateTotals();
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
                // Allow Enter in specific controls (buttons, search lists) but block default navigation
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

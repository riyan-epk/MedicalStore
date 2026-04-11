using MedicalStore.Common.Helpers;
using System.Windows;
using System.Windows.Media;

namespace MedicalStore.Views
{
    /// <summary>
    /// Result of the overpayment confirmation:
    ///   ReturnExcess  hand back the extra cash; record only bill total as revenue.
    ///   AddToCredit   retain excess as customer credit balance.
    ///   Cancelled     user dismissed the dialog; do NOT proceed with the sale.
    /// </summary>
    public enum OverpaymentAction { ReturnExcess, AddToCredit, AdjustSale, Cancelled }

    public partial class OverpaymentDialog : Window
    {
        // Public read-back
        public OverpaymentAction SelectedAction { get; private set; } = OverpaymentAction.Cancelled;
        public decimal ReturnedAmount { get; private set; } = 0;

        private bool _returnSelected = false;   // true  Return excess
        private bool _creditSelected = false;   // true  Add to credit
        private bool _adjustSelected = false;   // true  Adjust sale

        // Colours used for the active / inactive card borders
        private static readonly SolidColorBrush _activeGreen  = new(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush _activeBlue   = new(Color.FromRgb(25, 118, 210));
        private static readonly SolidColorBrush _inactive      = new(Color.FromRgb(208, 208, 208));

        // Constructor
        public OverpaymentDialog(decimal billTotal, decimal amountReceived, decimal excessAmount, bool isWalkInCustomer = false)
        {
            InitializeComponent();

            lblBillTotal.Text = $"{(billTotal).FormatRs()}";
            lblReceived.Text  = $"{(amountReceived).FormatRs()}";
            lblExcess.Text    = $"{(excessAmount).FormatRs()}";
            
            if (txtReturnAmount != null)
                txtReturnAmount.Text = excessAmount.ToString("0.00");

            if (isWalkInCustomer)
            {
                if (borderCreditOption != null)
                    borderCreditOption.Visibility = Visibility.Collapsed;
            }

            // Default: "Return Excess to Customer" is pre-selected so cashier
            // can just press Confirm without an extra click.
            _returnSelected      = true;
            _creditSelected      = false;
            _adjustSelected      = false;
            SelectedAction       = OverpaymentAction.ReturnExcess;
            btnConfirm.IsEnabled = true;

            // Apply visual selection after controls are loaded
            Loaded += (_, _) => SetReturnSelected();
        }

        // Option 1 Return excess
        private void ReturnOption_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _returnSelected = true;
            _creditSelected = false;
            _adjustSelected = false;
            SetReturnSelected();
            btnConfirm.IsEnabled = true;
        }

        private void SetReturnSelected()
        {
            // Active card
            borderReturnOption.BorderBrush = _activeGreen;
            borderReturnOption.Background  = new SolidColorBrush(Color.FromRgb(241, 248, 233));
            chkReturn.Text                 = "✓";
            if (txtReturnAmount != null)
            {
                txtReturnAmount.IsEnabled = true;
                txtReturnAmount.Background = Brushes.White;
            }

            // Inactive card
            borderCreditOption.BorderBrush = _inactive;
            borderCreditOption.Background  = new SolidColorBrush(Color.FromRgb(250, 250, 250));
            chkCredit.Text                 = "";

            if (borderAdjustOption != null)
            {
                borderAdjustOption.BorderBrush = _inactive;
                borderAdjustOption.Background  = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                chkAdjust.Text                 = "";
            }
        }

        // Option 2 Add to credit
        private void CreditOption_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _creditSelected = true;
            _returnSelected = false;
            _adjustSelected = false;
            SetCreditSelected();
            btnConfirm.IsEnabled = true;
        }

        private void SetCreditSelected()
        {
            // Active card
            borderCreditOption.BorderBrush = _activeBlue;
            borderCreditOption.Background  = new SolidColorBrush(Color.FromRgb(227, 242, 253));
            chkCredit.Text                 = "✓";

            // Inactive card
            borderReturnOption.BorderBrush = _inactive;
            borderReturnOption.Background  = new SolidColorBrush(Color.FromRgb(241, 248, 233));
            chkReturn.Text                 = "";
            if (txtReturnAmount != null)
            {
                txtReturnAmount.IsEnabled = false;
                txtReturnAmount.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            }

            if (borderAdjustOption != null)
            {
                borderAdjustOption.BorderBrush = _inactive;
                borderAdjustOption.Background  = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                chkAdjust.Text                 = "";
            }
        }

        private void AdjustOption_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _adjustSelected = true;
            _returnSelected = false;
            _creditSelected = false;
            SetAdjustSelected();
            btnConfirm.IsEnabled = true;
        }

        private void SetAdjustSelected()
        {
            // Active card
            borderAdjustOption.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 124, 0));
            borderAdjustOption.Background  = new SolidColorBrush(Color.FromRgb(255, 243, 224));
            chkAdjust.Text                 = "✓";

            // Inactive cards
            borderReturnOption.BorderBrush = _inactive;
            borderReturnOption.Background  = new SolidColorBrush(Color.FromRgb(241, 248, 233));
            chkReturn.Text                 = "";
            if (txtReturnAmount != null)
            {
                txtReturnAmount.IsEnabled = false;
                txtReturnAmount.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            }

            borderCreditOption.BorderBrush = _inactive;
            borderCreditOption.Background  = new SolidColorBrush(Color.FromRgb(250, 250, 250));
            chkCredit.Text                 = "";
        }

        // Buttons
        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (_returnSelected)
            {
                SelectedAction = OverpaymentAction.ReturnExcess;
                if (decimal.TryParse(txtReturnAmount.Text, out decimal retVal))
                    ReturnedAmount = retVal;
                else
                    ReturnedAmount = 0;
            }
            else if (_creditSelected)
            {
                SelectedAction = OverpaymentAction.AddToCredit;
            }
            else if (_adjustSelected)
            {
                SelectedAction = OverpaymentAction.AdjustSale;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = OverpaymentAction.Cancelled;
            DialogResult = false;
            Close();
        }

        private void NumberValidation(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9.]+$");
        }

        private void txtReturnAmount_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
                tb.SelectAll();
        }
    }
}

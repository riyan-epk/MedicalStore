using MedicalStore.Common.Helpers;
using System.Windows;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;
using System.Windows.Controls;

namespace MedicalStore.Views
{
    public partial class SupplierPaymentDialog : Window
    {
        private readonly SupplierService _supplierService = new();
        private readonly Supplier _supplier;

        public SupplierPaymentDialog(Supplier supplier)
        {
            InitializeComponent();
            _supplier = supplier;
            lblSupplierName.Text = supplier.Name;
            lblBalance.Text = $"{(supplier.Balance).FormatRs()}";
            txtAmount.Text = supplier.Balance.ToString("F2");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Invalid Input");
                return;
            }

            var method = (cmbMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
            var result = _supplierService.RecordPayment(_supplier.Id, amount, method, txtNotes.Text);

            if (result.Success)
            {
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

        private void NumberValidation(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9.]+$");
        }
    }
}

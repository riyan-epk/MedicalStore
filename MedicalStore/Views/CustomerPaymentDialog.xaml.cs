using MedicalStore.Common.Helpers;
using System.Windows;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class CustomerPaymentDialog : Window
    {
        private readonly CustomerService _customerService = new();
        private readonly Customer _customer;

        public CustomerPaymentDialog(Customer customer)
        {
            InitializeComponent();
            _customer = customer;
            lblCustomerName.Text = customer.Name;
            lblBalance.Text = $"{(customer.Balance).FormatRs()}";
            if (customer.Balance > 0)
                txtAmount.Text = customer.Balance.ToString("F2");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Invalid Input");
                return;
            }

            var result = _customerService.RecordPayment(_customer.Id, amount, txtNotes.Text);

            if (result.Success)
            {
                // Fetch the fresh customer balance to show in confirmation
                var updated = _customerService.GetById(_customer.Id);
                string balanceMsg;
                if (updated == null || updated.Balance <= 0)
                    balanceMsg = "Account fully settled.";
                else
                    balanceMsg = $"Remaining balance: Rs {updated.Balance:N2}";

                MessageBox.Show(
                    $"Payment of Rs {amount:N2} recorded.\n{balanceMsg}",
                    "Payment Recorded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

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

using System.Windows;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class SupplierDialog : Window
    {
        public Supplier Supplier { get; private set; }

        public SupplierDialog(Supplier? supplier = null)
        {
            InitializeComponent();
            if (supplier != null)
            {
                Supplier = supplier;
                lblTitle.Text = "Edit Supplier";
                txtName.Text = supplier.Name;
                txtPhone.Text = supplier.Phone;
                txtAddress.Text = supplier.Address;
                txtBalance.Text = supplier.Balance.ToString("0.##");
            }
            else
            {
                Supplier = new Supplier();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Name is required.", "Validation");
                return;
            }
            Supplier.Name = txtName.Text.Trim();
            Supplier.Phone = txtPhone.Text.Trim();
            Supplier.Address = txtAddress.Text.Trim();
            
            if (decimal.TryParse(txtBalance.Text, out decimal bal))
                Supplier.Balance = bal;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumberValidation(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9.-]+$");
        }
    }
}

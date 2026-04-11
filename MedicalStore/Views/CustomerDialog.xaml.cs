using System.Windows;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class CustomerDialog : Window
    {
        public Customer Customer { get; private set; }

        public CustomerDialog(Customer? customer = null)
        {
            InitializeComponent();
            if (customer != null)
            {
                Customer = customer;
                lblTitle.Text = "Edit Customer";
                txtName.Text = customer.Name;
                txtPhone.Text = customer.Phone;
                txtAddress.Text = customer.Address;
            }
            else
            {
                Customer = new Customer();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Name is required.", "Validation");
                return;
            }
            Customer.Name = txtName.Text.Trim();
            Customer.Phone = txtPhone.Text.Trim();
            Customer.Address = txtAddress.Text.Trim();
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

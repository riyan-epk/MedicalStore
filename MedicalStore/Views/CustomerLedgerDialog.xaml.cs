using System.Windows;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class CustomerLedgerDialog : Window
    {
        public CustomerLedgerDialog(Customer customer)
        {
            InitializeComponent();
            lblCustomerName.Text = customer.Name;
            lblCurrentBalance.Text = $"Current Net Balance: Rs {customer.Balance:N2}";
            
            if (customer.Balance > 0)
                lblCurrentBalance.Foreground = System.Windows.Media.Brushes.Red;
            else if (customer.Balance < 0)
                lblCurrentBalance.Foreground = System.Windows.Media.Brushes.Green;

            LoadData(customer.Id);
        }

        private void LoadData(int customerId)
        {
            var service = new CustomerService();
            dgLedger.ItemsSource = service.GetLedger(customerId);
        }
    }
}

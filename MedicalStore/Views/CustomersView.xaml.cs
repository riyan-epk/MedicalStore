using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.Common.Helpers;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class CustomersView : Page
    {
        private readonly CustomerService _customerService = new();

        public CustomersView()
        {
            InitializeComponent();
            // "Delete All" wipes every customer and their entire sales/returns/payments
            // history — an irreversible, admin-only action. Hide it from non-admins.
            if (!AppSession.HasPermission(Common.Enums.Permission.ManageUsers))
                btnDeleteAll.Visibility = Visibility.Collapsed;
            LoadCustomers();
        }

        private void LoadCustomers()
        {
            try
            {
                dgCustomers.ItemsSource = _customerService.GetAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading customers: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddCustomer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new CustomerDialog();
                if (dialog.ShowDialog() == true)
                {
                    var result = _customerService.Create(dialog.Customer);
                    if (result.Success)
                    {
                        LoadCustomers();
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgCustomers_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (dgCustomers.SelectedItem is Customer customer && !customer.IsWalkIn)
                {
                    var dialog = new CustomerDialog(customer);
                    if (dialog.ShowDialog() == true)
                    {
                        var result = _customerService.Update(dialog.Customer);
                        if (result.Success)
                        {
                            LoadCustomers();
                        }
                        else
                        {
                            MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecordPayment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dgCustomers.SelectedItem is not Customer customer)
                {
                    MessageBox.Show("Select a customer first.", "Info");
                    return;
                }

                if (customer.Balance == 0)
                {
                    MessageBox.Show("This customer has no outstanding balance.", "Info");
                    return;
                }

                bool isRefund = customer.Balance < 0;
                string promptMsg = isRefund 
                    ? $"Customer: {customer.Name}\nCredit Balance: Rs {Math.Abs(customer.Balance):N2}\n(Store owes customer)\n\nEnter amount to refund to customer:"
                    : $"Customer: {customer.Name}\nBalance Due: Rs {customer.Balance:N2}\n(Customer owes store)\n\nEnter payment amount received:";

                var input = Microsoft.VisualBasic.Interaction.InputBox(
                    promptMsg, 
                    isRefund ? "Refund Customer" : "Record Payment", 
                    Math.Abs(customer.Balance).ToString("F2"));

                if (string.IsNullOrWhiteSpace(input)) return;

                if (decimal.TryParse(input, out var amount) && amount > 0)
                {
                    // If refunding the credit, we pass negative amount to reduce the negative balance to 0
                    decimal finalAmount = isRefund ? -amount : amount;
                    var result = _customerService.RecordPayment(customer.Id, finalAmount, isRefund ? "Refunded to customer" : "Manual payment");
                    MessageBox.Show(result.Message, result.Success ? "Success" : "Error");
                    LoadCustomers();
                }
                else
                {
                    MessageBox.Show("Invalid amount entered.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not record payment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDues_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                dgCustomers.ItemsSource = _customerService.GetBalances();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching balances: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dgCustomers.SelectedItem is Customer customer && !customer.IsWalkIn)
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var query = txtSearch.Text.ToLower();
                if (string.IsNullOrWhiteSpace(query))
                {
                    LoadCustomers();
                    return;
                }
                dgCustomers.ItemsSource = _customerService.GetAll().Where(c => 
                    c.Name.ToLower().Contains(query) || 
                    (c.Phone != null && c.Phone.Contains(query))).ToList();
            }
            catch (Exception ex)
            {
                // Silent search error but maybe log it
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
        }

        private void DeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is Customer customer)
                {
                    var confirm = MessageBox.Show($"Are you sure you want to delete customer '{customer.Name}'?\nThis will deactivate the customer.", 
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    
                    if (confirm == MessageBoxResult.Yes)
                    {
                        var result = _customerService.Delete(customer.Id);
                        if (result.Success)
                        {
                            MessageBox.Show(result.Message, "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadCustomers();
                        }
                        else
                        {
                            MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deletion failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteAllCustomers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Defense in depth: block the destructive action even if the button is
                // somehow reachable by a non-admin.
                if (!AppSession.HasPermission(Common.Enums.Permission.ManageUsers))
                {
                    MessageBox.Show("You do not have permission to perform this action.",
                        "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show("CRITICAL ACTION: Are you sure you want to delete ALL customers and their entire transaction history (Sales, Returns, Payments)?\n\nThis action CANNOT be undone.", 
                    "Confirm Mass Deletion", MessageBoxButton.YesNo, MessageBoxImage.Stop);

                if (result == MessageBoxResult.Yes)
                {
                    // Second confirmation for such a destructive action
                    var doubleCheck = MessageBox.Show("Are you ABSOLUTELY certain? All sales tracking for these customers will be lost.", 
                        "Final Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (doubleCheck == MessageBoxResult.Yes)
                    {
                        var deleteResult = _customerService.DeleteAllCustomers();
                        if (deleteResult.Success)
                        {
                            MessageBox.Show(deleteResult.Message, "Mass Deletion Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadCustomers();
                        }
                        else
                        {
                            MessageBox.Show(deleteResult.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Mass deletion failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

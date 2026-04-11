using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;
using System.Windows;
using System.Windows.Controls;

namespace MedicalStore.Views
{
    public partial class ExpensesView : Page
    {
        private readonly ExpenseService _expenseService = new();
        private int _editingId = 0;

        public ExpensesView()
        {
            InitializeComponent();
            dpDate.SelectedDate = DateTime.Today;
            LoadExpenses();
        }

        private void LoadExpenses()
        {
            dgExpenses.ItemsSource = _expenseService.GetAll();
        }

        private void SaveExpense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Expense Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("Please enter a valid amount.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var date = dpDate.SelectedDate ?? DateTime.Today;
                var description = txtDescription.Text;

                if (_editingId == 0)
                {
                    var result = _expenseService.Create(txtName.Text, amount, date, description);
                    if (!result.Success)
                    {
                        MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    var result = _expenseService.Update(_editingId, txtName.Text, amount, date, description);
                    if (!result.Success)
                    {
                        MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                ClearForm();
                LoadExpenses();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save expense: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            _editingId = 0;
            txtName.Text = "";
            txtAmount.Text = "";
            dpDate.SelectedDate = DateTime.Today;
            txtDescription.Text = "";
            btnSave.Content = "Save Expense";
        }

        private void EditExpense_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Expense expense)
            {
                _editingId = expense.Id;
                txtName.Text = expense.Name;
                txtAmount.Text = expense.Amount.ToString("0.##");
                dpDate.SelectedDate = expense.Date;
                txtDescription.Text = expense.Description;
                btnSave.Content = "Update Expense";
            }
        }

        private void DeleteExpense_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Expense expense)
            {
                var confirm = MessageBox.Show($"Are you sure you want to delete expense '{expense.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    var result = _expenseService.Delete(expense.Id);
                    if (result.Success)
                    {
                        LoadExpenses();
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void NumberValidation(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9.]+$");
        }
    }
}

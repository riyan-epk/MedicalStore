using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class SuppliersView : Page
    {
        private readonly SupplierService _supplierService = new();

        public SuppliersView()
        {
            InitializeComponent();
            LoadSuppliers();
        }

        private void LoadSuppliers(string? filter = null)
        {
            try
            {
                var all = _supplierService.GetAll();

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var q = filter.ToLower();
                    all = all.Where(s =>
                        s.Name.ToLower().Contains(q) ||
                        (s.Phone != null && s.Phone.Contains(q)) ||
                        (s.Address != null && s.Address.ToLower().Contains(q))
                    ).ToList();
                }

                dgSuppliers.ItemsSource = all;

                if (lblSupCount != null)
                    lblSupCount.Text = $"{all.Count} supplier{(all.Count == 1 ? "" : "s")}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading suppliers: {ex.Message}", "Database Error");
            }
        }

        private void AddSupplier_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SupplierDialog();
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                {
                    // Explicitly call Create to save to DB
                    var result = _supplierService.Create(dialog.Supplier);
                    if (result.Success)
                    {
                        LoadSuppliers();
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error");
            }
        }

        private void EditSupplier_Click(object sender, RoutedEventArgs e)
        {
            if (dgSuppliers.SelectedItem is Supplier supplier)
            {
                OpenEditDialog(supplier);
            }
            else
            {
                MessageBox.Show("Please select a supplier to edit.", "Selection Required");
            }
        }

        private void dgSuppliers_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgSuppliers.SelectedItem is Supplier supplier)
            {
                OpenEditDialog(supplier);
            }
        }

        private void OpenEditDialog(Supplier supplier)
        {
            try
            {
                var dialog = new SupplierDialog(supplier);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                {
                    var result = _supplierService.Update(dialog.Supplier);
                    if (result.Success)
                    {
                        LoadSuppliers(txtSearch.Text);
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error");
            }
        }

        private void RecordPayment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supplier = dgSuppliers.SelectedItem as Supplier;
                if (supplier == null)
                {
                    MessageBox.Show("Please select a supplier first.", "Selection Required");
                    return;
                }

                var dialog = new SupplierPaymentDialog(supplier);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                {
                    LoadSuppliers(txtSearch.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error recording payment: {ex.Message}", "Error");
            }
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dgSuppliers.SelectedItem is Supplier supplier)
                {
                    var dialog = new SupplierHistoryDialog(supplier);
                    dialog.Owner = Window.GetWindow(this);
                    dialog.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Please select a supplier first.", "Selection Required");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error viewing history: {ex.Message}", "Error");
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                LoadSuppliers(txtSearch.Text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Supplier search error: {ex.Message}");
            }
        }

        private void DeleteSupplier_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is Supplier supplier)
                {
                    var confirm = MessageBox.Show(
                        $"Delete supplier '{supplier.Name}'?\nThis will deactivate the supplier.",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (confirm == MessageBoxResult.Yes)
                    {
                        var result = _supplierService.Delete(supplier.Id);
                        if (result.Success)
                        {
                            LoadSuppliers(txtSearch.Text);
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
                MessageBox.Show($"Deletion failed: {ex.Message}", "Error");
            }
        }
    }
}

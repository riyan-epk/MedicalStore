using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.Common.Constants;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class ProductsView : Page
    {
        private readonly ProductService _productService = new();

        public ProductsView()
        {
            InitializeComponent();
            LoadProducts();
            LoadCategories();

            // Units/Pack column is now always visible natively.
        }

        private void LoadCategories()
        {
            try
            {
                icCategories.ItemsSource = _productService.GetCategories();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
            }
        }

        private void LoadProducts()
        {
            try
            {
                dgProducts.ItemsSource = _productService.GetAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var query = txtSearch.Text.Trim();
                dgProducts.ItemsSource = string.IsNullOrEmpty(query) 
                    ? _productService.GetAll() 
                    : _productService.Search(query);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ProductDialog();
                if (dialog.ShowDialog() == true)
                {
                    _productService.Create(dialog.Product);
                    LoadProducts();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not add product: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgProducts_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (dgProducts.SelectedItem is Product product)
                {
                    var dialog = new ProductDialog(product);
                    if (dialog.ShowDialog() == true)
                    {
                        _productService.Update(dialog.Product);
                        LoadProducts();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating product: {ex.Message}", "Error");
            }
        }

        private void AllProducts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtSearch.Text = string.Empty;
                LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void CategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is Category category)
                {
                    txtSearch.Text = string.Empty;
                    dgProducts.ItemsSource = _productService.GetAll().Where(p => p.CategoryId == category.Id).ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error filtering by category: {ex.Message}");
            }
        }

        private void LowStock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                dgProducts.ItemsSource = _productService.GetLowStock();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void NearExpiry_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                dgProducts.ItemsSource = _productService.GetNearExpiry();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is Product product)
                {
                    var confirm = MessageBox.Show($"Are you sure you want to delete product '{product.Name}'?\nThis will deactivate the product.", 
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (confirm == MessageBoxResult.Yes)
                    {
                        var result = _productService.Delete(product.Id);
                        if (result.Success)
                        {
                            MessageBox.Show(result.Message, "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadProducts();
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

        private void BulkImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ProductImportDialog
                {
                    Owner = Window.GetWindow(this)
                };

                dialog.ShowDialog();

                if (dialog.ImportCompleted)
                {
                    LoadProducts();
                    LoadCategories();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

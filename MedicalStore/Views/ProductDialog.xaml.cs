using System.Windows;
using MedicalStore.BLL.Services;
using MedicalStore.Common.Constants;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class ProductDialog : Window
    {
        public Product Product { get; private set; }
        private readonly ProductService _productService = new();
        private readonly SupplierService _supplierService = new();

        public ProductDialog(Product? product = null)
        {
            InitializeComponent();
            LoadCategories();
            LoadSuppliers();

            if (product != null)
            {
                Product = product;
                lblTitle.Text = "Edit Product";
                PopulateFields();
            }
            else
            {
                Product = new Product();
            }
        }

        private void LoadCategories()
        {
            cmbCategory.ItemsSource = _productService.GetCategories();
            if (cmbCategory.Items.Count > 0 && cmbCategory.SelectedIndex == -1)
                cmbCategory.SelectedIndex = 0;
        }

        private void LoadSuppliers()
        {
            cmbSupplier.ItemsSource = _supplierService.GetAll();
            if (cmbSupplier.Items.Count > 0 && cmbSupplier.SelectedIndex == -1)
                cmbSupplier.SelectedIndex = 0;
        }

        private void PopulateFields()
        {
            txtName.Text = Product.Name;
            cmbCategory.SelectedValue = Product.CategoryId;
            cmbSupplier.SelectedValue = Product.SupplierId;
            txtCompany.Text = Product.Company;
            txtBatchNo.Text = Product.BatchNo;
            txtBarcode.Text = Product.Barcode;
            txtPurchasePrice.Text = Product.PurchasePrice.ToString();
            txtSalePrice.Text = Product.SalePrice.ToString();
            txtQuantity.Text = Product.Quantity.ToString();
            txtMinStock.Text = Product.MinStockLevel.ToString();
            dpManufDate.SelectedDate = Product.ManufDate;
            dpExpiryDate.SelectedDate = Product.ExpiryDate;

            // Pack-unit fields
            txtUnitsPerPack.Text = Product.UnitsPerPack.ToString();
            txtPackPrice.Text    = Product.PackPrice.ToString();
            if (Product.UnitsPerPack > 0 && Product.StockUnits > 0)
            {
                txtNoOfPacks.Text = (Product.StockUnits / Product.UnitsPerPack).ToString();
            }
            RefreshUnitPriceDisplay();
            UpdateStockDisplay();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Product name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpManufDate.SelectedDate.HasValue && dpExpiryDate.SelectedDate.HasValue)
            {
                if (dpManufDate.SelectedDate.Value >= dpExpiryDate.SelectedDate.Value)
                {
                    MessageBox.Show("Manufacturer date must be prior to the Expiry date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            Product.Name       = txtName.Text.Trim();
            Product.CategoryId = (int)(cmbCategory.SelectedValue ?? 1);
            Product.SupplierId = (int)(cmbSupplier.SelectedValue ?? 1);
            Product.Company    = txtCompany.Text.Trim();
            Product.BatchNo    = txtBatchNo.Text.Trim();
            Product.Barcode    = string.IsNullOrWhiteSpace(txtBarcode.Text) ? null : txtBarcode.Text.Trim();

            if (decimal.TryParse(txtPurchasePrice.Text, out var pp)) Product.PurchasePrice = pp;
            if (decimal.TryParse(txtSalePrice.Text,     out var sp)) Product.SalePrice     = sp;
            if (int.TryParse(txtQuantity.Text,  out var qty)) Product.Quantity     = qty;
            if (int.TryParse(txtMinStock.Text,  out var ms))  Product.MinStockLevel = ms;

            Product.ManufDate  = dpManufDate.SelectedDate;
            Product.ExpiryDate = dpExpiryDate.SelectedDate;

            // Pack-unit fields
            if (int.TryParse(txtUnitsPerPack.Text, out var upp) && upp > 0)
                Product.UnitsPerPack = upp;
            else
                Product.UnitsPerPack = 1;

            if (decimal.TryParse(txtPackPrice.Text, out var packP))
                Product.PackPrice = packP;

            // Auto-derive UnitPrice
            Product.UnitPrice = Product.UnitsPerPack > 0
                ? Product.PackPrice / Product.UnitsPerPack
                : Product.PackPrice;

            // StockUnits = Quantity
            Product.StockUnits = Product.Quantity;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void NumberValidation(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9.]+$");
        }

        /// <summary>Recalculates unit price when pack price or units-per-pack change.</summary>
        private void PackPricing_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RefreshUnitPriceDisplay();
        }

        private void RefreshUnitPriceDisplay()
        {
            if (txtUnitsPerPack == null || txtPackPrice == null || txtUnitPriceDisplay == null || txtNoOfPacks == null || txtQuantity == null || txtPurchasePrice == null) return;

            if (decimal.TryParse(txtPackPrice.Text, out decimal packP) &&
                int.TryParse(txtUnitsPerPack.Text,  out int    upp)    && upp > 0)
            {
                decimal unitP = packP / upp;
                txtUnitPriceDisplay.Text = unitP.ToString("N2");
                txtPurchasePrice.Text = unitP.ToString("N2");
            }
            else
            {
                txtUnitPriceDisplay.Text = "";
            }

            if (int.TryParse(txtNoOfPacks.Text, out int numPacks) &&
                int.TryParse(txtUnitsPerPack.Text, out int unitsPer))
            {
                txtQuantity.Text = (numPacks * unitsPer).ToString();
            }
        }

        private void UpdateStockDisplay()
        {
            if (lblStockDisplay == null) return;

            int stockUnits  = Product.StockUnits > 0 ? Product.StockUnits : Product.Quantity;
            int unitsPerPack = Product.UnitsPerPack > 1 ? Product.UnitsPerPack : 1;

            if (unitsPerPack > 1)
            {
                int packs = stockUnits / unitsPerPack;
                int loose = stockUnits % unitsPerPack;
                lblStockDisplay.Text = $"Current Stock: {stockUnits} units = {packs} packs + {loose} loose units";
            }
            else
            {
                lblStockDisplay.Text = $"Current Stock: {stockUnits} units";
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Add Category", "Enter Category Name:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Answer))
            {
                using var db = new DAL.AppDbContext();
                var category = new Category { Name = dialog.Answer };
                db.Categories.Add(category);
                db.SaveChanges();
                LoadCategories();
                cmbCategory.SelectedValue = category.Id;
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCategory.SelectedItem is Category category)
            {
                using var db = new DAL.AppDbContext();
                bool hasProducts = db.Products.Any(p => p.CategoryId == category.Id);

                if (hasProducts)
                {
                    MessageBox.Show($"Cannot delete '{category.Name}' because it is assigned to existing products.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (MessageBox.Show($"Are you sure you want to permanently delete category '{category.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    var catToDelete = db.Categories.Find(category.Id);
                    if (catToDelete != null)
                    {
                        db.Categories.Remove(catToDelete);
                        db.SaveChanges();
                        LoadCategories();
                    }
                }
            }
        }

        private void AddSupplier_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SupplierDialog(null);
            if (dialog.ShowDialog() == true)
            {
                var result = _supplierService.Create(dialog.Supplier);
                if (result.Success)
                {
                    LoadSuppliers();
                    cmbSupplier.SelectedValue = dialog.Supplier.Id;
                }
                else
                {
                    MessageBox.Show(result.Message, "Error");
                }
            }
        }
    }
}

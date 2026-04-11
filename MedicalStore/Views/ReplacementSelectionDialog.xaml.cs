using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    /// <summary>
    /// Result returned after a user confirms a replacement selection.
    /// </summary>
    public class ReplacementSelectionResult
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int StockAvailable { get; set; }
    }

    /// <summary>
    /// Professional dialog for selecting a replacement product in the Manual Refund workflow.
    /// It knows about the refund item being replaced and enforces quantity constraints.
    /// </summary>
    public partial class ReplacementSelectionDialog : Window
    {
        private readonly ProductService _productService = new();
        private readonly int _maxQty;          // Maximum allowed replacement qty (= refund qty)
        private Product? _selectedProduct;
        private List<Product> _allProducts = new();

        public ReplacementSelectionResult? Result { get; private set; }

        /// <param name="refundProductName">Name of the product being refunded (for display)</param>
        /// <param name="refundQty">Quantity being refunded caps replacement qty</param>
        public ReplacementSelectionDialog(string refundProductName, int refundQty)
        {
            InitializeComponent();
            _maxQty = refundQty;

            // Set context labels
            lblRefundContext.Text = $"Choose a product from stock to give as replacement for the returned item";
            lblRefundProduct.Text = refundProductName;
            lblRefundQty.Text = refundQty.ToString();
            lblMaxReplacement.Text = refundQty.ToString();

            // Load all products initially
            LoadProducts("");

            txtSearch.Focus();
        }

        private void LoadProducts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                _allProducts = _productService.GetAll();
            else
                _allProducts = _productService.Search(query);

            lstProducts.ItemsSource = _allProducts;
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadProducts(txtSearch.Text.Trim());
        }

        private void txtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && lstProducts.Items.Count > 0)
            {
                lstProducts.Focus();
                lstProducts.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && lstProducts.Items.Count > 0)
            {
                SelectProduct((Product)lstProducts.Items[0]);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void lstProducts_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstProducts.SelectedItem is Product p)
            {
                SelectProduct(p);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void lstProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstProducts.SelectedItem is Product p)
            {
                SelectProduct(p);
            }
        }

        private void SelectProduct(Product product)
        {
            if (product.Quantity <= 0)
            {
                lblValidation.Text = $"{product.Name} is out of stock. Please select another product.";
                btnConfirm.IsEnabled = false;
                return;
            }

            _selectedProduct = product;

            lblSelectedProduct.Text = $"{product.Name}  Rs {product.SalePrice:N2}";
            lblSelectedStock.Text = $"Available stock: {product.Quantity} units";

            // Reset qty to 1, bounded by stock and max
            int defaultQty = Math.Min(1, Math.Min(product.Quantity, _maxQty));
            txtQty.Text = defaultQty.ToString();

            panelQtySelection.Visibility = Visibility.Visible;
            lblValidation.Text = "";
            ValidateAndUpdateConfirm();
        }

        private void txtQty_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateAndUpdateConfirm();
        }

        private void IncrementQty_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtQty.Text, out int q))
                txtQty.Text = (q + 1).ToString();
        }

        private void DecrementQty_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtQty.Text, out int q) && q > 1)
                txtQty.Text = (q - 1).ToString();
        }

        private void ValidateAndUpdateConfirm()
        {
            if (_selectedProduct == null)
            {
                btnConfirm.IsEnabled = false;
                return;
            }

            if (!int.TryParse(txtQty.Text, out int qty) || qty <= 0)
            {
                lblValidation.Text = "Please enter a valid quantity (minimum 1).";
                btnConfirm.IsEnabled = false;
                return;
            }

            if (qty > _maxQty)
            {
                lblValidation.Text = $"Replacement qty cannot exceed refund qty ({_maxQty}).";
                btnConfirm.IsEnabled = false;
                return;
            }

            if (qty > _selectedProduct.Quantity)
            {
                lblValidation.Text = $"Insufficient stock. Only {_selectedProduct.Quantity} in stock.";
                btnConfirm.IsEnabled = false;
                return;
            }

            lblValidation.Text = "";
            btnConfirm.IsEnabled = true;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProduct == null) return;
            if (!int.TryParse(txtQty.Text, out int qty) || qty <= 0) return;

            Result = new ReplacementSelectionResult
            {
                ProductId = _selectedProduct.Id,
                ProductName = _selectedProduct.Name,
                UnitPrice = _selectedProduct.SalePrice,
                Quantity = qty,
                StockAvailable = _selectedProduct.Quantity
            };

            DialogResult = true;
            Close();
        }

        private void CloseDialog_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

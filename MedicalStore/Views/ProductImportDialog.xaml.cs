using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using Microsoft.Win32;

namespace MedicalStore.Views
{
    public partial class ProductImportDialog : Window
    {
        private string? _selectedFilePath;

        public bool ImportCompleted { get; private set; } = false;

        public ProductImportDialog()
        {
            InitializeComponent();
        }

        private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = "ProductImportTemplate.xlsx",
                Title = "Save Product Import Template"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ProductImportService.GenerateTemplate(dialog.FileName);
                    MessageBox.Show(
                        $"Template saved successfully!\n\n{dialog.FileName}\n\nOpen it, fill in your products, and come back to upload.",
                        "Template Saved",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save template: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                Title = "Select Product Excel File"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;
                lblSelectedFile.Text = System.IO.Path.GetFileName(_selectedFilePath);
                btnImport.IsEnabled = true;
            }
        }

        private async void StartImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !System.IO.File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Please select a valid Excel file first.", "No File", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Disable UI during import
            btnImport.IsEnabled = false;
            panelProgress.Visibility = Visibility.Visible;
            panelResults.Visibility = Visibility.Collapsed;
            scrollLog.Visibility = Visibility.Collapsed;
            lblProgress.Text = "Processing...";
            progressBar.Value = 0;

            ImportResult? result = null;

            try
            {
                // Run import on background thread
                result = await Task.Run(() =>
                {
                    return ProductImportService.ImportFromExcel(_selectedFilePath, (current, total) =>
                    {
                        // Report progress on UI thread
                        Dispatcher.Invoke(() =>
                        {
                            double pct = total > 0 ? (double)current / total * 100 : 0;
                            progressBar.Value = pct;
                            lblProgressDetail.Text = $"Processing row {current} of {total}...";
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                lblProgress.Text = "Import failed!";
                lblProgressDetail.Text = ex.Message;
                btnImport.IsEnabled = true;
                return;
            }

            // Show results
            progressBar.Value = 100;
            lblProgress.Text = "Import Complete!";
            lblProgressDetail.Text = "";
            panelResults.Visibility = Visibility.Visible;

            lblTotalRows.Text = result.TotalRows.ToString();
            lblSuccess.Text = result.SuccessCount.ToString();
            lblSkipped.Text = result.SkippedCount.ToString();
            lblErrors.Text = result.ErrorCount.ToString();

            // Build log
            var logLines = new List<string>();
            foreach (var warn in result.Warnings)
                logLines.Add($"⚠ {warn}");
            foreach (var err in result.Errors)
                logLines.Add($"❌ {err}");

            if (logLines.Count > 0)
            {
                scrollLog.Visibility = Visibility.Visible;
                lblLog.Text = string.Join("\n", logLines);
            }

            if (result.SuccessCount > 0)
                ImportCompleted = true;

            btnImport.Content = "Import Again";
            btnImport.IsEnabled = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = ImportCompleted;
            Close();
        }
    }
}

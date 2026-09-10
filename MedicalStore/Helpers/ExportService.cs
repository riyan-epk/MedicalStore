using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MedicalStore.Common.Models;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Helpers;
using MedicalStore.Common.Constants;

namespace MedicalStore.Helpers
{
    public class ExportService
    {
        public bool ExportToCsv(IEnumerable<LedgerEntry> data, string title)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Date,Type,Reference,Debit,Credit,Notes");

                    foreach (var item in data)
                    {
                        var notes = item.Notes?.Replace(",", ";").Replace("\n", " ").Replace("\r", " ") ?? "";
                        sb.AppendLine($"{item.Date:yyyy-MM-dd HH:mm},{item.Type},{item.Reference},{item.Debit},{item.Credit},\"{notes}\"");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            return false;
        }

        private void PrintDocument(FlowDocument doc, string documentName)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    doc.ColumnWidth = printDialog.PrintableAreaWidth;
                    IDocumentPaginatorSource idp = doc;
                    printDialog.PrintDocument(idp.DocumentPaginator, documentName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF Generation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument CreateBaseDocument()
        {
            return new FlowDocument
            {
                PagePadding = new Thickness(50),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                PageHeight = 1122,
                PageWidth = 793,
                ColumnWidth = 693
            };
        }

        private void AddHeader(FlowDocument doc, string reportTitle, string reportPeriod)
        {
            string storeName = string.IsNullOrEmpty(AppConstants.StoreName) ? "MEDICAL STORE" : AppConstants.StoreName.ToUpper();
            
            var titlePara = new Paragraph(new Bold(new Run($"{storeName} - {reportTitle.ToUpper()}")))
            {
                FontSize = 22,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243))
            };
            doc.Blocks.Add(titlePara);

            if (!string.IsNullOrEmpty(reportPeriod))
            {
                doc.Blocks.Add(new Paragraph(new Run($"Report Period: {reportPeriod}"))
                {
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4),
                    Foreground = Brushes.DarkSlateGray
                });
            }

            doc.Blocks.Add(new Paragraph(new Run($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm}"))
            {
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = Brushes.Gray
            });
        }

        private void AddFooter(FlowDocument doc)
        {
            doc.Blocks.Add(new Paragraph(new Run($"This is a computer-generated report. | Store: {AppConstants.StoreName}"))
            {
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
                Foreground = Brushes.Gray
            });
            doc.Blocks.Add(new Paragraph(new Run(AppConstants.DeveloperBranding))
            {
                FontSize = 9,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = Brushes.Gray
            });
        }

        private Table CreateTable(params string[] headers)
        {
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0.5, 0, 0.5), Margin = new Thickness(0,0,0,20) };
            var headerGroup = new TableRowGroup();
            var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)), FontWeight = FontWeights.Bold };
            
            foreach (var h in headers)
            {
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h)) { Padding = new Thickness(5) }) { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0,0,0,1) });
            }
            
            headerGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerGroup);
            return table;
        }

        private void AddRow(TableRowGroup group, params (string text, Brush brush, bool rightAlign)[] cells)
        {
            var row = new TableRow();
            foreach (var c in cells)
            {
                var p = new Paragraph(new Run(c.text)) { Padding = new Thickness(5) };
                if (c.brush != null) p.Foreground = c.brush;
                if (c.rightAlign) p.TextAlignment = TextAlignment.Right;
                row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.WhiteSmoke, BorderThickness = new Thickness(0,0,0,1) });
            }
            group.Rows.Add(row);
        }

        // --- Sales Report ---
        public void ExportSalesPdf(IEnumerable<Sale> sales, string period)
        {
            var doc = CreateBaseDocument();
            AddHeader(doc, "Sales Report", period);

            var summaryGrid = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 20) };
            summaryGrid.Columns.Add(new TableColumn { Width = new GridLength(200) });
            summaryGrid.Columns.Add(new TableColumn { Width = new GridLength(200) });
            var sg = new TableRowGroup();
            summaryGrid.RowGroups.Add(sg);
            AddRow(sg, ("Total Sales:", (Brush)null, false), (sales.Sum(s => s.NetAmount).FormatRs(), Brushes.DarkGreen, false));
            AddRow(sg, ("Total Discounts:", (Brush)null, false), (sales.Sum(s => s.Discount).FormatRs(), Brushes.DarkRed, false));
            AddRow(sg, ("Total Profit:", (Brush)null, false), (sales.Sum(s => s.Profit).FormatRs(), Brushes.Blue, false));
            doc.Blocks.Add(summaryGrid);

            var table = CreateTable("Inv No", "Customer", "Date", "Items Sold", "Qty", "SubTotal", "Discount", "Tax", "Net Total");
            table.Columns.Add(new TableColumn { Width = new GridLength(50) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(40) });
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            var dataGroup = new TableRowGroup();
            foreach (var s in sales)
            {
                string itemsStr = string.Join(", ", s.Items.Select(i => i.Product?.Name ?? "Item"));
                string qtyStr = string.Join(", ", s.Items.Select(i => i.TotalUnits));
                AddRow(dataGroup,
                    (s.InvoiceNo, (Brush)null, false),
                    (s.Customer?.Name ?? "Walk-in", (Brush)null, false),
                    (s.Date.ToString("dd/MM/yy"), (Brush)null, false),
                    (itemsStr, (Brush)null, false),
                    (qtyStr, (Brush)null, false),
                    (s.SubTotal.Format(), (Brush)null, true),
                    (s.Discount.Format(), (Brush)null, true),
                    (s.Tax.Format(), (Brush)null, true),
                    (s.NetAmount.FormatRs(), Brushes.DarkGreen, true)
                );
            }
            table.RowGroups.Add(dataGroup);
            doc.Blocks.Add(table);

            AddFooter(doc);
            PrintDocument(doc, "Sales Report");
        }

        // --- Financial Report ---
        public void ExportFinancialPdf(decimal sales, decimal purchases, decimal refunds, decimal discounts, decimal expenses, decimal profit, string period)
        {
            var doc = CreateBaseDocument();
            AddHeader(doc, "Financial Summary Report", period);

            var summaryGrid = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 20) };
            for(int i=0; i<2; i++) summaryGrid.Columns.Add(new TableColumn { Width = new GridLength(200) });
            var sg = new TableRowGroup();
            summaryGrid.RowGroups.Add(sg);

            AddRow(sg, ("Total Revenue (Sales):", (Brush)null, false), (sales.FormatRs(), Brushes.DarkGreen, false));
            AddRow(sg, ("Total Purchases:", (Brush)null, false), (purchases.FormatRs(), Brushes.DarkRed, false));
            AddRow(sg, ("Total Refunds:", (Brush)null, false), (refunds.FormatRs(), Brushes.DarkOrange, false));
            AddRow(sg, ("Total Discounts Given:", (Brush)null, false), (discounts.FormatRs(), Brushes.Blue, false));
            AddRow(sg, ("Total Expenses:", (Brush)null, false), (expenses.FormatRs(), Brushes.DarkRed, false));
            AddRow(sg, ("Net Profit / (Loss):", (Brush)null, false), (profit.FormatRs(), profit >= 0 ? Brushes.DarkGreen : Brushes.Red, false));

            doc.Blocks.Add(summaryGrid);
            AddFooter(doc);
            PrintDocument(doc, "Financial Report");
        }

        // --- Inventory Report ---
        public void ExportInventoryPdf(IEnumerable<Product> products)
        {
            var doc = CreateBaseDocument();
            AddHeader(doc, "Inventory Status Report", "");

            var table = CreateTable("Code", "Product Name", "Category", "Stock Qty", "Purchase Price", "Selling Price", "Stock Value", "Alerts");
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(140) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            var dataGroup = new TableRowGroup();
            foreach (var p in products)
            {
                decimal val = (p.StockUnits > 0 ? p.StockUnits : p.Quantity) * p.PurchasePrice;
                string status = p.Quantity <= p.MinStockLevel ? "Low Stock" : "Normal";
                Brush statusBrush = p.Quantity <= p.MinStockLevel ? Brushes.Red : Brushes.DarkGreen;

                AddRow(dataGroup,
                    (p.Id.ToString(), (Brush)null, false),
                    (p.Name, (Brush)null, false),
                    (p.Category?.Name ?? "", (Brush)null, false),
                    (p.StockDisplay, (Brush)null, false),
                    (p.PurchasePrice.Format(), (Brush)null, true),
                    (p.SalePrice.Format(), (Brush)null, true),
                    (val.Format(), (Brush)null, true),
                    (status, statusBrush, false)
                );
            }
            table.RowGroups.Add(dataGroup);
            doc.Blocks.Add(table);

            AddFooter(doc);
            PrintDocument(doc, "Inventory Report");
        }

        // --- Purchases Report ---
        public void ExportPurchasesPdf(IEnumerable<Purchase> purchases, string period)
        {
            var doc = CreateBaseDocument();
            AddHeader(doc, "Purchases Report", period);

            var summaryGrid = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 20) };
            for(int i=0; i<2; i++) summaryGrid.Columns.Add(new TableColumn { Width = new GridLength(200) });
            var sg = new TableRowGroup();
            summaryGrid.RowGroups.Add(sg);

            AddRow(sg, ("Total Purchases:", (Brush)null, false), (purchases.Sum(p => p.TotalAmount).FormatRs(), Brushes.DarkBlue, false));
            AddRow(sg, ("Total Paid:", (Brush)null, false), (purchases.Sum(p => p.PaidAmount).FormatRs(), Brushes.DarkGreen, false));
            AddRow(sg, ("Outstanding Dues:", (Brush)null, false), (purchases.Sum(p => p.DueAmount).FormatRs(), Brushes.Red, false));
            doc.Blocks.Add(summaryGrid);

            var table = CreateTable("Date", "Invoice", "Supplier Name", "Items Purchased", "Qty", "Cost Price", "Total Amount");
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(120) });
            table.Columns.Add(new TableColumn { Width = new GridLength(160) });
            table.Columns.Add(new TableColumn { Width = new GridLength(50) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            var dataGroup = new TableRowGroup();
            foreach (var p in purchases)
            {
                string itemsDesc = string.Join(", ", p.Items.Select(i => i.Product?.Name ?? "Item"));
                string qtys = string.Join(", ", p.Items.Select(i => i.TotalUnits));
                AddRow(dataGroup,
                    (p.Date.ToString("dd/MM/yy"), (Brush)null, false),
                    (p.Id.ToString(), (Brush)null, false),
                    (p.Supplier?.Name ?? "", (Brush)null, false),
                    (itemsDesc, (Brush)null, false),
                    (qtys, (Brush)null, false),
                    (p.Items.FirstOrDefault()?.UnitPrice.Format() ?? "0", (Brush)null, true),
                    (p.TotalAmount.FormatRs(), (Brush)null, true)
                );
            }
            table.RowGroups.Add(dataGroup);
            doc.Blocks.Add(table);

            AddFooter(doc);
            PrintDocument(doc, "Purchases Report");
        }

        // --- Customer Report ---
        public void ExportCustomerPdf(Customer c, IEnumerable<Sale> sales, IEnumerable<Payment> payments, string period)
        {
            var doc = CreateBaseDocument();
            AddHeader(doc, "Customer Transaction Report", period);

            var summaryGrid = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 20) };
            for(int i=0; i<2; i++) summaryGrid.Columns.Add(new TableColumn { Width = new GridLength(200) });
            var sg = new TableRowGroup();
            summaryGrid.RowGroups.Add(sg);

            AddRow(sg, ("Customer Name:", (Brush)null, false), (c.Name, (Brush)null, false));
            AddRow(sg, ("Phone:", (Brush)null, false), (c.Phone ?? "N/A", (Brush)null, false));
            string status = c.Balance == 0 ? "Fully Paid" : (c.Balance > 0 ? "Pending Dues" : "Advance/Credit");
            Brush statusBrush = c.Balance == 0 ? Brushes.DarkGreen : (c.Balance > 0 ? Brushes.Red : Brushes.Blue);
            
            AddRow(sg, ("Current Status:", (Brush)null, false), (status, statusBrush, false));
            AddRow(sg, ("Current Balance:", (Brush)null, false), (Math.Abs(c.Balance).FormatRs(), statusBrush, false));

            doc.Blocks.Add(summaryGrid);

            var table = CreateTable("Date", "Invoice", "Item Name", "Quantity", "Unit Price", "Discount", "Tax", "Total Price");
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(140) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            var dataGroup = new TableRowGroup();
            foreach (var s in sales)
            {
                foreach (var i in s.Items)
                {
                   AddRow(dataGroup,
                        (s.Date.ToString("dd/MM/yy"), (Brush)null, false),
                        (s.InvoiceNo, (Brush)null, false),
                        (i.Product?.Name ?? "Unknown Item", (Brush)null, false),
                        (i.TotalUnits.ToString(), (Brush)null, false),
                        (i.UnitPrice.Format(), (Brush)null, true),
                        ("-", (Brush)null, true), 
                        ("-", (Brush)null, true),
                        ((i.TotalUnits * i.UnitPrice).Format(), (Brush)null, true)
                   );
                }
                
                var subRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)) };
                subRow.Cells.Add(new TableCell(new Paragraph(new Run($"Invoice {s.InvoiceNo} Summary:")) { FontStyle = FontStyles.Italic, Foreground = Brushes.Gray }) { ColumnSpan = 3, Padding = new Thickness(5) });
                subRow.Cells.Add(new TableCell(new Paragraph(new Run($"- {s.Discount.Format()} Disc")) { FontStyle = FontStyles.Italic, Foreground = Brushes.Gray }) { ColumnSpan=2, TextAlignment = TextAlignment.Right, Padding = new Thickness(5)});
                subRow.Cells.Add(new TableCell(new Paragraph(new Run($"+ {s.Tax.Format()} Tax")) { FontStyle = FontStyles.Italic, Foreground = Brushes.Gray }) { ColumnSpan=2, TextAlignment = TextAlignment.Right, Padding = new Thickness(5)});
                subRow.Cells.Add(new TableCell(new Paragraph(new Run(s.NetAmount.FormatRs())) { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkBlue }) { TextAlignment = TextAlignment.Right, Padding = new Thickness(5) });
                dataGroup.Rows.Add(subRow);
            }
            
            table.RowGroups.Add(dataGroup);
            doc.Blocks.Add(table);

            if (payments.Any())
            {
                doc.Blocks.Add(new Paragraph(new Bold(new Run("PAYMENTS MADE:"))) { FontSize = 14, Margin = new Thickness(0, 20, 0, 10), Foreground = Brushes.DarkBlue });
                
                var pTable = CreateTable("Date", "Amount", "Method", "Notes");
                pTable.Columns.Add(new TableColumn { Width = new GridLength(100) });
                pTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
                pTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
                pTable.Columns.Add(new TableColumn { Width = new GridLength(293) });

                var pGroup = new TableRowGroup();
                foreach (var p in payments)
                {
                    AddRow(pGroup,
                        (p.Date.ToString("dd/MM/yy HH:mm"), (Brush)null, false),
                        (p.Amount.FormatRs(), Brushes.DarkGreen, false),
                        ("Cash", (Brush)null, false),
                        (p.Notes ?? "-", (Brush)null, false)
                    );
                }
                pTable.RowGroups.Add(pGroup);
                doc.Blocks.Add(pTable);
            }

            AddFooter(doc);
            PrintDocument(doc, $"Customer_{c.Name}_Report");
        }

        // --- Supplier Report ---
        public void ExportSupplierPdf(Supplier s, IEnumerable<Purchase> purchases, IEnumerable<SupplierPayment> payments, string period)
        {
            var doc = CreateBaseDocument();
            AddHeader(doc, "Supplier Transaction Report", period);

            var summaryGrid = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 20) };
            for(int i=0; i<2; i++) summaryGrid.Columns.Add(new TableColumn { Width = new GridLength(200) });
            var sg = new TableRowGroup();
            summaryGrid.RowGroups.Add(sg);

            AddRow(sg, ("Supplier Name:", (Brush)null, false), (s.Name, (Brush)null, false));
            AddRow(sg, ("Contact:", (Brush)null, false), (s.Phone ?? "N/A", (Brush)null, false));
            AddRow(sg, ("Outstanding Dues:", (Brush)null, false), (s.Balance > 0 ? s.Balance.FormatRs() : "0", Brushes.Red, false));
            AddRow(sg, ("Advance Paid:", (Brush)null, false), (s.Balance < 0 ? Math.Abs(s.Balance).FormatRs() : "0", Brushes.DarkGreen, false));

            doc.Blocks.Add(summaryGrid);

            var table = CreateTable("Date", "Invoice", "Supplied Products", "Amount Paid", "Due Amount", "Total Purchase");
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(220) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });

            var dataGroup = new TableRowGroup();
            foreach (var p in purchases)
            {
                string itemsDesc = string.Join(", ", p.Items.Select(i => $"{i.Product?.Name ?? "Item"} x{i.TotalUnits}"));
                AddRow(dataGroup,
                    (p.Date.ToString("dd/MM/yy"), (Brush)null, false),
                    (p.Id.ToString(), (Brush)null, false),
                    (itemsDesc, (Brush)null, false),
                    (p.PaidAmount.FormatRs(), Brushes.DarkGreen, true),
                    (p.DueAmount.FormatRs(), Brushes.Red, true),
                    (p.TotalAmount.FormatRs(), (Brush)null, true)
                );
            }
            table.RowGroups.Add(dataGroup);
            doc.Blocks.Add(table);

            if (payments.Any())
            {
                doc.Blocks.Add(new Paragraph(new Bold(new Run("PAYMENTS TO SUPPLIER:"))) { FontSize = 14, Margin = new Thickness(0, 20, 0, 10), Foreground = Brushes.DarkBlue });
                
                var pTable = CreateTable("Date", "Amount", "Method", "Notes");
                pTable.Columns.Add(new TableColumn { Width = new GridLength(100) });
                pTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
                pTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
                pTable.Columns.Add(new TableColumn { Width = new GridLength(293) });

                var pGroup = new TableRowGroup();
                foreach (var p in payments)
                {
                    AddRow(pGroup,
                        (p.Date.ToString("dd/MM/yy HH:mm"), (Brush)null, false),
                        (p.Amount.FormatRs(), Brushes.DarkGreen, false),
                        (p.PaymentMethod ?? "Cash", (Brush)null, false),
                        (p.Notes ?? "-", (Brush)null, false)
                    );
                }
                pTable.RowGroups.Add(pGroup);
                doc.Blocks.Add(pTable);
            }

            AddFooter(doc);
            PrintDocument(doc, $"Supplier_{s.Name}_Report");
        }

        // --- Returns Report ---
        public void ExportReturnsPdf(IEnumerable<Return> returns, string period)
        {
            var doc = CreateBaseDocument();
            AddHeader(doc, "Returns Report", period);

            var summaryGrid = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 20) };
            for(int i=0; i<2; i++) summaryGrid.Columns.Add(new TableColumn { Width = new GridLength(200) });
            var sg = new TableRowGroup();
            summaryGrid.RowGroups.Add(sg);

            AddRow(sg, ("Total Returned Value:", (Brush)null, false), (returns.Sum(r => r.TotalAmount).FormatRs(), Brushes.DarkBlue, false));
            AddRow(sg, ("Total Refunded:", (Brush)null, false), (returns.Sum(r => r.RefundAmount).FormatRs(), Brushes.DarkRed, false));
            AddRow(sg, ("Total Replaced:", (Brush)null, false), (returns.Sum(r => r.ReplaceAmount).FormatRs(), Brushes.Blue, false));
            doc.Blocks.Add(summaryGrid);

            var table = CreateTable("Date", "Customer", "Inv No", "Returned Products", "Reason", "Return Rs", "Refund Rs");
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(160) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            var dataGroup = new TableRowGroup();
            foreach (var r in returns)
            {
                string itemsDesc = string.Join(", ", r.Items.Select(i => $"{i.Product?.Name ?? "Item"} x{i.Quantity}"));
                AddRow(dataGroup,
                    (r.Date.ToString("dd/MM/yy"), (Brush)null, false),
                    (r.Customer?.Name ?? "Walk-in", (Brush)null, false),
                    (r.Sale?.InvoiceNo ?? "", (Brush)null, false),
                    (itemsDesc, (Brush)null, false),
                    (r.Notes ?? r.ReturnType ?? "-", (Brush)null, false),
                    (r.TotalAmount.Format(), (Brush)null, true),
                    (r.RefundAmount.Format(), Brushes.DarkRed, true)
                );
            }
            table.RowGroups.Add(dataGroup);
            doc.Blocks.Add(table);

            AddFooter(doc);
            PrintDocument(doc, "Returns Report");
        }
    }
}

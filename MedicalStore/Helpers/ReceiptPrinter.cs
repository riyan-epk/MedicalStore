using MedicalStore.Common.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Constants;

namespace MedicalStore.Helpers
{
    public class ReceiptPrinter
    {
        public void Print(Sale sale)
        {
            try
            {
                var doc = CreateReceipt(sale);
                PrintDoc(doc, $"Receipt-{sale.InvoiceNo}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print failed: {ex.Message}", "Print Error");
            }
        }

        public void Print(Return ret)
        {
            try
            {
                var doc = CreateReturnReceipt(ret);
                PrintDoc(doc, $"Return-{ret.Id}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print failed: {ex.Message}", "Print Error");
            }
        }

        private void PrintDoc(FlowDocument doc, string title)
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() == true)
            {
                doc.PageHeight = dialog.PrintableAreaHeight;
                doc.PageWidth = dialog.PrintableAreaWidth;
                doc.PagePadding = new Thickness(40);
                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                dialog.PrintDocument(paginator, title);
            }
        }

        private FlowDocument CreateReceipt(Sale sale)
        {
            var doc = SetupDoc();

            // Store Header
            doc.Blocks.Add(CreateHeader(sale.InvoiceNo, sale.Date, sale.Customer?.Name, sale.User?.FullName));

            // Items Table
            doc.Blocks.Add(CreateItemsTable(sale.Items.Select(i => (i.Product?.Name ?? "", i.Quantity, i.UnitPrice, i.Total)).ToList()));

            // Totals
            var totals = new Paragraph { Margin = new Thickness(0, 8, 0, 0), FontSize = 11 };
            totals.Inlines.Add(new Run(new string('-', 45) + "\n") { FontSize = 10 });
            totals.Inlines.Add(new Run($"{"Sub Total:",-25} Rs {sale.SubTotal,12:N2}\n"));
            if (sale.Discount > 0)
                totals.Inlines.Add(new Run($"{"Discount:",-25} Rs {sale.Discount,12:N2}\n"));
            
            decimal netBeforeTax = sale.SubTotal - sale.Discount;
            decimal taxAmount = sale.NetAmount - netBeforeTax;
            if (taxAmount > 0.01m)
                totals.Inlines.Add(new Run($"{"Tax:",-25} Rs {taxAmount,12:N2}\n"));

            totals.Inlines.Add(new Bold(new Run($"{"Net Amount:",-25} Rs {sale.NetAmount,12:N2}\n") { FontSize = 13 }));

            // Add Previous Balance and Total Outstanding
            if (sale.Customer != null && !sale.Customer.IsWalkIn)
            {
                decimal currentDue = sale.NetAmount - sale.PaidAmount;
                decimal prevBalance = sale.Customer.Balance - currentDue;
                
                if (Math.Abs(prevBalance) > 0.01m)
                {
                    string prevLabel = prevBalance >= 0 ? "Previous Dues:" : "Previous Advance:";
                    totals.Inlines.Add(new Run($"{prevLabel,-25} Rs {Math.Abs(prevBalance),12:N2}\n"));
                    
                    decimal totalOutstanding = prevBalance + sale.NetAmount;
                    totals.Inlines.Add(new Bold(new Run($"{"Total Outstanding:",-25} Rs {totalOutstanding,12:N2}\n")));
                }
            }

            totals.Inlines.Add(new Run($"{"Paid Amount:",-25} Rs {sale.PaidAmount,12:N2}\n"));
            if (sale.ChangeAmount > 0)
                totals.Inlines.Add(new Run($"{"Change:",-25} Rs {sale.ChangeAmount,12:N2}\n"));
            
            if (sale.Customer != null && !sale.Customer.IsWalkIn)
            {
                totals.Inlines.Add(new Run(new string('-', 45) + "\n"));
                string balLabel = sale.Customer.Balance >= 0 ? "Final Balance Due:" : "Final Advance Balance:";
                var balColor = sale.Customer.Balance >= 0 ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;
                totals.Inlines.Add(new Bold(new Run($"{balLabel,-25} Rs {Math.Abs(sale.Customer.Balance),12:N2}\n") { Foreground = balColor }));
            }
            doc.Blocks.Add(totals);

            doc.Blocks.Add(CreateFooter());
            return doc;
        }

        private FlowDocument CreateReturnReceipt(Return ret)
        {
            var doc = SetupDoc();
            doc.Blocks.Add(CreateHeader($"RET-{ret.Id}", ret.Date, ret.Customer?.Name, "ADMIN"));

            var titlePar = new Paragraph(new Bold(new Run($"*** {ret.ReturnType.ToUpper()} RECEIPT ***"))) { TextAlignment = TextAlignment.Center, FontSize = 14 };
            doc.Blocks.Add(titlePar);

            doc.Blocks.Add(new Paragraph(new Bold(new Run("Returned Items:"))) { Margin = new Thickness(0, 8, 0, 4) });
            doc.Blocks.Add(CreateItemsTable(ret.Items.Select(i => (i.Product?.Name ?? "", i.Quantity, i.UnitPrice, i.Total)).ToList()));

            if (ret.ReplacementItems.Any())
            {
                doc.Blocks.Add(new Paragraph(new Bold(new Run("Replacement Items:"))) { Margin = new Thickness(0, 12, 0, 4) });
                doc.Blocks.Add(CreateItemsTable(ret.ReplacementItems.Select(i => (i.Product?.Name ?? "", i.Quantity, i.UnitPrice, i.Total)).ToList()));
            }

            var totals = new Paragraph { Margin = new Thickness(0, 8, 0, 0), FontSize = 11 };
            totals.Inlines.Add(new Run(new string('-', 45) + "\n") { FontSize = 10 });
            
            totals.Inlines.Add(new Run($"{"Returned Total:",-25} Rs {ret.TotalAmount,12:N2}\n"));
            if (ret.ReplaceAmount > 0)
                totals.Inlines.Add(new Run($"{"Replacement Total:",-25} Rs {ret.ReplaceAmount,12:N2}\n"));
            
            decimal netDiff = ret.ReplaceAmount - ret.TotalAmount;
            if (netDiff > 0)
                totals.Inlines.Add(new Bold(new Run($"{"Customer Paid Extra:",-25} Rs {netDiff,12:N2}\n") { FontSize = 13, Foreground = System.Windows.Media.Brushes.DarkRed }));
            else if (netDiff < 0)
                totals.Inlines.Add(new Bold(new Run($"{"Refunded to Customer:",-25} Rs {ret.RefundAmount,12:N2}\n") { FontSize = 13, Foreground = System.Windows.Media.Brushes.Green }));
            else
                totals.Inlines.Add(new Bold(new Run($"{"Even Exchange:",-25} Rs 0.00\n") { FontSize = 13 }));

            if (ret.Customer != null && !ret.Customer.IsWalkIn)
            {
                totals.Inlines.Add(new Run(new string('-', 45) + "\n"));
                string balLabel = ret.Customer.Balance >= 0 ? "Total Pending Amount:" : "Total Advance Credit:";
                var balColor = ret.Customer.Balance >= 0 ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;
                totals.Inlines.Add(new Bold(new Run($"{balLabel,-25} Rs {Math.Abs(ret.Customer.Balance),12:N2}\n") { Foreground = balColor, FontSize = 13 }));
            }

            if (!string.IsNullOrEmpty(ret.Notes))
            {
                totals.Inlines.Add(new Run(new string('-', 45) + "\n") { FontSize = 10 });
                totals.Inlines.Add(new Run($"Note: {ret.Notes}\n") { FontStyle = FontStyles.Italic, FontSize = 10 });
            }
            doc.Blocks.Add(totals);

            doc.Blocks.Add(CreateFooter(true));
            return doc;
        }

        private FlowDocument SetupDoc() => new FlowDocument { FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12 };

        private Paragraph CreateHeader(string invoice, DateTime date, string? customer, string? cashier)
        {
            var header = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            header.Inlines.Add(new Bold(new Run(AppConstants.StoreName + "\n") { FontSize = 18 }));
            if (!string.IsNullOrEmpty(AppConstants.StorePhone))
                header.Inlines.Add(new Run($"Phone: {AppConstants.StorePhone}\n") { FontSize = 10 });
            if (!string.IsNullOrEmpty(AppConstants.StoreAddress))
                header.Inlines.Add(new Run(AppConstants.StoreAddress + "\n") { FontSize = 10 }); // Kept this line from original
            header.Inlines.Add(new Run(new string('-', 45) + "\n") { FontSize = 10 });
            header.Inlines.Add(new Run($"No: {invoice} | Date: {date:dd/MM/yy HH:mm}\n"));
            header.Inlines.Add(new Run($"Customer: {customer ?? "Walk-in"}\n"));
            header.Inlines.Add(new Run($"Cashier: {cashier ?? ""}\n")); // Kept this line from original
            header.Inlines.Add(new Run(new string('-', 45)));
            return header;
        }

        private Table CreateItemsTable(System.Collections.Generic.List<(string Name, int Qty, decimal Price, decimal Total)> items)
        {
            var table = new Table { FontSize = 11, CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Kept this column from original
            table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });
            var group = new TableRowGroup();
            var hRow = new TableRow();
            hRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Item")))));
            hRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Qty")))) { TextAlignment = TextAlignment.Center });
            hRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Price")))) { TextAlignment = TextAlignment.Right }); // Kept this cell from original
            hRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Total")))) { TextAlignment = TextAlignment.Right });
            group.Rows.Add(hRow);

            foreach (var item in items)
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Name))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Qty.ToString()))) { TextAlignment = TextAlignment.Center });
                row.Cells.Add(new TableCell(new Paragraph(new Run($"{(item.Price).Format()}"))) { TextAlignment = TextAlignment.Right }); // Kept this cell from original
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Total.Format()))) { TextAlignment = TextAlignment.Right });
                group.Rows.Add(row);
            }
            table.RowGroups.Add(group);
            return table;
        }

        private Paragraph CreateFooter(bool isReturn = false)
        {
            var p = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 12, 0, 0), FontSize = 9 };
            p.Inlines.Add(new Run(new string('-', 45) + "\n"));
            if (isReturn)
            {
                p.Inlines.Add(new Run("RETURN/REPLACEMENT POLICY:\n"));
                p.Inlines.Add(new Run("1. Items must be returned within 3 days.\n"));
                p.Inlines.Add(new Run("2. Original receipt is mandatory.\n"));
                p.Inlines.Add(new Run("3. Opened or damaged items are not eligible.\n"));
            }
            else
            {
                p.Inlines.Add(new Run(AppConstants.ReceiptPolicyNote + "\n"));
            }
            p.Inlines.Add(new Run("\nThank you for choosing " + AppConstants.StoreName + "!"));
            return p;
        }
    }
}

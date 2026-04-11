using ClosedXML.Excel;
using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class ImportResult
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ProductImportService
    {
        /// <summary>
        /// Generates a sample Excel template matching all ProductDialog fields + pack/unit fields.
        /// </summary>
        public static void GenerateTemplate(string filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Products");

            // Headers – same field order as the ProductDialog UI
            var headers = new[]
            {
                "Name *",               // Col A
                "Category",             // Col B
                "Company",              // Col C
                "Supplier",             // Col D
                "BatchNo",              // Col E
                "Barcode",              // Col F
                "PurchasePrice",        // Col G
                "SalePrice *",          // Col H
                "No. of Packs",         // Col I  (pack-unit section)
                "Units Per Pack",       // Col J  (pack-unit section)
                "Pack Price",           // Col K  (pack-unit section)
                "Quantity (Total Units) *", // Col L  – auto from I × J when blank
                "MinStockLevel",        // Col M
                "ManufDate (dd/MM/yyyy)", // Col N
                "ExpiryDate (dd/MM/yyyy)" // Col O
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A73E8");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            // Example row 1 – pack-based product
            ws.Cell(2, 1).Value = "Panadol Extra 500mg";
            ws.Cell(2, 2).Value = "Tablet";
            ws.Cell(2, 3).Value = "GSK";
            ws.Cell(2, 4).Value = "Default Supplier";
            ws.Cell(2, 5).Value = "B-2024-001";
            ws.Cell(2, 6).Value = "8901234567890";
            ws.Cell(2, 7).Value = 80;
            ws.Cell(2, 8).Value = 120;
            ws.Cell(2, 9).Value = 10;     // 10 packs
            ws.Cell(2, 10).Value = 10;    // 10 units per pack
            ws.Cell(2, 11).Value = 1200;  // Pack price
            ws.Cell(2, 12).Value = "";    // Calculated automatically: 10×10 = 100
            ws.Cell(2, 13).Value = 20;
            ws.Cell(2, 14).Value = "01/01/2024";
            ws.Cell(2, 15).Value = "01/01/2026";

            // Example row 2 – single-unit product
            ws.Cell(3, 1).Value = "Amoxicillin 250mg Syrup";
            ws.Cell(3, 2).Value = "Syrup";
            ws.Cell(3, 3).Value = "Pfizer";
            ws.Cell(3, 4).Value = "";
            ws.Cell(3, 5).Value = "B-2024-002";
            ws.Cell(3, 6).Value = "";
            ws.Cell(3, 7).Value = 50;
            ws.Cell(3, 8).Value = 85;
            ws.Cell(3, 9).Value = "";
            ws.Cell(3, 10).Value = 1;
            ws.Cell(3, 11).Value = "";
            ws.Cell(3, 12).Value = 200;
            ws.Cell(3, 13).Value = 30;
            ws.Cell(3, 14).Value = "15/03/2024";
            ws.Cell(3, 15).Value = "15/03/2026";

            // Instructions sheet
            var instrWs = workbook.AddWorksheet("Instructions");
            instrWs.Cell(1, 1).Value = "Product Bulk Import - Instructions";
            instrWs.Cell(1, 1).Style.Font.Bold = true;
            instrWs.Cell(1, 1).Style.Font.FontSize = 16;
            instrWs.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1A237E");

            instrWs.Cell(3, 1).Value = "REQUIRED FIELDS (marked with *):";
            instrWs.Cell(3, 1).Style.Font.Bold = true;
            instrWs.Cell(3, 1).Style.Font.FontColor = XLColor.Red;
            instrWs.Cell(4, 1).Value = "  • Name *       – Product name (must be unique, max 200 characters)";
            instrWs.Cell(5, 1).Value = "  • SalePrice *  – Sale price per unit (must be > 0)";
            instrWs.Cell(6, 1).Value = "  • Quantity *   – Total stock in smallest units (must be >= 0)";
            instrWs.Cell(7, 1).Value = "                   If left blank and No. of Packs × Units Per Pack > 0, it will be auto-calculated.";

            instrWs.Cell(9, 1).Value = "OPTIONAL FIELDS:";
            instrWs.Cell(9, 1).Style.Font.Bold = true;
            instrWs.Cell(10, 1).Value = "  • Category      – If category doesn't exist, it will be created automatically";
            instrWs.Cell(11, 1).Value = "  • Company       – Manufacturer / brand name";
            instrWs.Cell(12, 1).Value = "  • Supplier      – Supplier name (if not found, Default Supplier is used)";
            instrWs.Cell(13, 1).Value = "  • BatchNo       – Batch number";
            instrWs.Cell(14, 1).Value = "  • Barcode       – Product barcode";
            instrWs.Cell(15, 1).Value = "  • PurchasePrice – Cost / buy price";

            instrWs.Cell(17, 1).Value = "PACK / UNIT SETTINGS (same as Product Dialog):";
            instrWs.Cell(17, 1).Style.Font.Bold = true;
            instrWs.Cell(17, 1).Style.Font.FontColor = XLColor.FromHtml("#1A73E8");
            instrWs.Cell(18, 1).Value = "  • No. of Packs    – Number of packs being received / stocked";
            instrWs.Cell(19, 1).Value = "  • Units Per Pack   – How many smallest units fit in one pack (default: 1)";
            instrWs.Cell(20, 1).Value = "  • Pack Price       – Price for one full pack (auto-derives Unit Price = Pack Price ÷ Units Per Pack)";
            instrWs.Cell(21, 1).Value = "  Note: If Quantity is left blank, it is calculated as No. of Packs × Units Per Pack.";
            instrWs.Cell(22, 1).Value = "  Note: If Pack Price is left blank, it defaults to SalePrice × Units Per Pack.";

            instrWs.Cell(24, 1).Value = "OTHER FIELDS:";
            instrWs.Cell(24, 1).Style.Font.Bold = true;
            instrWs.Cell(25, 1).Value = "  • MinStockLevel    – Low stock alert threshold (default: 10)";
            instrWs.Cell(26, 1).Value = "  • ManufDate        – Manufacturing date (dd/MM/yyyy)";
            instrWs.Cell(27, 1).Value = "  • ExpiryDate       – Expiry date (dd/MM/yyyy)";

            instrWs.Cell(29, 1).Value = "DUPLICATE HANDLING:";
            instrWs.Cell(29, 1).Style.Font.Bold = true;
            instrWs.Cell(29, 1).Style.Font.FontColor = XLColor.FromHtml("#E65100");
            instrWs.Cell(30, 1).Value = "  • Products with the same name as existing products in the database will be SKIPPED.";
            instrWs.Cell(31, 1).Value = "  • Duplicate names within the Excel file: first occurrence wins, subsequent ones are skipped.";

            instrWs.Column(1).Width = 80;

            // Auto-fit products sheet columns
            ws.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        }

        /// <summary>
        /// Imports products from an Excel file. Validates, creates categories/suppliers as needed,
        /// batch-inserts products. Reports progress via callback.
        /// </summary>
        public static ImportResult ImportFromExcel(string filePath, Action<int, int>? progressCallback = null)
        {
            var result = new ImportResult();

            using var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                result.Errors.Add("No worksheet found in the Excel file.");
                return result;
            }

            // Build header-column map
            var headerRow = ws.Row(1);
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (int c = 1; c <= lastCol; c++)
            {
                string headerVal = headerRow.Cell(c).GetString().Trim()
                    .Replace("*", "").Trim()
                    .Replace(" (dd/MM/yyyy)", "").Trim()
                    .Replace(" (Total Units)", "").Trim();
                if (!string.IsNullOrEmpty(headerVal) && !colMap.ContainsKey(headerVal))
                    colMap[headerVal] = c;
            }

            // Also handle "No. of Packs" and alternate names
            AddAlias(colMap, "No. of Packs", "NoOfPacks");
            AddAlias(colMap, "Units Per Pack", "UnitsPerPack");
            AddAlias(colMap, "Pack Price", "PackPrice");

            // Validate required headers
            if (!colMap.ContainsKey("Name"))
            {
                result.Errors.Add("Missing required column: 'Name'. Please use the template.");
                return result;
            }

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            result.TotalRows = lastRow - 1;

            if (result.TotalRows <= 0)
            {
                result.Errors.Add("No data rows found in the Excel file.");
                return result;
            }

            // Pre-load existing data
            using var db = new AppDbContext();
            var existingProducts = db.Products
                .Select(p => p.Name.ToLower())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var categories = db.Categories
                .ToDictionary(c => c.Name.ToLower(), c => c.Id, StringComparer.OrdinalIgnoreCase);
            var suppliers = db.Suppliers
                .ToDictionary(s => s.Name.ToLower(), s => s.Id, StringComparer.OrdinalIgnoreCase);

            var productsToInsert = new List<Product>();
            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int row = 2; row <= lastRow; row++)
            {
                try
                {
                    string name = GetCellString(ws, row, colMap, "Name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        result.Warnings.Add($"Row {row}: Skipped (empty product name).");
                        result.SkippedCount++;
                        continue;
                    }

                    // Duplicate check
                    if (existingProducts.Contains(name.ToLower()) || processedNames.Contains(name.ToLower()))
                    {
                        result.Warnings.Add($"Row {row}: Skipped duplicate '{name}'.");
                        result.SkippedCount++;
                        continue;
                    }

                    // Parse core fields
                    decimal salePrice = GetCellDecimal(ws, row, colMap, "SalePrice");
                    if (salePrice <= 0)
                    {
                        result.Errors.Add($"Row {row}: '{name}' has invalid SalePrice ({salePrice}). Skipped.");
                        result.ErrorCount++;
                        continue;
                    }

                    // Pack / unit fields (mirror ProductDialog logic)
                    int noOfPacks = GetCellInt(ws, row, colMap, "No. of Packs");
                    int unitsPerPack = GetCellInt(ws, row, colMap, "Units Per Pack");
                    if (unitsPerPack <= 0) unitsPerPack = 1;

                    decimal packPrice = GetCellDecimal(ws, row, colMap, "Pack Price");

                    // Quantity: if explicitly set, use it; otherwise calculate from packs
                    int quantity = GetCellInt(ws, row, colMap, "Quantity");
                    if (quantity <= 0 && noOfPacks > 0 && unitsPerPack > 0)
                    {
                        quantity = noOfPacks * unitsPerPack;
                    }

                    // Pack price: if not set, derive from sale price
                    if (packPrice <= 0)
                        packPrice = salePrice * unitsPerPack;

                    // Unit price: auto-derive (mirrors ProductDialog's RefreshUnitPriceDisplay)
                    decimal unitPrice = unitsPerPack > 0 ? packPrice / unitsPerPack : salePrice;

                    // Purchase price
                    decimal purchasePrice = GetCellDecimal(ws, row, colMap, "PurchasePrice");
                    // If PurchasePrice blank and pack price exists, use UnitPrice as purchase price (like dialog)
                    if (purchasePrice <= 0 && packPrice > 0 && unitsPerPack > 0)
                        purchasePrice = packPrice / unitsPerPack;

                    // Handle category
                    string categoryName = GetCellString(ws, row, colMap, "Category");
                    int categoryId = 9; // Default: Others
                    if (!string.IsNullOrWhiteSpace(categoryName))
                    {
                        if (categories.TryGetValue(categoryName.ToLower(), out int catId))
                        {
                            categoryId = catId;
                        }
                        else
                        {
                            var newCat = new Category { Name = categoryName };
                            db.Categories.Add(newCat);
                            db.SaveChanges();
                            categories[categoryName.ToLower()] = newCat.Id;
                            categoryId = newCat.Id;
                            result.Warnings.Add($"Row {row}: Created new category '{categoryName}'.");
                        }
                    }

                    // Handle supplier
                    string supplierName = GetCellString(ws, row, colMap, "Supplier");
                    int supplierId = 1; // Default Supplier
                    if (!string.IsNullOrWhiteSpace(supplierName))
                    {
                        if (suppliers.TryGetValue(supplierName.ToLower(), out int supId))
                        {
                            supplierId = supId;
                        }
                        else
                        {
                            var newSup = new Supplier { Name = supplierName, IsActive = true };
                            db.Suppliers.Add(newSup);
                            db.SaveChanges();
                            suppliers[supplierName.ToLower()] = newSup.Id;
                            supplierId = newSup.Id;
                            result.Warnings.Add($"Row {row}: Created new supplier '{supplierName}'.");
                        }
                    }

                    int minStock = GetCellInt(ws, row, colMap, "MinStockLevel");
                    if (minStock <= 0) minStock = 10;

                    var product = new Product
                    {
                        Name = name.Trim(),
                        CategoryId = categoryId,
                        SupplierId = supplierId,
                        Company = GetCellString(ws, row, colMap, "Company"),
                        BatchNo = GetCellString(ws, row, colMap, "BatchNo"),
                        Barcode = GetCellString(ws, row, colMap, "Barcode"),
                        PurchasePrice = purchasePrice,
                        SalePrice = salePrice,
                        Quantity = quantity,
                        UnitsPerPack = unitsPerPack,
                        PackPrice = packPrice,
                        UnitPrice = unitPrice,
                        StockUnits = quantity,
                        MinStockLevel = minStock,
                        ManufDate = GetCellDate(ws, row, colMap, "ManufDate"),
                        ExpiryDate = GetCellDate(ws, row, colMap, "ExpiryDate"),
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    productsToInsert.Add(product);
                    processedNames.Add(name.ToLower());

                    // Report progress
                    progressCallback?.Invoke(row - 1, result.TotalRows);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Row {row}: Unexpected error - {ex.Message}");
                    result.ErrorCount++;
                }
            }

            // Batch insert for performance
            if (productsToInsert.Count > 0)
            {
                int batchSize = 100;
                for (int i = 0; i < productsToInsert.Count; i += batchSize)
                {
                    var batch = productsToInsert.Skip(i).Take(batchSize).ToList();
                    db.Products.AddRange(batch);
                    db.SaveChanges();

                    progressCallback?.Invoke(
                        Math.Min(i + batchSize, productsToInsert.Count) + result.SkippedCount + result.ErrorCount,
                        result.TotalRows);
                }
                result.SuccessCount = productsToInsert.Count;
            }

            return result;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static void AddAlias(Dictionary<string, int> colMap, string primary, string alias)
        {
            if (colMap.ContainsKey(primary) && !colMap.ContainsKey(alias))
                colMap[alias] = colMap[primary];
        }

        private static string GetCellString(IXLWorksheet ws, int row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.TryGetValue(key, out int col)) return "";
            return ws.Cell(row, col).GetString().Trim();
        }

        private static decimal GetCellDecimal(IXLWorksheet ws, int row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.TryGetValue(key, out int col)) return 0;
            var cellValue = ws.Cell(row, col).GetString().Trim();
            if (string.IsNullOrEmpty(cellValue)) return 0;
            if (decimal.TryParse(cellValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal val))
                return val;
            return 0;
        }

        private static int GetCellInt(IXLWorksheet ws, int row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.TryGetValue(key, out int col)) return 0;
            var cellValue = ws.Cell(row, col).GetString().Trim();
            if (string.IsNullOrEmpty(cellValue)) return 0;
            if (int.TryParse(cellValue, out int val)) return val;
            if (decimal.TryParse(cellValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal dVal))
                return (int)dVal;
            return 0;
        }

        private static DateTime? GetCellDate(IXLWorksheet ws, int row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.TryGetValue(key, out int col)) return null;
            var cell = ws.Cell(row, col);

            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime();

            string dateStr = cell.GetString().Trim();
            if (string.IsNullOrEmpty(dateStr)) return null;

            string[] formats = { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "d/M/yyyy", "dd-MM-yyyy" };
            if (DateTime.TryParseExact(dateStr, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime dt))
                return dt;

            if (DateTime.TryParse(dateStr, out DateTime dt2))
                return dt2;

            return null;
        }
    }
}

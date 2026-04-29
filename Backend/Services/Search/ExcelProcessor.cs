// using System.Text;
// using DocumentFormat.OpenXml.Packaging;
// using DocumentFormat.OpenXml.Spreadsheet;

// namespace Backend.Services.Search
// {
//     /// <summary>
//     /// Specialized service for processing Excel spreadsheets with enhanced data analysis capabilities.
//     /// This handles the complex logic of extracting meaningful information from Excel files.
//     /// </summary>
//     public class ExcelProcessor
//     {
//         /// <summary>
//         /// Extracts comprehensive content from Excel documents with enhanced analysis
//         /// </summary>
//         /// <param name="doc">The Excel document to process</param>
//         /// <returns>Formatted text representation of the Excel content</returns>
//         public string ExtractExcelContent(SpreadsheetDocument doc)
//         {
//             var sb = new StringBuilder();
//             var workbookPart = doc.WorkbookPart;
//             var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

//             // Get worksheet names for better context
//             var sheets = workbookPart.Workbook.Descendants<Sheet>().ToList();

//             sb.AppendLine("=== EXCEL DOCUMENT ANALYSIS ===");
//             sb.AppendLine($"Total Sheets: {sheets.Count}");
//             sb.AppendLine();

//             int sheetIndex = 0;
//             foreach (var worksheetPart in workbookPart.WorksheetParts)
//             {
//                 var worksheet = worksheetPart.Worksheet;
//                 var sheetData = worksheet.GetFirstChild<SheetData>();

//                 if (sheetData == null) continue;

//                 // Add sheet name for context
//                 var sheetName = sheetIndex < sheets.Count ? sheets[sheetIndex].Name?.Value : $"Sheet{sheetIndex + 1}";

//                 // Detect if this is a summary/aggregate sheet
//                 var isSummarySheet = IsSummarySheet(sheetName, sheetData, sharedStringTable);

//                 if (isSummarySheet)
//                 {
//                     // Completely skip summary sheets to prevent AI confusion
//                     sb.AppendLine($"=== SHEET: {sheetName} (SKIPPED - SUMMARY/AGGREGATE SHEET) ===");
//                     sb.AppendLine("This sheet contains organizational/team summary data and has been excluded from analysis to prevent confusion between individual and organizational statistics.");
//                     sb.AppendLine();
//                     sheetIndex++;
//                     continue;
//                 }

//                 var sheetType = "INDIVIDUAL DATA SHEET";
//                 sb.AppendLine($"=== SHEET: {sheetName} ({sheetType}) ===");

//                 var rows = sheetData.Descendants<Row>().ToList();
//                 if (!rows.Any()) continue;

//                 // Extract headers (first row) and data rows separately for better structure
//                 var headerRow = rows.FirstOrDefault();
//                 var dataRows = rows.Skip(1).ToList();

//                 // Extract column headers with data types analysis
//                 var headers = new List<string>();
//                 var columnDataTypes = new List<string>();

//                 if (headerRow != null)
//                 {
//                     foreach (var cell in headerRow.Elements<Cell>())
//                     {
//                         var cellValue = GetCellValue(cell, sharedStringTable);
//                         headers.Add(cellValue ?? "");
//                     }

//                     // Analyze data types in each column by sampling first few rows
//                     for (int colIndex = 0; colIndex < headers.Count; colIndex++)
//                     {
//                         var sampleValues = dataRows.Take(5).Select(row =>
//                         {
//                             var cells = row.Elements<Cell>().ToList();
//                             return colIndex < cells.Count ? GetCellValue(cells[colIndex], sharedStringTable) : "";
//                         }).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

//                         var dataType = InferDataType(sampleValues);
//                         columnDataTypes.Add(dataType);
//                     }

//                     // Add comprehensive headers with data type info
//                     sb.AppendLine($"COLUMNS ({headers.Count} total):");
//                     for (int i = 0; i < headers.Count; i++)
//                     {
//                         if (!string.IsNullOrWhiteSpace(headers[i]))
//                         {
//                             var dataType = i < columnDataTypes.Count ? columnDataTypes[i] : "Unknown";
//                             sb.AppendLine($"  {i + 1}. {headers[i]} ({dataType})");
//                         }
//                     }
//                     sb.AppendLine($"TOTAL ROWS: {dataRows.Count}");
//                     sb.AppendLine();

//                     // Add summary statistics
//                     sb.AppendLine("DATA SUMMARY:");
//                     CreateDataSummary(sb, headers, dataRows, sharedStringTable);
//                     sb.AppendLine();
//                 }

//                 // Process data rows with enhanced context and relationships
//                 sb.AppendLine("DETAILED DATA:");
//                 int rowNumber = 1;
//                 foreach (var row in dataRows)
//                 {
//                     var cells = row.Elements<Cell>().ToList();
//                     var rowData = new List<string>();

//                     // Map cells to their column positions
//                     for (int i = 0; i < Math.Max(cells.Count, headers.Count); i++)
//                     {
//                         string cellValue = "";
//                         if (i < cells.Count)
//                         {
//                             cellValue = GetCellValue(cells[i], sharedStringTable) ?? "";
//                         }
//                         rowData.Add(cellValue);
//                     }

//                     // Create comprehensive row representation with row numbers
//                     var meaningfulData = new List<string>();
//                     for (int i = 0; i < rowData.Count; i++)
//                     {
//                         if (!string.IsNullOrWhiteSpace(rowData[i]))
//                         {
//                             var header = i < headers.Count ? headers[i] : $"Column{i + 1}";
//                             if (!string.IsNullOrWhiteSpace(header))
//                             {
//                                 meaningfulData.Add($"{header}: {rowData[i]}");
//                             }
//                             else
//                             {
//                                 meaningfulData.Add(rowData[i]);
//                             }
//                         }
//                     }

//                     if (meaningfulData.Any())
//                     {
//                         sb.AppendLine($"Row {rowNumber}: {string.Join(" | ", meaningfulData)}");
//                         rowNumber++;
//                     }
//                 }

//                 sb.AppendLine(); // Add spacing between sheets
//                 sheetIndex++;
//             }

//             return sb.ToString();
//         }

//         /// <summary>
//         /// Infers the data type of a column based on sample values
//         /// </summary>
//         /// <param name="sampleValues">Sample values from the column</param>
//         /// <returns>Inferred data type description</returns>
//         private string InferDataType(List<string> sampleValues)
//         {
//             if (!sampleValues.Any()) return "Empty";

//             var numericCount = sampleValues.Count(v => double.TryParse(v, out _));
//             var dateCount = sampleValues.Count(v => DateTime.TryParse(v, out _));

//             if (numericCount > sampleValues.Count * 0.8) return "Numeric";
//             if (dateCount > sampleValues.Count * 0.8) return "Date";

//             // Check for specific patterns
//             var hasUrls = sampleValues.Any(v => v.StartsWith("http"));
//             var hasEmails = sampleValues.Any(v => v.Contains("@"));

//             if (hasUrls) return "URL/Link";
//             if (hasEmails) return "Email";

//             return "Text";
//         }

//         /// <summary>
//         /// Creates a statistical summary of the data
//         /// </summary>
//         /// <param name="sb">StringBuilder to append to</param>
//         /// <param name="headers">Column headers</param>
//         /// <param name="dataRows">Data rows</param>
//         /// <param name="sharedStringTable">Shared string table for cell value resolution</param>
//         private void CreateDataSummary(StringBuilder sb, List<string> headers, List<Row> dataRows, SharedStringTable? sharedStringTable)
//         {
//             // Analyze unique values and patterns
//             for (int colIndex = 0; colIndex < headers.Count; colIndex++)
//             {
//                 if (string.IsNullOrWhiteSpace(headers[colIndex])) continue;

//                 var columnValues = dataRows.Select(row =>
//                 {
//                     var cells = row.Elements<Cell>().ToList();
//                     return colIndex < cells.Count ? GetCellValue(cells[colIndex], sharedStringTable) : "";
//                 }).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

//                 if (columnValues.Any())
//                 {
//                     var uniqueCount = columnValues.Distinct().Count();
//                     var totalCount = columnValues.Count;

//                     sb.AppendLine($"  {headers[colIndex]}: {uniqueCount} unique values out of {totalCount} total");

//                     // Show sample unique values for categorical data
//                     if (uniqueCount <= 20 && uniqueCount < totalCount * 0.8)
//                     {
//                         var uniqueValues = columnValues.Distinct().Take(10).ToList();
//                         sb.AppendLine($"    Sample values: {string.Join(", ", uniqueValues)}");
//                     }
//                 }
//             }
//         }

//         /// <summary>
//         /// Gets the actual value from an Excel cell, handling different data types
//         /// </summary>
//         /// <param name="cell">The Excel cell</param>
//         /// <param name="sharedStringTable">Shared string table for resolving string references</param>
//         /// <returns>The cell value as a string</returns>
//         private string GetCellValue(Cell cell, SharedStringTable? sharedStringTable)
//         {
//             if (cell.CellValue == null) return "";

//             var value = cell.CellValue.InnerText;

//             // Handle different cell types
//             if (cell.DataType != null)
//             {
//                 if (cell.DataType.Value == CellValues.SharedString)
//                 {
//                     // Resolve shared string reference
//                     if (sharedStringTable != null && int.TryParse(value, out int index))
//                     {
//                         var sharedStringItems = sharedStringTable.Elements<SharedStringItem>().ToList();
//                         if (index < sharedStringItems.Count)
//                         {
//                             return sharedStringItems[index].InnerText;
//                         }
//                     }
//                 }
//                 else if (cell.DataType.Value == CellValues.Boolean)
//                 {
//                     return value == "1" ? "TRUE" : "FALSE";
//                 }
//                 else if (cell.DataType.Value == CellValues.Date)
//                 {
//                     if (DateTime.TryParse(value, out DateTime date))
//                     {
//                         return date.ToString("yyyy-MM-dd");
//                     }
//                 }
//                 else if (cell.DataType.Value == CellValues.Number)
//                 {
//                     return value;
//                 }
//             }

//             return value;
//         }

//         /// <summary>
//         /// Determines if a worksheet is a summary/aggregate sheet based on its name and content.
//         /// </summary>
//         /// <param name="sheetName">The name of the worksheet</param>
//         /// <param name="sheetData">The SheetData element of the worksheet</param>
//         /// <param name="sharedStringTable">Shared string table for cell value resolution</param>
//         /// <returns>True if it's a summary sheet, false otherwise</returns>
//         private bool IsSummarySheet(string sheetName, SheetData sheetData, SharedStringTable? sharedStringTable)
//         {
//             // Common indicators for a summary sheet:
//             // 1. Sheet name contains "Summary", "Total", "Grand", "Aggregate", "Team", "Organization"
//             // 2. Contains specific keywords like "Total", "Grand Total", "Grand Sum", "Grand Total Sum"
//             // 3. Contains common aggregate functions like SUM, COUNT, AVG, MAX, MIN
//             // 4. Contains specific column names like "Total", "Grand Total", "Grand Sum", "Grand Total Sum"
//             // 5. Contains common aggregate functions like SUM, COUNT, AVG, MAX, MIN

//             // Convert sheet name to lowercase for case-insensitive matching
//             var lowerSheetName = sheetName.ToLower();

//             // Check for common summary keywords in the name
//             if (lowerSheetName.Contains("summary") || lowerSheetName.Contains("total") || lowerSheetName.Contains("grand") || lowerSheetName.Contains("aggregate") || lowerSheetName.Contains("team") || lowerSheetName.Contains("organization"))
//             {
//                 return true;
//             }

//             // Check for specific aggregate function names in the data
//             var rows = sheetData.Descendants<Row>().ToList();
//             if (rows.Any())
//             {
//                 var headerRow = rows.FirstOrDefault();
//                 if (headerRow != null)
//                 {
//                     foreach (var cell in headerRow.Elements<Cell>())
//                     {
//                         var cellValue = GetCellValue(cell, sharedStringTable);
//                         if (!string.IsNullOrWhiteSpace(cellValue))
//                         {
//                             var lowerCellValue = cellValue.ToLower();
//                             if (lowerCellValue.Contains("sum") || lowerCellValue.Contains("count") || lowerCellValue.Contains("avg") || lowerCellValue.Contains("max") || lowerCellValue.Contains("min"))
//                             {
//                                 return true;
//                             }
//                         }
//                     }
//                 }
//             }

//             // Check for specific column names that are typically aggregate
//             if (rows.Any())
//             {
//                 var headerRow = rows.FirstOrDefault();
//                 if (headerRow != null)
//                 {
//                     foreach (var cell in headerRow.Elements<Cell>())
//                     {
//                         var cellValue = GetCellValue(cell, sharedStringTable);
//                         if (!string.IsNullOrWhiteSpace(cellValue))
//                         {
//                             var lowerCellValue = cellValue.ToLower();
//                             if (lowerCellValue.Contains("total") || lowerCellValue.Contains("grand total") || lowerCellValue.Contains("grand sum") || lowerCellValue.Contains("grand total sum"))
//                             {
//                                 return true;
//                             }
//                         }
//                     }
//                 }
//             }

//             return false;
//         }
//     }
// }




using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Backend.Services.Search
{
    /// <summary>
    /// Specialized service for processing Excel spreadsheets with enhanced data analysis capabilities.
    /// This handles the complex logic of extracting meaningful information from Excel files.
    /// </summary>
    public class ExcelProcessor
    {
        /// <summary>
        /// Extracts comprehensive content from Excel documents with enhanced analysis
        /// </summary>
        /// <param name="doc">The Excel document to process</param>
        /// <returns>Formatted text representation of the Excel content</returns>
        public string ExtractExcelContent(SpreadsheetDocument doc)
        {
            var sb = new StringBuilder();
            var workbookPart = doc.WorkbookPart;
            var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

            // Get worksheet names for better context
            var sheets = workbookPart.Workbook.Descendants<Sheet>().ToList();

            sb.AppendLine("=== EXCEL DOCUMENT ANALYSIS ===");
            sb.AppendLine($"Total Sheets: {sheets.Count}");
            sb.AppendLine();

            int sheetIndex = 0;
            foreach (var sheet in sheets)
            {
                try
                {
                    // Get the worksheet part by relationship ID
                    var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                    var worksheet = worksheetPart.Worksheet;
                    var sheetData = worksheet.GetFirstChild<SheetData>();

                    if (sheetData == null)
                    {
                        sheetIndex++;
                        continue;
                    }

                    var sheetName = sheet.Name?.Value ?? $"Sheet{sheetIndex + 1}";

                    // Check if this is a summary/aggregate sheet
                    var isSummarySheet = IsSummarySheet(sheetName, sheetData, sharedStringTable);

                    if (isSummarySheet)
                    {
                        sb.AppendLine($"=== SHEET: {sheetName} (SKIPPED - SUMMARY/AGGREGATE SHEET) ===");
                        sb.AppendLine("This sheet contains organizational/team summary data and has been excluded from analysis to prevent confusion between individual and organizational statistics.");
                        sb.AppendLine();
                        sheetIndex++;
                        continue;
                    }

                    var sheetType = "INDIVIDUAL DATA SHEET";
                    sb.AppendLine($"=== SHEET: {sheetName} ({sheetType}) ===");

                    var rows = sheetData.Descendants<Row>().OrderBy(r => r.RowIndex?.Value ?? 0).ToList();
                    if (!rows.Any())
                    {
                        sb.AppendLine("No data found in this sheet.");
                        sb.AppendLine();
                        sheetIndex++;
                        continue;
                    }

                    // Process the sheet data
                    ProcessSheetData(sb, rows, sharedStringTable, sheetName);

                    sb.AppendLine(); // Add spacing between sheets
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Error processing sheet {sheet.Name?.Value}: {ex.Message}");
                }

                sheetIndex++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Processes the data in a worksheet and formats it for RAG consumption
        /// </summary>
        private void ProcessSheetData(StringBuilder sb, List<Row> rows, SharedStringTable? sharedStringTable, string sheetName)
        {
            // Find the header row (may not be the first row)
            var headerRowIndex = FindHeaderRow(rows, sharedStringTable);
            if (headerRowIndex == -1)
            {
                sb.AppendLine("Could not identify header row in this sheet.");
                sb.AppendLine();
                return;
            }

            var headerRow = rows[headerRowIndex];
            var dataRows = rows.Skip(headerRowIndex + 1).ToList();

            // Extract and clean headers
            var headers = ExtractHeaders(headerRow, sharedStringTable);
            var maxColumns = Math.Max(headers.Count, dataRows.SelectMany(r => r.Elements<Cell>()).Count());

            // Ensure we have enough header slots
            while (headers.Count < maxColumns)
            {
                headers.Add($"Column_{headers.Count + 1}");
            }

            sb.AppendLine($"COLUMNS ({headers.Count} total):");
            for (int i = 0; i < headers.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(headers[i]))
                {
                    sb.AppendLine($"  {i + 1}. {headers[i]}");
                }
            }
            sb.AppendLine($"TOTAL ROWS: {dataRows.Count}");
            sb.AppendLine();

            // Create data summary for better context
            CreateEnhancedDataSummary(sb, headers, dataRows, sharedStringTable);

            // Process data rows with proper cell mapping
            sb.AppendLine("DETAILED DATA:");
            ProcessDataRows(sb, dataRows, headers, sharedStringTable);
        }

        /// <summary>
        /// Finds the most likely header row by analyzing content patterns
        /// </summary>
        private int FindHeaderRow(List<Row> rows, SharedStringTable? sharedStringTable)
        {
            for (int i = 0; i < Math.Min(5, rows.Count); i++) // Check first 5 rows
            {
                var row = rows[i];
                var cellValues = GetRowValues(row, sharedStringTable);

                // Headers typically have:
                // 1. Text values (not mostly numeric)
                // 2. No empty cells at the beginning
                // 3. Descriptive names

                var nonEmptyValues = cellValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                if (nonEmptyValues.Count > 0)
                {
                    var numericCount = nonEmptyValues.Count(v => IsNumeric(v));
                    var textRatio = (double)(nonEmptyValues.Count - numericCount) / nonEmptyValues.Count;

                    // If mostly text and has reasonable number of columns, likely a header
                    if (textRatio > 0.5 && nonEmptyValues.Count >= 2)
                    {
                        return i;
                    }
                }
            }

            return 0; // Default to first row
        }

        /// <summary>
        /// Extracts and cleans header values from a row
        /// </summary>
        private List<string> ExtractHeaders(Row headerRow, SharedStringTable? sharedStringTable)
        {
            var headers = new List<string>();
            var cells = headerRow.Elements<Cell>().OrderBy(c => GetColumnIndex(c.CellReference?.Value)).ToList();

            // Fill gaps in cell references
            var allColumnIndices = new List<int>();
            foreach (var cell in cells)
            {
                var colIndex = GetColumnIndex(cell.CellReference?.Value);
                allColumnIndices.Add(colIndex);
            }

            if (allColumnIndices.Any())
            {
                var maxCol = allColumnIndices.Max();
                for (int i = 0; i <= maxCol; i++)
                {
                    var cell = cells.FirstOrDefault(c => GetColumnIndex(c.CellReference?.Value) == i);
                    if (cell != null)
                    {
                        var value = GetCellValue(cell, sharedStringTable);
                        headers.Add(CleanHeaderValue(value));
                    }
                    else
                    {
                        headers.Add($"Column_{i + 1}");
                    }
                }
            }

            return headers;
        }

        /// <summary>
        /// Processes data rows with proper cell-to-column mapping
        /// </summary>
        private void ProcessDataRows(StringBuilder sb, List<Row> dataRows, List<string> headers, SharedStringTable? sharedStringTable)
        {
            int rowNumber = 1;
            foreach (var row in dataRows)
            {
                var rowData = GetRowDataMapped(row, headers.Count, sharedStringTable);

                // Create comprehensive row representation
                var meaningfulData = new List<string>();
                for (int i = 0; i < Math.Min(rowData.Count, headers.Count); i++)
                {
                    if (!string.IsNullOrWhiteSpace(rowData[i]))
                    {
                        var header = headers[i];
                        if (!string.IsNullOrWhiteSpace(header) && !header.StartsWith("Column_"))
                        {
                            meaningfulData.Add($"{header}: {rowData[i]}");
                        }
                        else
                        {
                            meaningfulData.Add(rowData[i]);
                        }
                    }
                }

                if (meaningfulData.Any())
                {
                    sb.AppendLine($"Row {rowNumber}: {string.Join(" | ", meaningfulData)}");
                    rowNumber++;
                }

                // Limit output for very large datasets
                if (rowNumber > 1000) // Configurable limit
                {
                    sb.AppendLine($"... (showing first 1000 rows out of {dataRows.Count} total rows)");
                    break;
                }
            }
        }

        /// <summary>
        /// Gets row values mapped to correct column positions
        /// </summary>
        private List<string> GetRowDataMapped(Row row, int expectedColumns, SharedStringTable? sharedStringTable)
        {
            var rowData = new List<string>(new string[expectedColumns]);

            foreach (var cell in row.Elements<Cell>())
            {
                var colIndex = GetColumnIndex(cell.CellReference?.Value);
                if (colIndex >= 0 && colIndex < expectedColumns)
                {
                    rowData[colIndex] = GetCellValue(cell, sharedStringTable) ?? "";
                }
            }

            return rowData;
        }

        /// <summary>
        /// Gets all values from a row in order
        /// </summary>
        private List<string> GetRowValues(Row row, SharedStringTable? sharedStringTable)
        {
            var values = new List<string>();
            var cells = row.Elements<Cell>().OrderBy(c => GetColumnIndex(c.CellReference?.Value)).ToList();

            foreach (var cell in cells)
            {
                values.Add(GetCellValue(cell, sharedStringTable) ?? "");
            }

            return values;
        }

        /// <summary>
        /// Converts Excel column reference to zero-based index (A=0, B=1, etc.)
        /// </summary>
        private int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrEmpty(cellReference)) return 0;

            // Extract column letters from cell reference (e.g., "A1" -> "A")
            var columnLetters = "";
            foreach (char c in cellReference)
            {
                if (char.IsLetter(c))
                {
                    columnLetters += c;
                }
                else
                {
                    break;
                }
            }

            if (string.IsNullOrEmpty(columnLetters)) return 0;

            // Convert column letters to index
            int index = 0;
            for (int i = 0; i < columnLetters.Length; i++)
            {
                index = index * 26 + (columnLetters[i] - 'A' + 1);
            }

            return index - 1; // Convert to zero-based
        }

        /// <summary>
        /// Cleans and normalizes header values
        /// </summary>
        private string CleanHeaderValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            return value.Trim()
                       .Replace("\n", " ")
                       .Replace("\r", " ")
                       .Replace("\t", " ")
                       .Trim();
        }

        /// <summary>
        /// Enhanced data summary with better statistical analysis
        /// </summary>
        private void CreateEnhancedDataSummary(StringBuilder sb, List<string> headers, List<Row> dataRows, SharedStringTable? sharedStringTable)
        {
            sb.AppendLine("DATA SUMMARY:");

            for (int colIndex = 0; colIndex < headers.Count; colIndex++)
            {
                if (string.IsNullOrWhiteSpace(headers[colIndex]) || headers[colIndex].StartsWith("Column_")) continue;

                var columnValues = dataRows.Select(row =>
                {
                    var rowData = GetRowDataMapped(row, headers.Count, sharedStringTable);
                    return colIndex < rowData.Count ? rowData[colIndex] : "";
                }).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

                if (columnValues.Any())
                {
                    var uniqueCount = columnValues.Distinct().Count();
                    var totalCount = columnValues.Count;
                    var dataType = InferDataType(columnValues.Take(10).ToList());

                    sb.AppendLine($"  {headers[colIndex]} ({dataType}): {uniqueCount} unique values out of {totalCount} total");

                    // Show sample unique values for categorical data
                    if (uniqueCount <= 20 && uniqueCount < totalCount * 0.8)
                    {
                        var uniqueValues = columnValues.Distinct().Take(5).ToList();
                        sb.AppendLine($"    Sample values: {string.Join(", ", uniqueValues)}");
                    }

                    // For numeric data, show basic statistics
                    if (dataType == "Numeric" && columnValues.Count > 0)
                    {
                        var numericValues = columnValues.Where(v => double.TryParse(v, out _))
                                                      .Select(v => double.Parse(v)).ToList();
                        if (numericValues.Any())
                        {
                            sb.AppendLine($"    Range: {numericValues.Min():F2} - {numericValues.Max():F2}, Average: {numericValues.Average():F2}");
                        }
                    }
                }
            }
            sb.AppendLine();
        }

        /// <summary>
        /// Check if a string represents a numeric value
        /// </summary>
        private bool IsNumeric(string value)
        {
            return double.TryParse(value, out _);
        }

        /// <summary>
        /// Infers the data type of a column based on sample values
        /// </summary>
        private string InferDataType(List<string> sampleValues)
        {
            if (!sampleValues.Any()) return "Empty";

            var numericCount = sampleValues.Count(v => double.TryParse(v, out _));
            var dateCount = sampleValues.Count(v => DateTime.TryParse(v, out _));
            var booleanCount = sampleValues.Count(v => v.ToLower() == "true" || v.ToLower() == "false" || v == "1" || v == "0");

            var total = sampleValues.Count;

            if (booleanCount > total * 0.8) return "Boolean";
            if (numericCount > total * 0.8) return "Numeric";
            if (dateCount > total * 0.8) return "Date";

            // Check for specific patterns
            var hasUrls = sampleValues.Any(v => v.StartsWith("http", StringComparison.OrdinalIgnoreCase));
            var hasEmails = sampleValues.Any(v => v.Contains("@") && v.Contains("."));

            if (hasUrls) return "URL/Link";
            if (hasEmails) return "Email";

            // Check if it looks like an ID field
            var hasIds = sampleValues.Any(v => v.ToLower().Contains("id") ||
                                            (v.Length < 20 && (char.IsDigit(v[0]) || v.Contains("-"))));
            if (hasIds) return "Identifier";

            return "Text";
        }

        /// <summary>
        /// Gets the actual value from an Excel cell, handling different data types and formats
        /// </summary>
        private string GetCellValue(Cell cell, SharedStringTable? sharedStringTable)
        {
            if (cell.CellValue == null) return "";

            var value = cell.CellValue.InnerText;

            // Handle different cell types
            if (cell.DataType != null)
            {
                switch (cell.DataType.Value.ToString().ToLower())
                {
                    case "sharedstring":
                        // Resolve shared string reference
                        if (sharedStringTable != null && int.TryParse(value, out int index))
                        {
                            var sharedStringItems = sharedStringTable.Elements<SharedStringItem>().ToList();
                            if (index < sharedStringItems.Count)
                            {
                                return sharedStringItems[index].InnerText;
                            }
                        }
                        break;

                    case "boolean":
                        return value == "1" ? "TRUE" : "FALSE";

                    case "date":
                        if (double.TryParse(value, out double dateValue))
                        {
                            try
                            {
                                var date = DateTime.FromOADate(dateValue);
                                return date.ToString("yyyy-MM-dd");
                            }
                            catch
                            {
                                return value;
                            }
                        }
                        break;

                    case "number":
                        // Handle numeric formatting
                        if (double.TryParse(value, out double numValue))
                        {
                            // Check if it's likely a date stored as number
                            if (numValue > 1 && numValue < 100000) // Reasonable Excel date range
                            {
                                try
                                {
                                    var date = DateTime.FromOADate(numValue);
                                    if (date.Year > 1900 && date.Year < 2100)
                                    {
                                        return date.ToString("yyyy-MM-dd");
                                    }
                                }
                                catch { }
                            }

                            // Format numeric values appropriately
                            if (numValue == Math.Floor(numValue))
                            {
                                return ((long)numValue).ToString();
                            }
                            else
                            {
                                return numValue.ToString("F2");
                            }
                        }
                        break;
                }
            }
            else
            {
                // Handle cells without explicit data type
                if (double.TryParse(value, out double numValue))
                {
                    // Check if it might be a date
                    if (numValue > 1 && numValue < 100000)
                    {
                        try
                        {
                            var date = DateTime.FromOADate(numValue);
                            if (date.Year > 1900 && date.Year < 2100)
                            {
                                return date.ToString("yyyy-MM-dd");
                            }
                        }
                        catch { }
                    }
                }
            }

            return value;
        }

        /// <summary>
        /// Determines if a worksheet is a summary/aggregate sheet based on its name and content.
        /// </summary>
        private bool IsSummarySheet(string sheetName, SheetData sheetData, SharedStringTable? sharedStringTable)
        {
            // Convert sheet name to lowercase for case-insensitive matching
            var lowerSheetName = sheetName.ToLower();

            // Check for common summary keywords in the name
            var summaryKeywords = new[] { "summary", "total", "grand", "aggregate", "team", "organization", "overview", "dashboard" };
            if (summaryKeywords.Any(keyword => lowerSheetName.Contains(keyword)))
            {
                return true;
            }

            // Check content for aggregate indicators
            var rows = sheetData.Descendants<Row>().Take(5).ToList(); // Check first few rows only
            foreach (var row in rows)
            {
                var cellValues = GetRowValues(row, sharedStringTable);
                foreach (var cellValue in cellValues)
                {
                    if (!string.IsNullOrWhiteSpace(cellValue))
                    {
                        var lowerCellValue = cellValue.ToLower();
                        var aggregateKeywords = new[] { "sum", "count", "avg", "average", "max", "min", "total", "grand total", "subtotal" };

                        if (aggregateKeywords.Any(keyword => lowerCellValue.Contains(keyword)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
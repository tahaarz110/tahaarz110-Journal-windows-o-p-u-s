// ابتدای فایل: Services/ImportExportService.cs
// مسیر: /Services/ImportExportService.cs

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using TradingJournal.Data;
using TradingJournal.Data.Models;

namespace TradingJournal.Services
{
    public enum ExportFormat
    {
        Excel,
        CSV,
        JSON,
        XML
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public int TotalRecords { get; set; }
        public int ImportedRecords { get; set; }
        public int FailedRecords { get; set; }
        public List<string> Errors { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    public class ExportOptions
    {
        public ExportFormat Format { get; set; } = ExportFormat.Excel;
        public List<string>? Fields { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? FilterExpression { get; set; }
        public bool IncludeImages { get; set; } = false;
        public bool CompressOutput { get; set; } = false;
    }

    public interface IImportExportService
    {
        Task<ImportResult> ImportFromCsvAsync(string filePath, bool hasHeaders = true);
        Task<ImportResult> ImportFromExcelAsync(string filePath, string sheetName = "Sheet1");
        Task<ImportResult> ImportFromJsonAsync(string filePath);
        Task<ImportResult> ImportFromMetaTraderAsync(string filePath);
        
        Task<string> ExportToExcelAsync(ExportOptions options);
        Task<string> ExportToCsvAsync(ExportOptions options);
        Task<string> ExportToJsonAsync(ExportOptions options);
        Task<string> ExportToXmlAsync(ExportOptions options);
        
        Task<bool> ValidateImportFileAsync(string filePath);
        Dictionary<string, string> GetFieldMappings();
        void SetFieldMappings(Dictionary<string, string> mappings);
    }

    public class ImportExportService : IImportExportService
    {
        private readonly DatabaseContext _dbContext;
        private readonly string _exportPath;
        private Dictionary<string, string> _fieldMappings;

        public ImportExportService()
        {
            _dbContext = new DatabaseContext();
            _exportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "TradingJournal",
                "Exports"
            );
            Directory.CreateDirectory(_exportPath);
            
            _fieldMappings = GetDefaultFieldMappings();
        }

        private Dictionary<string, string> GetDefaultFieldMappings()
        {
            return new Dictionary<string, string>
            {
                { "Symbol", "Symbol" },
                { "نماد", "Symbol" },
                { "Entry Date", "EntryDate" },
                { "تاریخ ورود", "EntryDate" },
                { "Entry Price", "EntryPrice" },
                { "قیمت ورود", "EntryPrice" },
                { "Exit Date", "ExitDate" },
                { "تاریخ خروج", "ExitDate" },
                { "Exit Price", "ExitPrice" },
                { "قیمت خروج", "ExitPrice" },
                { "Volume", "Volume" },
                { "حجم", "Volume" },
                { "Direction", "Direction" },
                { "جهت", "Direction" },
                { "Stop Loss", "StopLoss" },
                { "حد ضرر", "StopLoss" },
                { "Take Profit", "TakeProfit" },
                { "حد سود", "TakeProfit" },
                { "Profit/Loss", "ProfitLoss" },
                { "سود/زیان", "ProfitLoss" },
                { "Commission", "Commission" },
                { "کارمزد", "Commission" },
                { "Swap", "Swap" },
                { "سواپ", "Swap" }
            };
        }

        public async Task<ImportResult> ImportFromCsvAsync(string filePath, bool hasHeaders = true)
        {
            var result = new ImportResult();
            var startTime = DateTime.Now;

            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = hasHeaders,
                    MissingFieldFound = null,
                    BadDataFound = null
                });

                if (hasHeaders)
                {
                    csv.Read();
                    csv.ReadHeader();
                    var headers = csv.HeaderRecord;
                    ValidateHeaders(headers);
                }

                var trades = new List<Trade>();
                
                while (csv.Read())
                {
                    result.TotalRecords++;
                    
                    try
                    {
                        var trade = MapCsvRecordToTrade(csv);
                        trades.Add(trade);
                        result.ImportedRecords++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedRecords++;
                        result.Errors.Add($"سطر {csv.Parser.Row}: {ex.Message}");
                        
                        if (result.Errors.Count >= 100)
                        {
                            result.Errors.Add("خطاهای بیشتر نمایش داده نشد...");
                            break;
                        }
                    }
                }

                // Save to database
                if (trades.Any())
                {
                    _dbContext.Trades.AddRange(trades);
                    await _dbContext.SaveChangesAsync();
                }

                result.Success = result.FailedRecords == 0;
                result.Duration = DateTime.Now - startTime;

                Log.Information($"وارد کردن از CSV: {result.ImportedRecords}/{result.TotalRecords} رکورد");
                
                NotificationService.Instance.Show(
                    "ورود داده",
                    $"{result.ImportedRecords} معامله با موفقیت وارد شد",
                    result.Success ? NotificationType.Success : NotificationType.Warning
                );
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
                Log.Error(ex, "خطا در ورود از CSV");
            }

            return result;
        }

        private Trade MapCsvRecordToTrade(CsvReader csv)
        {
            var trade = new Trade();

            foreach (var mapping in _fieldMappings)
            {
                try
                {
                    var csvValue = csv.GetField(mapping.Key);
                    if (string.IsNullOrWhiteSpace(csvValue))
                        continue;

                    var property = typeof(Trade).GetProperty(mapping.Value);
                    if (property == null)
                        continue;

                    object? value = ConvertValue(csvValue, property.PropertyType);
                    if (value != null)
                    {
                        property.SetValue(trade, value);
                    }
                }
                catch
                {
                    // Skip field if mapping fails
                }
            }

            return trade;
        }

        private object? ConvertValue(string value, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                if (targetType == typeof(string))
                    return value;
                
                if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                {
                    if (DateTime.TryParse(value, out var date))
                        return date;
                    return null;
                }
                
                if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                {
                    if (decimal.TryParse(value, out var dec))
                        return dec;
                    return null;
                }
                
                if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    if (int.TryParse(value, out var intVal))
                        return intVal;
                    return null;
                }
                
                if (targetType == typeof(TradeDirection))
                {
                    if (value.ToLower().Contains("buy") || value.Contains("خرید"))
                        return TradeDirection.Buy;
                    if (value.ToLower().Contains("sell") || value.Contains("فروش"))
                        return TradeDirection.Sell;
                    return TradeDirection.Buy;
                }
                
                if (targetType == typeof(TradeStatus))
                {
                    if (value.ToLower().Contains("open") || value.Contains("باز"))
                        return TradeStatus.Open;
                    if (value.ToLower().Contains("close") || value.Contains("بسته"))
                        return TradeStatus.Closed;
                    return TradeStatus.Open;
                }

                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        public async Task<ImportResult> ImportFromExcelAsync(string filePath, string sheetName = "Sheet1")
        {
            var result = new ImportResult();
            var startTime = DateTime.Now;

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(sheetName);
                
                if (worksheet == null)
                {
                    result.Errors.Add($"کاربرگ '{sheetName}' یافت نشد");
                    return result;
                }

                var rows = worksheet.RowsUsed().Skip(1); // Skip header
                var headers = worksheet.Row(1).CellsUsed().Select(c => c.Value.ToString()).ToList();
                
                ValidateHeaders(headers.ToArray());

                var trades = new List<Trade>();
                
                foreach (var row in rows)
                {
                    result.TotalRecords++;
                    
                    try
                    {
                        var trade = MapExcelRowToTrade(row, headers);
                        trades.Add(trade);
                        result.ImportedRecords++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedRecords++;
                        result.Errors.Add($"سطر {row.RowNumber()}: {ex.Message}");
                    }
                }

                // Save to database
                if (trades.Any())
                {
                    _dbContext.Trades.AddRange(trades);
                    await _dbContext.SaveChangesAsync();
                }

                result.Success = result.FailedRecords == 0;
                result.Duration = DateTime.Now - startTime;

                Log.Information($"وارد کردن از Excel: {result.ImportedRecords}/{result.TotalRecords} رکورد");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
                Log.Error(ex, "خطا در ورود از Excel");
            }

            return result;
        }

        private Trade MapExcelRowToTrade(IXLRow row, List<string> headers)
        {
            var trade = new Trade();

            for (int i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                if (!_fieldMappings.ContainsKey(header))
                    continue;

                var propertyName = _fieldMappings[header];
                var property = typeof(Trade).GetProperty(propertyName);
                if (property == null)
                    continue;

                var cellValue = row.Cell(i + 1).Value.ToString();
                var value = ConvertValue(cellValue, property.PropertyType);
                
                if (value != null)
                {
                    property.SetValue(trade, value);
                }
            }

            return trade;
        }

        public async Task<ImportResult> ImportFromJsonAsync(string filePath)
        {
            var result = new ImportResult();
            var startTime = DateTime.Now;

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var trades = JsonConvert.DeserializeObject<List<Trade>>(json);

                if (trades != null && trades.Any())
                {
                    result.TotalRecords = trades.Count;
                    
                    foreach (var trade in trades)
                    {
                        try
                        {
                            _dbContext.Trades.Add(trade);
                            result.ImportedRecords++;
                        }
                        catch (Exception ex)
                        {
                            result.FailedRecords++;
                            result.Errors.Add($"معامله {trade.Symbol}: {ex.Message}");
                        }
                    }

                    await _dbContext.SaveChangesAsync();
                }

                result.Success = result.FailedRecords == 0;
                result.Duration = DateTime.Now - startTime;

                Log.Information($"وارد کردن از JSON: {result.ImportedRecords}/{result.TotalRecords} رکورد");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
                Log.Error(ex, "خطا در ورود از JSON");
            }

            return result;
        }

        public async Task<ImportResult> ImportFromMetaTraderAsync(string filePath)
        {
            var result = new ImportResult();
            var startTime = DateTime.Now;

            try
            {
                // Read MT4/MT5 export file (usually HTML or CSV)
                var content = await File.ReadAllTextAsync(filePath);
                
                // Parse based on file format
                if (filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                {
                    result = await ImportFromMetaTraderHtmlAsync(content);
                }
                else if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    result = await ImportFromCsvAsync(filePath, true);
                }

                result.Duration = DateTime.Now - startTime;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
                Log.Error(ex, "خطا در ورود از MetaTrader");
            }

            return result;
        }

        private async Task<ImportResult> ImportFromMetaTraderHtmlAsync(string html)
        {
            // Parse HTML table from MT4/MT5
            // Implementation depends on exact HTML format
            return await Task.FromResult(new ImportResult());
        }

        public async Task<string> ExportToExcelAsync(ExportOptions options)
        {
            try
            {
                var query = BuildExportQuery(options);
                var trades = await query.ToListAsync();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Trades");

                // Add headers
                var headers = GetExportHeaders(options);
                for (int i = 0; i < headers.Count; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                // Style header row
                var headerRange = worksheet.Range(1, 1, 1, headers.Count);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Add data
                var row = 2;
                foreach (var trade in trades)
                {
                    for (int col = 0; col < headers.Count; col++)
                    {
                        var value = GetTradeFieldValue(trade, headers[col]);
                        worksheet.Cell(row, col + 1).Value = value?.ToString() ?? "";
                    }
                    row++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                // Save file
                var fileName = $"TradingJournal_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = Path.Combine(_exportPath, fileName);
                workbook.SaveAs(filePath);

                Log.Information($"صادر شد به Excel: {trades.Count} رکورد");
                
                NotificationService.Instance.Show(
                    "صدور داده",
                    $"{trades.Count} معامله با موفقیت صادر شد",
                    NotificationType.Success
                );

                return filePath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در صدور به Excel");
                throw;
            }
        }

        public async Task<string> ExportToCsvAsync(ExportOptions options)
        {
            try
            {
                var query = BuildExportQuery(options);
                var trades = await query.ToListAsync();

                var fileName = $"TradingJournal_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(_exportPath, fileName);

                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

                // Write headers
                var headers = GetExportHeaders(options);
                foreach (var header in headers)
                {
                    csv.WriteField(header);
                }
                csv.NextRecord();

                // Write data
                foreach (var trade in trades)
                {
                    foreach (var header in headers)
                    {
                        var value = GetTradeFieldValue(trade, header);
                        csv.WriteField(value);
                    }
                    csv.NextRecord();
                }

                Log.Information($"صادر شد به CSV: {trades.Count} رکورد");

                return filePath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در صدور به CSV");
                throw;
            }
        }

        public async Task<string> ExportToJsonAsync(ExportOptions options)
        {
            try
            {
                var query = BuildExportQuery(options);
                var trades = await query.ToListAsync();

                var fileName = $"TradingJournal_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(_exportPath, fileName);

                var json = JsonConvert.SerializeObject(trades, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);

                Log.Information($"صادر شد به JSON: {trades.Count} رکورد");

                return filePath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در صدور به JSON");
                throw;
            }
        }

        public async Task<string> ExportToXmlAsync(ExportOptions options)
        {
            try
            {
                var query = BuildExportQuery(options);
                var trades = await query.ToListAsync();

                var fileName = $"TradingJournal_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                var filePath = Path.Combine(_exportPath, fileName);

                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<Trade>));
                using var writer = new StreamWriter(filePath);
                serializer.Serialize(writer, trades);

                Log.Information($"صادر شد به XML: {trades.Count} رکورد");

                return filePath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در صدور به XML");
                throw;
            }
        }

        private IQueryable<Trade> BuildExportQuery(ExportOptions options)
        {
            var query = _dbContext.Trades.AsQueryable();

            if (options.FromDate.HasValue)
            {
                query = query.Where(t => t.EntryDate >= options.FromDate.Value);
            }

            if (options.ToDate.HasValue)
            {
                query = query.Where(t => t.EntryDate <= options.ToDate.Value);
            }

            // Apply custom filter expression if provided
            // This would need a proper expression parser

            return query.OrderByDescending(t => t.EntryDate);
        }

        private List<string> GetExportHeaders(ExportOptions options)
        {
            if (options.Fields != null && options.Fields.Any())
            {
                return options.Fields;
            }

            // Return default fields
            return new List<string>
            {
                "Symbol", "EntryDate", "EntryPrice", "ExitDate", "ExitPrice",
                "Volume", "Direction", "Status", "ProfitLoss", "Commission", "Swap"
            };
        }

        private object? GetTradeFieldValue(Trade trade, string fieldName)
        {
            var property = typeof(Trade).GetProperty(fieldName);
            return property?.GetValue(trade);
        }

        public async Task<bool> ValidateImportFileAsync(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                
                switch (extension)
                {
                    case ".csv":
                        return await ValidateCsvFileAsync(filePath);
                    case ".xlsx":
                    case ".xls":
                        return await ValidateExcelFileAsync(filePath);
                    case ".json":
                        return await ValidateJsonFileAsync(filePath);
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateCsvFileAsync(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                
                csv.Read();
                csv.ReadHeader();
                
                return csv.HeaderRecord != null && csv.HeaderRecord.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateExcelFileAsync(string filePath)
        {
            try
            {
                using var workbook = new XLWorkbook(filePath);
                return workbook.Worksheets.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateJsonFileAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var trades = JsonConvert.DeserializeObject<List<Trade>>(json);
                return trades != null;
            }
            catch
            {
                return false;
            }
        }

        private void ValidateHeaders(string[]? headers)
        {
            if (headers == null || headers.Length == 0)
            {
                throw new InvalidOperationException("فایل فاقد هدر است");
            }

            // Check for required fields
            var requiredFields = new[] { "Symbol", "نماد", "Entry Date", "تاریخ ورود" };
            var hasRequired = requiredFields.Any(f => headers.Contains(f, StringComparer.OrdinalIgnoreCase));
            
            if (!hasRequired)
            {
                throw new InvalidOperationException("فیلدهای اجباری در فایل یافت نشد");
            }
        }

        public Dictionary<string, string> GetFieldMappings()
        {
            return _fieldMappings;
        }

        public void SetFieldMappings(Dictionary<string, string> mappings)
        {
            _fieldMappings = mappings;
        }
    }
}

// پایان فایل: Services/ImportExportService.cs
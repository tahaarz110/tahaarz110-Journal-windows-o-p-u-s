// مسیر فایل: Core/QueryEngine/ExportManager.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using CsvHelper;
using System.Globalization;

namespace TradingJournal.Core.QueryEngine
{
    public class ExportManager
    {
        public async Task<bool> ExportToExcelAsync<T>(IEnumerable<T> data, string filePath, string sheetName = "Sheet1")
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add(sheetName);

                // تبدیل به DataTable
                var dataTable = ToDataTable(data);
                
                // اضافه کردن داده‌ها به worksheet
                worksheet.Cell(1, 1).InsertTable(dataTable);
                
                // تنظیمات ظاهری
                worksheet.Columns().AdjustToContents();
                worksheet.RangeUsed().Style.Font.FontName = "B Nazanin";
                worksheet.Row(1).Style.Font.Bold = true;
                worksheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;
                
                // راست‌چین برای فارسی
                worksheet.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                worksheet.Style.Alignment.SetReadingOrder(XLAlignmentReadingOrderValues.RightToLeft);

                // ذخیره فایل
                workbook.SaveAs(filePath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در Export به Excel: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExportToCsvAsync<T>(IEnumerable<T> data, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
                using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Encoding = Encoding.UTF8,
                    HasHeaderRecord = true
                });

                await csv.WriteRecordsAsync(data);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در Export به CSV: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExportToPdfAsync<T>(IEnumerable<T> data, string filePath, string title = "گزارش")
        {
            try
            {
                // ثبت فونت فارسی
                var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "B Nazanin.ttf");
                if (!FontFactory.IsRegistered("B Nazanin") && File.Exists(fontPath))
                {
                    FontFactory.Register(fontPath, "B Nazanin");
                }

                var baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                var font = new Font(baseFont, 10, Font.NORMAL);
                var headerFont = new Font(baseFont, 12, Font.BOLD);
                var titleFont = new Font(baseFont, 16, Font.BOLD);

                using var document = new Document(PageSize.A4.Rotate());
                using var writer = PdfWriter.GetInstance(document, new FileStream(filePath, FileMode.Create));

                document.Open();

                // عنوان
                var titleParagraph = new Paragraph(title, titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20
                };
                document.Add(titleParagraph);

                // تبدیل به DataTable
                var dataTable = ToDataTable(data);
                
                // ایجاد جدول PDF
                var pdfTable = new PdfPTable(dataTable.Columns.Count)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100
                };

                // اضافه کردن سرستون‌ها
                foreach (DataColumn column in dataTable.Columns)
                {
                    var cell = new PdfPCell(new Phrase(column.ColumnName, headerFont))
                    {
                        BackgroundColor = BaseColor.LIGHT_GRAY,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL
                    };
                    pdfTable.AddCell(cell);
                }

                // اضافه کردن داده‌ها
                foreach (DataRow row in dataTable.Rows)
                {
                    foreach (var item in row.ItemArray)
                    {
                        var cell = new PdfPCell(new Phrase(item?.ToString() ?? "", font))
                        {
                            RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                            HorizontalAlignment = Element.ALIGN_RIGHT
                        };
                        pdfTable.AddCell(cell);
                    }
                }

                document.Add(pdfTable);
                document.Close();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در Export به PDF: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExportToJsonAsync<T>(IEnumerable<T> data, string filePath)
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = System.Text.Json.JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در Export به JSON: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExportToHtmlAsync<T>(IEnumerable<T> data, string filePath, string title = "گزارش")
        {
            try
            {
                var html = new StringBuilder();
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html dir='rtl' lang='fa'>");
                html.AppendLine("<head>");
                html.AppendLine("<meta charset='UTF-8'>");
                html.AppendLine($"<title>{title}</title>");
                html.AppendLine("<style>");
                html.AppendLine(@"
                    body { font-family: 'B Nazanin', Tahoma, Arial; direction: rtl; }
                    table { width: 100%; border-collapse: collapse; margin-top: 20px; }
                    th, td { border: 1px solid #ddd; padding: 8px; text-align: right; }
                    th { background-color: #f2f2f2; font-weight: bold; }
                    tr:nth-child(even) { background-color: #f9f9f9; }
                    h1 { text-align: center; color: #333; }
                ");
                html.AppendLine("</style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");
                html.AppendLine($"<h1>{title}</h1>");

                // تبدیل به DataTable
                var dataTable = ToDataTable(data);

                html.AppendLine("<table>");
                
                // سرستون‌ها
                html.AppendLine("<thead><tr>");
                foreach (DataColumn column in dataTable.Columns)
                {
                    html.AppendLine($"<th>{column.ColumnName}</th>");
                }
                html.AppendLine("</tr></thead>");

                // داده‌ها
                html.AppendLine("<tbody>");
                foreach (DataRow row in dataTable.Rows)
                {
                    html.AppendLine("<tr>");
                    foreach (var item in row.ItemArray)
                    {
                        html.AppendLine($"<td>{item}</td>");
                    }
                    html.AppendLine("</tr>");
                }
                html.AppendLine("</tbody>");
                
                html.AppendLine("</table>");
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                await File.WriteAllTextAsync(filePath, html.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در Export به HTML: {ex.Message}");
                return false;
            }
        }

        private DataTable ToDataTable<T>(IEnumerable<T> data)
        {
            var dataTable = new DataTable();
            var properties = typeof(T).GetProperties();

            // اضافه کردن ستون‌ها
            foreach (var prop in properties)
            {
                var displayName = GetDisplayName(prop);
                dataTable.Columns.Add(displayName, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }

            // اضافه کردن ردیف‌ها
            foreach (var item in data)
            {
                var values = new object[properties.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    values[i] = properties[i].GetValue(item) ?? DBNull.Value;
                }
                dataTable.Rows.Add(values);
            }

            return dataTable;
        }

        private string GetDisplayName(System.Reflection.PropertyInfo property)
        {
            var displayAttr = property.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
                .FirstOrDefault() as System.ComponentModel.DataAnnotations.DisplayAttribute;
            
            return displayAttr?.Name ?? property.Name;
        }

        public async Task<ExportResult> ExportWithOptionsAsync<T>(
            IEnumerable<T> data,
            ExportOptions options)
        {
            var result = new ExportResult();

            try
            {
                var success = options.Format switch
                {
                    ExportFormat.Excel => await ExportToExcelAsync(data, options.FilePath, options.Title),
                    ExportFormat.Csv => await ExportToCsvAsync(data, options.FilePath),
                    ExportFormat.Pdf => await ExportToPdfAsync(data, options.FilePath, options.Title),
                    ExportFormat.Json => await ExportToJsonAsync(data, options.FilePath),
                    ExportFormat.Html => await ExportToHtmlAsync(data, options.FilePath, options.Title),
                    _ => false
                };

                result.Success = success;
                if (success)
                {
                    result.FilePath = options.FilePath;
                    result.Message = "خروجی با موفقیت ایجاد شد";
                }
                else
                {
                    result.Message = "خطا در ایجاد خروجی";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا: {ex.Message}";
            }

            return result;
        }
    }

    public class ExportOptions
    {
        public ExportFormat Format { get; set; }
        public string FilePath { get; set; }
        public string Title { get; set; }
        public bool IncludeHeaders { get; set; } = true;
        public bool IncludeFooter { get; set; } = false;
        public Dictionary<string, string> CustomHeaders { get; set; }
    }

    public enum ExportFormat
    {
        Excel,
        Csv,
        Pdf,
        Json,
        Html
    }

    public class ExportResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string Message { get; set; }
        public DateTime ExportDate { get; set; } = DateTime.Now;
    }
}
// پایان کد
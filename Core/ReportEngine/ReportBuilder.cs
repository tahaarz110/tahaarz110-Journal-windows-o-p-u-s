// ابتدای فایل: Core/ReportEngine/ReportBuilder.cs - بخش 1
// مسیر: /Core/ReportEngine/ReportBuilder.cs

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Serilog;
using TradingJournal.Core.QueryEngine;
using TradingJournal.Core.WidgetEngine;
using TradingJournal.Data;
using TradingJournal.Data.Models;
using ClosedXML.Excel;
using System.Data;

namespace TradingJournal.Core.ReportEngine
{
    public enum ReportFormat
    {
        PDF,
        Excel,
        HTML,
        CSV,
        Print
    }

    public class ReportDefinition
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public string ReportName { get; set; } = string.Empty;
        public string ReportNameFa { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReportFormat Format { get; set; }
        public JObject Layout { get; set; } = new JObject();
        public JObject DataSources { get; set; } = new JObject();
        public JObject Parameters { get; set; } = new JObject();
        public JObject Sections { get; set; } = new JObject();
        public JObject Styles { get; set; } = new JObject();
    }

    public class ReportBuilder
    {
        private readonly DatabaseContext _dbContext;
        private readonly QueryEngine.QueryEngine _queryEngine;
        private readonly WidgetBuilder _widgetBuilder;
        private readonly Dictionary<string, DataTable> _dataSets;

        public ReportBuilder()
        {
            _dbContext = new DatabaseContext();
            _queryEngine = new QueryEngine.QueryEngine();
            _widgetBuilder = new WidgetBuilder();
            _dataSets = new Dictionary<string, DataTable>();
        }

        public async Task<byte[]> GenerateReportAsync(ReportDefinition definition, Dictionary<string, object>? parameters = null)
        {
            try
            {
                // Load data
                await LoadDataSourcesAsync(definition.DataSources, parameters);

                // Generate report based on format
                return definition.Format switch
                {
                    ReportFormat.PDF => await GeneratePdfReport(definition),
                    ReportFormat.Excel => await GenerateExcelReport(definition),
                    ReportFormat.HTML => await GenerateHtmlReport(definition),
                    ReportFormat.CSV => await GenerateCsvReport(definition),
                    ReportFormat.Print => await GeneratePrintReport(definition),
                    _ => throw new NotSupportedException($"Report format {definition.Format} not supported")
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error generating report: {definition.ReportName}");
                throw;
            }
        }

        private async Task LoadDataSourcesAsync(JObject dataSources, Dictionary<string, object>? parameters)
        {
            foreach (var source in dataSources.Properties())
            {
                var sourceConfig = source.Value as JObject;
                if (sourceConfig != null)
                {
                    var dataTable = await LoadDataTableAsync(sourceConfig, parameters);
                    _dataSets[source.Name] = dataTable;
                }
            }
        }

        private async Task<DataTable> LoadDataTableAsync(JObject config, Dictionary<string, object>? parameters)
        {
            var entity = config["entity"]?.ToString() ?? "Trade";
            var table = new DataTable(entity);

            // Build query
            var query = new QueryBuilder
            {
                EntityType = entity
            };

            // Apply filters
            var filters = config["filters"] as JArray;
            if (filters != null)
            {
                query.Filters = ParseFilters(filters, parameters);
            }

            // Apply sorting
            var sorts = config["sorts"] as JArray;
            if (sorts != null)
            {
                query.Sorts = ParseSorts(sorts);
            }

            // Apply pagination
            if (config["limit"] != null)
            {
                query.Take = config["limit"].Value<int>();
            }

            // Execute query
            var result = await _queryEngine.ExecuteQueryAsync<Trade>(query);

            // Convert to DataTable
            if (result.Data.Any())
            {
                // Add columns
                var properties = typeof(Trade).GetProperties();
                foreach (var prop in properties)
                {
                    table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                }

                // Add rows
                foreach (var item in result.Data)
                {
                    var row = table.NewRow();
                    foreach (var prop in properties)
                    {
                        row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                    }
                    table.Rows.Add(row);
                }
            }

            return table;
        }

        private List<FilterCondition> ParseFilters(JArray filters, Dictionary<string, object>? parameters)
        {
            var conditions = new List<FilterCondition>();
            
            foreach (JObject filter in filters)
            {
                var condition = new FilterCondition
                {
                    Field = filter["field"]?.ToString() ?? "",
                    Operator = Enum.Parse<FilterOperator>(filter["operator"]?.ToString() ?? "Equals"),
                    Value = ResolveParameterValue(filter["value"], parameters),
                    LogicalOperator = Enum.Parse<LogicalOperator>(filter["logicalOperator"]?.ToString() ?? "And")
                };
                
                conditions.Add(condition);
            }
            
            return conditions;
        }

        private List<SortCondition> ParseSorts(JArray sorts)
        {
            var conditions = new List<SortCondition>();
            
            foreach (JObject sort in sorts)
            {
                conditions.Add(new SortCondition
                {
                    Field = sort["field"]?.ToString() ?? "",
                    Ascending = sort["ascending"]?.Value<bool>() ?? true
                });
            }
            
            return conditions;
        }

        private object? ResolveParameterValue(JToken? value, Dictionary<string, object>? parameters)
        {
            if (value == null) return null;
            
            var strValue = value.ToString();
            if (strValue.StartsWith("@") && parameters != null)
            {
                var paramName = strValue.Substring(1);
                return parameters.GetValueOrDefault(paramName);
            }
            
            return value.ToObject<object>();
        }
// ابتدای بخش 2 فایل: Core/ReportEngine/ReportBuilder.cs
// ادامه از بخش 1

        private async Task<byte[]> GeneratePdfReport(ReportDefinition definition)
        {
            using var document = new PdfDocument();
            document.Info.Title = definition.ReportName;
            document.Info.Author = "Trading Journal";
            document.Info.CreationDate = DateTime.Now;

            // Process sections
            var sections = definition.Sections.Properties();
            foreach (var section in sections.OrderBy(s => s.Value["order"]?.Value<int>() ?? 0))
            {
                var page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                
                using var gfx = XGraphics.FromPdfPage(page);
                var yPosition = 40.0;
                
                // Render section
                await RenderPdfSection(gfx, section.Value as JObject, ref yPosition, page);
            }

            // Save to memory stream
            using var stream = new MemoryStream();
            document.Save(stream, false);
            return stream.ToArray();
        }

        private async Task RenderPdfSection(XGraphics gfx, JObject section, ref double yPosition, PdfPage page)
        {
            var sectionType = section["type"]?.ToString() ?? "text";
            
            switch (sectionType.ToLower())
            {
                case "header":
                    RenderPdfHeader(gfx, section, ref yPosition);
                    break;
                    
                case "text":
                    RenderPdfText(gfx, section, ref yPosition);
                    break;
                    
                case "table":
                    RenderPdfTable(gfx, section, ref yPosition, page);
                    break;
                    
                case "chart":
                    await RenderPdfChart(gfx, section, ref yPosition);
                    break;
                    
                case "summary":
                    RenderPdfSummary(gfx, section, ref yPosition);
                    break;
                    
                case "pagebreak":
                    yPosition = page.Height + 100; // Force new page
                    break;
            }
        }

        private void RenderPdfHeader(XGraphics gfx, JObject section, ref double yPosition)
        {
            var title = section["title"]?.ToString() ?? "";
            var subtitle = section["subtitle"]?.ToString() ?? "";
            
            // Title
            var titleFont = new XFont("Arial", 20, XFontStyle.Bold);
            gfx.DrawString(title, titleFont, XBrushes.Black,
                new XRect(40, yPosition, gfx.PdfPage.Width - 80, 30),
                XStringFormats.TopCenter);
            yPosition += 35;
            
            // Subtitle
            if (!string.IsNullOrEmpty(subtitle))
            {
                var subtitleFont = new XFont("Arial", 14, XFontStyle.Regular);
                gfx.DrawString(subtitle, subtitleFont, XBrushes.Gray,
                    new XRect(40, yPosition, gfx.PdfPage.Width - 80, 20),
                    XStringFormats.TopCenter);
                yPosition += 25;
            }
            
            // Date
            var dateFont = new XFont("Arial", 10, XFontStyle.Regular);
            gfx.DrawString(DateTime.Now.ToString("yyyy/MM/dd HH:mm"), dateFont, XBrushes.Gray,
                new XRect(40, yPosition, gfx.PdfPage.Width - 80, 15),
                XStringFormats.TopRight);
            yPosition += 25;
            
            // Separator line
            gfx.DrawLine(XPens.LightGray, 40, yPosition, gfx.PdfPage.Width - 40, yPosition);
            yPosition += 10;
        }

        private void RenderPdfText(XGraphics gfx, JObject section, ref double yPosition)
        {
            var text = section["text"]?.ToString() ?? "";
            var fontSize = section["fontSize"]?.Value<int>() ?? 12;
            var isBold = section["bold"]?.Value<bool>() ?? false;
            
            var font = new XFont("Arial", fontSize, isBold ? XFontStyle.Bold : XFontStyle.Regular);
            
            // Word wrap
            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";
            var maxWidth = gfx.PdfPage.Width - 80;
            
            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                var size = gfx.MeasureString(testLine, font);
                
                if (size.Width > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                        lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);
            
            // Draw lines
            foreach (var line in lines)
            {
                gfx.DrawString(line, font, XBrushes.Black, new XPoint(40, yPosition));
                yPosition += fontSize + 5;
            }
            
            yPosition += 10;
        }

        private void RenderPdfTable(XGraphics gfx, JObject section, ref double yPosition, PdfPage page)
        {
            var dataSource = section["dataSource"]?.ToString() ?? "";
            if (!_dataSets.TryGetValue(dataSource, out var dataTable))
                return;
            
            var columns = section["columns"] as JArray;
            if (columns == null || columns.Count == 0)
                return;
            
            var font = new XFont("Arial", 10, XFontStyle.Regular);
            var headerFont = new XFont("Arial", 11, XFontStyle.Bold);
            var cellHeight = 25;
            var startX = 40;
            var tableWidth = page.Width - 80;
            var columnWidth = tableWidth / columns.Count;
            
            // Header
            var x = startX;
            foreach (JObject column in columns)
            {
                var header = column["header"]?.ToString() ?? "";
                gfx.DrawRectangle(XPens.Black, XBrushes.LightGray, x, yPosition, columnWidth, cellHeight);
                gfx.DrawString(header, headerFont, XBrushes.Black,
                    new XRect(x + 5, yPosition + 5, columnWidth - 10, cellHeight - 10),
                    XStringFormats.CenterLeft);
                x += columnWidth;
            }
            yPosition += cellHeight;
            
            // Rows
            foreach (DataRow row in dataTable.Rows)
            {
                if (yPosition + cellHeight > page.Height - 40)
                    break; // Need new page
                
                x = startX;
                foreach (JObject column in columns)
                {
                    var field = column["field"]?.ToString() ?? "";
                    var value = row[field]?.ToString() ?? "";
                    
                    gfx.DrawRectangle(XPens.Black, x, yPosition, columnWidth, cellHeight);
                    gfx.DrawString(value, font, XBrushes.Black,
                        new XRect(x + 5, yPosition + 5, columnWidth - 10, cellHeight - 10),
                        XStringFormats.CenterLeft);
                    x += columnWidth;
                }
                yPosition += cellHeight;
            }
            
            yPosition += 20;
        }

        private async Task RenderPdfChart(XGraphics gfx, JObject section, ref double yPosition)
        {
            var chartType = section["chartType"]?.ToString() ?? "line";
            var width = section["width"]?.Value<int>() ?? 500;
            var height = section["height"]?.Value<int>() ?? 300;
            
            // Create chart using OxyPlot
            var plotModel = new PlotModel
            {
                Title = section["title"]?.ToString()
            };
            
            // Add series based on chart type
            // (simplified - should use actual data)
            var series = new LineSeries();
            series.Points.Add(new DataPoint(0, 0));
            series.Points.Add(new DataPoint(1, 1));
            series.Points.Add(new DataPoint(2, 0.5));
            plotModel.Series.Add(series);
            
            // Export to image
            var exporter = new OxyPlot.Wpf.PngExporter
            {
                Width = width,
                Height = height
            };
            
            using var stream = new MemoryStream();
            exporter.Export(plotModel, stream);
            stream.Position = 0;
            
            using var image = XImage.FromStream(stream);
            gfx.DrawImage(image, 40, yPosition, width, height);
            
            yPosition += height + 20;
            await Task.CompletedTask;
        }

        private void RenderPdfSummary(XGraphics gfx, JObject section, ref double yPosition)
        {
            var items = section["items"] as JArray;
            if (items == null) return;
            
            var font = new XFont("Arial", 11, XFontStyle.Regular);
            var boldFont = new XFont("Arial", 11, XFontStyle.Bold);
            
            foreach (JObject item in items)
            {
                var label = item["label"]?.ToString() ?? "";
                var value = item["value"]?.ToString() ?? "";
                
                gfx.DrawString($"{label}:", boldFont, XBrushes.Black, new XPoint(40, yPosition));
                gfx.DrawString(value, font, XBrushes.Black, new XPoint(150, yPosition));
                yPosition += 20;
            }
            
            yPosition += 10;
        }

        private async Task<byte[]> GenerateExcelReport(ReportDefinition definition)
        {
            using var workbook = new XLWorkbook();
            
            // Process sections
            var sections = definition.Sections.Properties()
                .OrderBy(s => s.Value["order"]?.Value<int>() ?? 0);
            
            foreach (var section in sections)
            {
                var sectionConfig = section.Value as JObject;
                var sheetName = sectionConfig["sheetName"]?.ToString() ?? section.Name;
                var worksheet = workbook.Worksheets.Add(sheetName);
                
                await RenderExcelSection(worksheet, sectionConfig);
            }
            
            // Save to memory stream
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private async Task RenderExcelSection(IXLWorksheet worksheet, JObject section)
        {
            var sectionType = section["type"]?.ToString() ?? "table";
            var row = 1;
            
            switch (sectionType.ToLower())
            {
                case "header":
                    RenderExcelHeader(worksheet, section, ref row);
                    break;
                    
                case "table":
                    RenderExcelTable(worksheet, section, ref row);
                    break;
                    
                case "summary":
                    RenderExcelSummary(worksheet, section, ref row);
                    break;
            }
            
            // Auto-fit columns
            worksheet.Columns().AdjustToContents();
            await Task.CompletedTask;
        }

        private void RenderExcelHeader(IXLWorksheet worksheet, JObject section, ref int row)
        {
            var title = section["title"]?.ToString() ?? "";
            var subtitle = section["subtitle"]?.ToString() ?? "";
            
            // Title
            worksheet.Cell(row, 1).Value = title;
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 18;
            row++;
            
            // Subtitle
            if (!string.IsNullOrEmpty(subtitle))
            {
                worksheet.Cell(row, 1).Value = subtitle;
                worksheet.Cell(row, 1).Style.Font.FontSize = 14;
                row++;
            }
            
            // Date
            worksheet.Cell(row, 1).Value = $"Generated: {DateTime.Now:yyyy/MM/dd HH:mm}";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            row += 2;
        }

        private void RenderExcelTable(IXLWorksheet worksheet, JObject section, ref int row)
        {
            var dataSource = section["dataSource"]?.ToString() ?? "";
            if (!_dataSets.TryGetValue(dataSource, out var dataTable))
                return;
            
            // Headers
            for (int col = 0; col < dataTable.Columns.Count; col++)
            {
                var cell = worksheet.Cell(row, col + 1);
                cell.Value = dataTable.Columns[col].ColumnName;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            row++;
            
            // Data
            foreach (DataRow dataRow in dataTable.Rows)
            {
                for (int col = 0; col < dataTable.Columns.Count; col++)
                {
                    var cell = worksheet.Cell(row, col + 1);
                    cell.Value = dataRow[col]?.ToString() ?? "";
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                row++;
            }
            
            row++;
        }

        private void RenderExcelSummary(IXLWorksheet worksheet, JObject section, ref int row)
        {
            var items = section["items"] as JArray;
            if (items == null) return;
            
            foreach (JObject item in items)
            {
                var label = item["label"]?.ToString() ?? "";
                var value = item["value"]?.ToString() ?? "";
                
                worksheet.Cell(row, 1).Value = label;
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 2).Value = value;
                row++;
            }
            
            row++;
        }

        private async Task<byte[]> GenerateHtmlReport(ReportDefinition definition)
        {
            // Generate HTML report
            var html = "<html><head><style>/* CSS styles */</style></head><body>";
            
            // Add content based on definition
            html += "</body></html>";
            
            return await Task.FromResult(System.Text.Encoding.UTF8.GetBytes(html));
        }

        private async Task<byte[]> GenerateCsvReport(ReportDefinition definition)
        {
            // Generate CSV report
            var csv = "";
            
            // Add data from first data source
            if (_dataSets.Any())
            {
                var dataTable = _dataSets.First().Value;
                
                // Headers
                csv = string.Join(",", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName)) + "\n";
                
                // Rows
                foreach (DataRow row in dataTable.Rows)
                {
                    csv += string.Join(",", row.ItemArray.Select(f => f?.ToString() ?? "")) + "\n";
                }
            }
            
            return await Task.FromResult(System.Text.Encoding.UTF8.GetBytes(csv));
        }

        private async Task<byte[]> GeneratePrintReport(ReportDefinition definition)
        {
            // Generate print-friendly report (similar to PDF)
            return await GeneratePdfReport(definition);
        }
    }
}

// پایان فایل: Core/ReportEngine/ReportBuilder.cs
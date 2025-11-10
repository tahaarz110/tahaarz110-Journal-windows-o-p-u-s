// مسیر فایل: Core/ReportEngine/DynamicReportEngine.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using iTextSharp.text;
using iTextSharp.text.pdf;
using OxyPlot;
using TradingJournal.Core.AnalysisEngine;
using TradingJournal.Core.MetadataEngine.Models;
using TradingJournal.Data.Models;
using TradingJournal.Data.Repositories;

namespace TradingJournal.Core.ReportEngine
{
    public class DynamicReportEngine
    {
        private readonly ITradeRepository _tradeRepository;
        private readonly SmartAnalysisEngine _analysisEngine;
        private readonly Dictionary<string, IReportGenerator> _generators;

        public DynamicReportEngine(ITradeRepository tradeRepository, SmartAnalysisEngine analysisEngine)
        {
            _tradeRepository = tradeRepository;
            _analysisEngine = analysisEngine;
            
            _generators = new Dictionary<string, IReportGenerator>
            {
                ["Performance"] = new PerformanceReportGenerator(),
                ["Analysis"] = new AnalysisReportGenerator(),
                ["Tax"] = new TaxReportGenerator(),
                ["Custom"] = new CustomReportGenerator()
            };
        }

        public async Task<ReportResult> GenerateReport(ReportRequest request)
        {
            try
            {
                // Get generator
                if (!_generators.TryGetValue(request.Type, out var generator))
                    generator = _generators["Custom"];

                // Get data
                var data = await PrepareReportData(request);
                
                // Generate report
                var document = await generator.Generate(request, data);
                
                // Export
                var result = await ExportReport(document, request);
                
                return result;
            }
            catch (Exception ex)
            {
                return new ReportResult
                {
                    Success = false,
                    Message = $"خطا در تولید گزارش: {ex.Message}"
                };
            }
        }

        private async Task<ReportData> PrepareReportData(ReportRequest request)
        {
            var data = new ReportData();
            
            // Get trades
            var trades = await _tradeRepository.GetAllAsync();
            
            // Apply date filter
            if (request.StartDate.HasValue)
                trades = trades.Where(t => t.EntryDate >= request.StartDate.Value);
            
            if (request.EndDate.HasValue)
                trades = trades.Where(t => t.EntryDate <= request.EndDate.Value);
            
            data.Trades = trades.ToList();
            
            // Get analysis
            if (request.IncludeAnalysis)
            {
                data.DayAnalysis = await _analysisEngine.AnalyzeDayOfWeekPerformance();
                data.StrategyAnalysis = await _analysisEngine.AnalyzeStrategies();
                data.SymbolAnalysis = await _analysisEngine.AnalyzeSymbols();
                data.EmotionalAnalysis = await _analysisEngine.AnalyzeEmotions();
            }
            
            // Calculate statistics
            data.Statistics = CalculateStatistics(data.Trades);
            
            return data;
        }

        private Dictionary<string, object> CalculateStatistics(List<Trade> trades)
        {
            var stats = new Dictionary<string, object>
            {
                ["TotalTrades"] = trades.Count,
                ["OpenTrades"] = trades.Count(t => t.IsOpen),
                ["ClosedTrades"] = trades.Count(t => !t.IsOpen),
                ["WinningTrades"] = trades.Count(t => t.IsWin),
                ["LosingTrades"] = trades.Count(t => !t.IsWin && t.Profit.HasValue),
                ["TotalProfit"] = trades.Sum(t => t.Profit ?? 0),
                ["TotalCommission"] = trades.Sum(t => t.Commission ?? 0),
                ["TotalSwap"] = trades.Sum(t => t.Swap ?? 0),
                ["NetProfit"] = trades.Sum(t => t.NetProfit ?? 0),
                ["WinRate"] = CalculateWinRate(trades),
                ["AverageWin"] = trades.Where(t => t.IsWin).DefaultIfEmpty().Average(t => t?.Profit ?? 0),
                ["AverageLoss"] = trades.Where(t => !t.IsWin && t.Profit.HasValue).DefaultIfEmpty().Average(t => t?.Profit ?? 0),
                ["LargestWin"] = trades.Where(t => t.IsWin).DefaultIfEmpty().Max(t => t?.Profit ?? 0),
                ["LargestLoss"] = trades.Where(t => !t.IsWin && t.Profit.HasValue).DefaultIfEmpty().Min(t => t?.Profit ?? 0),
                ["AverageRiskReward"] = trades.Average(t => t.RiskRewardRatio ?? 0),
                ["TotalVolume"] = trades.Sum(t => t.Volume),
                ["ProfitFactor"] = CalculateProfitFactor(trades),
                ["ExpectedValue"] = CalculateExpectedValue(trades),
                ["MaxDrawdown"] = CalculateMaxDrawdown(trades)
            };
            
            return stats;
        }

        private double CalculateWinRate(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            return (double)trades.Count(t => t.IsWin) / trades.Count * 100;
        }

        private double CalculateProfitFactor(List<Trade> trades)
        {
            var totalWins = trades.Where(t => t.IsWin).Sum(t => t.Profit ?? 0);
            var totalLosses = Math.Abs(trades.Where(t => !t.IsWin && t.Profit.HasValue).Sum(t => t.Profit ?? 0));
            
            if (totalLosses == 0) return totalWins > 0 ? double.MaxValue : 0;
            return (double)(totalWins / totalLosses);
        }

        private decimal CalculateExpectedValue(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            
            var winRate = CalculateWinRate(trades) / 100;
            var avgWin = trades.Where(t => t.IsWin).DefaultIfEmpty().Average(t => t?.Profit ?? 0);
            var avgLoss = Math.Abs(trades.Where(t => !t.IsWin && t.Profit.HasValue).DefaultIfEmpty().Average(t => t?.Profit ?? 0));
            
            return (decimal)(winRate * (double)avgWin - (1 - winRate) * (double)avgLoss);
        }

        private decimal CalculateMaxDrawdown(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            
            decimal peak = 0;
            decimal maxDrawdown = 0;
            decimal runningTotal = 0;
            
            foreach (var trade in trades.OrderBy(t => t.EntryDate))
            {
                runningTotal += trade.Profit ?? 0;
                if (runningTotal > peak)
                    peak = runningTotal;
                
                var drawdown = peak - runningTotal;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }
            
            return maxDrawdown;
        }

        private async Task<ReportResult> ExportReport(ReportDocument document, ReportRequest request)
        {
            var result = new ReportResult();
            
            try
            {
                switch (request.Format)
                {
                    case ReportFormat.PDF:
                        result.FilePath = await ExportToPdf(document, request);
                        break;
                    case ReportFormat.Excel:
                        result.FilePath = await ExportToExcel(document, request);
                        break;
                    case ReportFormat.HTML:
                        result.FilePath = await ExportToHtml(document, request);
                        break;
                    case ReportFormat.Word:
                        result.FilePath = await ExportToWord(document, request);
                        break;
                }
                
                result.Success = true;
                result.Message = "گزارش با موفقیت تولید شد";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا در ذخیره گزارش: {ex.Message}";
            }
            
            return result;
        }

        private async Task<string> ExportToPdf(ReportDocument document, ReportRequest request)
        {
            var filePath = GetOutputPath(request, ".pdf");
            
            using (var fs = new FileStream(filePath, FileMode.Create))
            using (var pdfDoc = new iTextSharp.text.Document(PageSize.A4))
            using (var writer = PdfWriter.GetInstance(pdfDoc, fs))
            {
                pdfDoc.Open();
                
                // Add content
                await AddPdfContent(pdfDoc, document);
                
                pdfDoc.Close();
            }
            
            return filePath;
        }

        private async Task AddPdfContent(iTextSharp.text.Document pdfDoc, ReportDocument document)
        {
            // Font setup
            var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "B Nazanin.ttf");
            var baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            var titleFont = new iTextSharp.text.Font(baseFont, 18, iTextSharp.text.Font.BOLD);
            var headerFont = new iTextSharp.text.Font(baseFont, 14, iTextSharp.text.Font.BOLD);
            var normalFont = new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.NORMAL);
            
            // Title
            var title = new iTextSharp.text.Paragraph(document.Title, titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            pdfDoc.Add(title);
            
            // Metadata
            var metadata = new iTextSharp.text.Paragraph($"تاریخ: {document.GeneratedDate:yyyy/MM/dd}\n", normalFont)
            {
                Alignment = Element.ALIGN_RIGHT
            };
            pdfDoc.Add(metadata);
            
            // Sections
            foreach (var section in document.Sections)
            {
                // Section header
                var sectionHeader = new iTextSharp.text.Paragraph(section.Title, headerFont)
                {
                    SpacingBefore = 20,
                    SpacingAfter = 10
                };
                pdfDoc.Add(sectionHeader);
                
                // Section content
                foreach (var element in section.Elements)
                {
                    switch (element.Type)
                    {
                        case ReportElementType.Text:
                            var text = new iTextSharp.text.Paragraph(element.Content.ToString(), normalFont);
                            pdfDoc.Add(text);
                            break;
                            
                        case ReportElementType.Table:
                            if (element.Content is DataTable dataTable)
                            {
                                var table = CreatePdfTable(dataTable, normalFont);
                                pdfDoc.Add(table);
                            }
                            break;
                            
                        case ReportElementType.Chart:
                            if (element.Content is PlotModel plotModel)
                            {
                                var image = ConvertChartToImage(plotModel);
                                pdfDoc.Add(image);
                            }
                            break;
                    }
                }
            }
            
            await Task.CompletedTask;
        }

        private PdfPTable CreatePdfTable(DataTable dataTable, iTextSharp.text.Font font)
        {
            var table = new PdfPTable(dataTable.Columns.Count)
            {
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                WidthPercentage = 100,
                SpacingBefore = 10,
                SpacingAfter = 10
            };
            
            // Headers
            foreach (DataColumn column in dataTable.Columns)
            {
                var cell = new PdfPCell(new Phrase(column.ColumnName, font))
                {
                    BackgroundColor = BaseColor.LIGHT_GRAY,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    HorizontalAlignment = Element.ALIGN_CENTER
                };
                table.AddCell(cell);
            }
            
            // Data
            foreach (DataRow row in dataTable.Rows)
            {
                foreach (var item in row.ItemArray)
                {
                    var cell = new PdfPCell(new Phrase(item?.ToString() ?? "", font))
                    {
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL
                    };
                    table.AddCell(cell);
                }
            }
            
            return table;
        }

        private iTextSharp.text.Image ConvertChartToImage(PlotModel plotModel)
        {
            // Convert OxyPlot to image
            var pngExporter = new OxyPlot.Wpf.PngExporter
            {
                Width = 600,
                Height = 400,
                Background = OxyColors.White
            };
            
            using (var stream = new MemoryStream())
            {
                pngExporter.Export(plotModel, stream);
                var image = iTextSharp.text.Image.GetInstance(stream.ToArray());
                image.ScaleToFit(500, 300);
                image.Alignment = Element.ALIGN_CENTER;
                return image;
            }
        }

        private async Task<string> ExportToExcel(ReportDocument document, ReportRequest request)
        {
            var filePath = GetOutputPath(request, ".xlsx");
            
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                foreach (var section in document.Sections)
                {
                    var worksheet = workbook.Worksheets.Add(section.Title);
                    int row = 1;
                    
                    // Section title
                    worksheet.Cell(row, 1).Value = section.Title;
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 1).Style.Font.FontSize = 14;
                    row += 2;
                    
                    // Elements
                    foreach (var element in section.Elements)
                    {
                        if (element.Type == ReportElementType.Table && element.Content is DataTable dataTable)
                        {
                            worksheet.Cell(row, 1).InsertTable(dataTable);
                            row += dataTable.Rows.Count + 3;
                        }
                        else if (element.Type == ReportElementType.Text)
                        {
                            worksheet.Cell(row, 1).Value = element.Content.ToString();
                            row += 2;
                        }
                    }
                    
                    worksheet.Columns().AdjustToContents();
                }
                
                workbook.SaveAs(filePath);
            }
            
            return filePath;
        }

        private async Task<string> ExportToHtml(ReportDocument document, ReportRequest request)
        {
            var filePath = GetOutputPath(request, ".html");
            
            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html dir='rtl' lang='fa'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine($"<title>{document.Title}</title>");
            html.AppendLine("<style>");
            html.AppendLine(@"
                body { font-family: 'B Nazanin', Tahoma; direction: rtl; margin: 20px; }
                h1 { text-align: center; color: #333; }
                h2 { color: #555; border-bottom: 2px solid #ddd; padding-bottom: 5px; }
                table { width: 100%; border-collapse: collapse; margin: 20px 0; }
                th, td { border: 1px solid #ddd; padding: 8px; text-align: right; }
                th { background-color: #f2f2f2; font-weight: bold; }
                .chart { text-align: center; margin: 20px 0; }
            ");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            html.AppendLine($"<h1>{document.Title}</h1>");
            html.AppendLine($"<p>تاریخ تولید: {document.GeneratedDate:yyyy/MM/dd HH:mm}</p>");
            
            foreach (var section in document.Sections)
            {
                html.AppendLine($"<h2>{section.Title}</h2>");
                
                foreach (var element in section.Elements)
                {
                    switch (element.Type)
                    {
                        case ReportElementType.Text:
                            html.AppendLine($"<p>{element.Content}</p>");
                            break;
                            
                        case ReportElementType.Table:
                            if (element.Content is DataTable dataTable)
                            {
                                html.AppendLine(ConvertDataTableToHtml(dataTable));
                            }
                            break;
                            
                        case ReportElementType.Chart:
                            html.AppendLine("<div class='chart'>[نمودار]</div>");
                            break;
                    }
                }
            }
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            await File.WriteAllTextAsync(filePath, html.ToString());
            return filePath;
        }

        private string ConvertDataTableToHtml(DataTable dataTable)
        {
            var html = new System.Text.StringBuilder();
            html.AppendLine("<table>");
            
            // Headers
            html.AppendLine("<thead><tr>");
            foreach (DataColumn column in dataTable.Columns)
            {
                html.AppendLine($"<th>{column.ColumnName}</th>");
            }
            html.AppendLine("</tr></thead>");
            
            // Data
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
            return html.ToString();
        }

        private async Task<string> ExportToWord(ReportDocument document, ReportRequest request)
        {
            // Implement Word export using a library like DocX
            var filePath = GetOutputPath(request, ".docx");
            
            // Placeholder implementation
            await File.WriteAllTextAsync(filePath.Replace(".docx", ".txt"), document.ToString());
            
            return filePath;
        }

        private string GetOutputPath(ReportRequest request, string extension)
        {
            var fileName = $"{request.Name}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
            var path = Path.Combine(request.OutputPath ?? "Reports", fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }
    }

    public interface IReportGenerator
    {
        Task<ReportDocument> Generate(ReportRequest request, ReportData data);
    }

    public class PerformanceReportGenerator : IReportGenerator
    {
        public async Task<ReportDocument> Generate(ReportRequest request, ReportData data)
        {
            var document = new ReportDocument
            {
                Title = "گزارش عملکرد معاملاتی",
                GeneratedDate = DateTime.Now
            };

            // Summary section
            var summarySection = new ReportSection { Title = "خلاصه عملکرد" };
            
            // Statistics table
            var statsTable = CreateStatisticsTable(data.Statistics);
            summarySection.Elements.Add(new ReportElement
            {
                Type = ReportElementType.Table,
                Content = statsTable
            });
            
            document.Sections.Add(summarySection);

            // Monthly performance section
            var monthlySection = new ReportSection { Title = "عملکرد ماهانه" };
            var monthlyTable = CreateMonthlyPerformanceTable(data.Trades);
            monthlySection.Elements.Add(new ReportElement
            {
                Type = ReportElementType.Table,
                Content = monthlyTable
            });
            
            document.Sections.Add(monthlySection);

            // Strategy performance section
            if (data.StrategyAnalysis != null)
            {
                var strategySection = new ReportSection { Title = "عملکرد استراتژی‌ها" };
                var strategyTable = CreateStrategyTable(data.StrategyAnalysis);
                strategySection.Elements.Add(new ReportElement
                {
                    Type = ReportElementType.Table,
                    Content = strategyTable
                });
                
                document.Sections.Add(strategySection);
            }

            return document;
        }

        private DataTable CreateStatisticsTable(Dictionary<string, object> statistics)
        {
            var table = new DataTable();
            table.Columns.Add("شاخص");
            table.Columns.Add("مقدار");

            foreach (var stat in statistics)
            {
                var row = table.NewRow();
                row["شاخص"] = GetPersianLabel(stat.Key);
                row["مقدار"] = FormatValue(stat.Value);
                table.Rows.Add(row);
            }

            return table;
        }

        private DataTable CreateMonthlyPerformanceTable(List<Trade> trades)
        {
            var table = new DataTable();
            table.Columns.Add("ماه");
            table.Columns.Add("تعداد معاملات");
            table.Columns.Add("سود/زیان");
            table.Columns.Add("نرخ برد");

            var monthlyGroups = trades
                .GroupBy(t => new { t.EntryDate.Year, t.EntryDate.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month);

            foreach (var group in monthlyGroups)
            {
                var row = table.NewRow();
                row["ماه"] = $"{group.Key.Year}/{group.Key.Month:D2}";
                row["تعداد معاملات"] = group.Count();
                row["سود/زیان"] = group.Sum(t => t.Profit ?? 0).ToString("N0");
                row["نرخ برد"] = $"{CalculateWinRate(group.ToList()):F1}%";
                table.Rows.Add(row);
            }

            return table;
        }

        private DataTable CreateStrategyTable(StrategyAnalysis analysis)
        {
            var table = new DataTable();
            table.Columns.Add("استراتژی");
            table.Columns.Add("تعداد");
            table.Columns.Add("سود کل");
            table.Columns.Add("نرخ برد");
            table.Columns.Add("Profit Factor");

            foreach (var strategy in analysis.StrategyStatistics)
            {
                var row = table.NewRow();
                row["استراتژی"] = strategy.Key;
                row["تعداد"] = strategy.Value.TotalTrades;
                row["سود کل"] = strategy.Value.TotalProfit.ToString("N0");
                row["نرخ برد"] = $"{strategy.Value.WinRate:F1}%";
                row["Profit Factor"] = strategy.Value.ProfitFactor.ToString("F2");
                table.Rows.Add(row);
            }

            return table;
        }

        private double CalculateWinRate(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            return (double)trades.Count(t => t.IsWin) / trades.Count * 100;
        }

        private string GetPersianLabel(string key)
        {
            var labels = new Dictionary<string, string>
            {
                ["TotalTrades"] = "کل معاملات",
                ["WinningTrades"] = "معاملات موفق",
                ["LosingTrades"] = "معاملات ناموفق",
                ["TotalProfit"] = "سود/زیان کل",
                ["WinRate"] = "نرخ برد",
                ["ProfitFactor"] = "نسبت سود",
                ["MaxDrawdown"] = "حداکثر افت",
                ["ExpectedValue"] = "ارزش مورد انتظار"
            };

            return labels.GetValueOrDefault(key, key);
        }

        private string FormatValue(object value)
        {
            return value switch
            {
                decimal d => d.ToString("N0"),
                double d => d.ToString("F2"),
                int i => i.ToString(),
                _ => value?.ToString() ?? ""
            };
        }
    }

    public class AnalysisReportGenerator : IReportGenerator
    {
        public async Task<ReportDocument> Generate(ReportRequest request, ReportData data)
        {
            var document = new ReportDocument
            {
                Title = "گزارش تحلیل معاملات",
                GeneratedDate = DateTime.Now
            };

            // Add analysis sections
            if (data.DayAnalysis != null)
            {
                var section = new ReportSection { Title = "تحلیل روزانه" };
                // Add day analysis content
                document.Sections.Add(section);
            }

            return document;
        }
    }

    public class TaxReportGenerator : IReportGenerator
    {
        public async Task<ReportDocument> Generate(ReportRequest request, ReportData data)
        {
            var document = new ReportDocument
            {
                Title = "گزارش مالیاتی",
                GeneratedDate = DateTime.Now
            };

            // Calculate tax information
            var taxSection = new ReportSection { Title = "اطلاعات مالیاتی" };
            
            var totalProfit = data.Trades.Sum(t => t.Profit ?? 0);
            var totalCommission = data.Trades.Sum(t => t.Commission ?? 0);
            var netProfit = totalProfit - totalCommission;

            taxSection.Elements.Add(new ReportElement
            {
                Type = ReportElementType.Text,
                Content = $"سود خالص: {netProfit:N0}"
            });

            document.Sections.Add(taxSection);
            return document;
        }
    }

    public class CustomReportGenerator : IReportGenerator
    {
        public async Task<ReportDocument> Generate(ReportRequest request, ReportData data)
        {
            var document = new ReportDocument
            {
                Title = request.Title ?? "گزارش سفارشی",
                GeneratedDate = DateTime.Now
            };

            // Generate custom report based on request configuration
            return document;
        }
    }

    // Report Models
    public class ReportRequest
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public ReportFormat Format { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IncludeAnalysis { get; set; }
        public bool IncludeCharts { get; set; }
        public string OutputPath { get; set; }
        public Dictionary<string, object> CustomParameters { get; set; }
    }

    public enum ReportFormat
    {
        PDF,
        Excel,
        HTML,
        Word
    }

    public class ReportDocument
    {
        public string Title { get; set; }
        public DateTime GeneratedDate { get; set; }
        public List<ReportSection> Sections { get; set; } = new List<ReportSection>();
    }

    public class ReportSection
    {
        public string Title { get; set; }
        public List<ReportElement> Elements { get; set; } = new List<ReportElement>();
    }

    public class ReportElement
    {
        public ReportElementType Type { get; set; }
        public object Content { get; set; }
    }

    public enum ReportElementType
    {
        Text,
        Table,
        Chart,
        Image
    }

    public class ReportData
    {
        public List<Trade> Trades { get; set; }
        public Dictionary<string, object> Statistics { get; set; }
        public DayOfWeekAnalysis DayAnalysis { get; set; }
        public StrategyAnalysis StrategyAnalysis { get; set; }
        public SymbolAnalysis SymbolAnalysis { get; set; }
        public EmotionalAnalysis EmotionalAnalysis { get; set; }
    }

    public class ReportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }
    }
}
// پایان کد
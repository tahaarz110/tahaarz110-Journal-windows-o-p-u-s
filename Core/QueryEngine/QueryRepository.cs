// مسیر فایل: Core/QueryEngine/QueryRepository.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TradingJournal.Core.QueryEngine.Models;

namespace TradingJournal.Core.QueryEngine
{
    public class QueryRepository
    {
        private readonly string _queriesPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public QueryRepository()
        {
            _queriesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Metadata", "Queries");
            Directory.CreateDirectory(_queriesPath);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        public async Task<QueryModel> SaveQueryAsync(QueryModel query)
        {
            if (string.IsNullOrEmpty(query.Name))
                throw new ArgumentException("نام query نمی‌تواند خالی باشد");

            query.IsSaved = true;
            
            if (query.CreatedDate == default)
                query.CreatedDate = DateTime.Now;
            else
                query.ModifiedDate = DateTime.Now;

            var fileName = $"{SanitizeFileName(query.Name)}.json";
            var filePath = Path.Combine(_queriesPath, fileName);

            var json = JsonSerializer.Serialize(query, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            return query;
        }

        public async Task<QueryModel> LoadQueryAsync(string name)
        {
            var fileName = $"{SanitizeFileName(name)}.json";
            var filePath = Path.Combine(_queriesPath, fileName);

            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<QueryModel>(json, _jsonOptions);
        }

        public async Task<List<QueryModel>> GetAllQueriesAsync()
        {
            var queries = new List<QueryModel>();
            var files = Directory.GetFiles(_queriesPath, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var query = JsonSerializer.Deserialize<QueryModel>(json, _jsonOptions);
                    if (query != null)
                        queries.Add(query);
                }
                catch
                {
                    // در صورت خطا در خواندن فایل، آن را نادیده بگیر
                }
            }

            return queries.OrderBy(q => q.Name).ToList();
        }

        public async Task<bool> DeleteQueryAsync(string name)
        {
            var fileName = $"{SanitizeFileName(name)}.json";
            var filePath = Path.Combine(_queriesPath, fileName);

            if (!File.Exists(filePath))
                return false;

            File.Delete(filePath);
            return true;
        }

        public async Task<QueryModel> DuplicateQueryAsync(string originalName, string newName)
        {
            var original = await LoadQueryAsync(originalName);
            if (original == null)
                return null;

            var duplicate = JsonSerializer.Deserialize<QueryModel>(
                JsonSerializer.Serialize(original, _jsonOptions), _jsonOptions);

            duplicate.Name = newName;
            duplicate.CreatedDate = DateTime.Now;
            duplicate.ModifiedDate = null;

            return await SaveQueryAsync(duplicate);
        }

        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        public async Task<List<string>> GetQueryNamesAsync()
        {
            var files = Directory.GetFiles(_queriesPath, "*.json");
            var names = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Name", out var nameElement))
                    {
                        names.Add(nameElement.GetString());
                    }
                }
                catch
                {
                    // نادیده گرفتن فایل‌های معیوب
                }
            }

            return names.OrderBy(n => n).ToList();
        }

        public async Task<bool> ExportQueryAsync(string name, string exportPath)
        {
            var query = await LoadQueryAsync(name);
            if (query == null)
                return false;

            var json = JsonSerializer.Serialize(query, _jsonOptions);
            await File.WriteAllTextAsync(exportPath, json);
            return true;
        }

        public async Task<QueryModel> ImportQueryAsync(string importPath)
        {
            if (!File.Exists(importPath))
                return null;

            var json = await File.ReadAllTextAsync(importPath);
            var query = JsonSerializer.Deserialize<QueryModel>(json, _jsonOptions);
            
            if (query != null)
            {
                // بررسی نام تکراری
                var existingNames = await GetQueryNamesAsync();
                var baseName = query.Name;
                var counter = 1;
                
                while (existingNames.Contains(query.Name))
                {
                    query.Name = $"{baseName} ({counter++})";
                }

                return await SaveQueryAsync(query);
            }

            return null;
        }
    }
}
// پایان کد
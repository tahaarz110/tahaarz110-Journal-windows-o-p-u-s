// ابتدای فایل: Core/MetadataEngine/MetadataEngine.cs - بخش 1
// مسیر: /Core/MetadataEngine/MetadataEngine.cs

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using Serilog;
using TradingJournal.Core.Configuration;

namespace TradingJournal.Core.MetadataEngine
{
    public class MetadataEngine
    {
        private readonly string _metadataPath;
        private readonly Dictionary<string, JObject> _metadataCache;
        private readonly Dictionary<string, JsonSchema> _schemaCache;

        public MetadataEngine()
        {
            _metadataPath = AppSettings.Instance.MetadataPath;
            _metadataCache = new Dictionary<string, JObject>();
            _schemaCache = new Dictionary<string, JsonSchema>();
            
            Initialize();
        }

        private void Initialize()
        {
            // Create metadata directories
            Directory.CreateDirectory(_metadataPath);
            Directory.CreateDirectory(Path.Combine(_metadataPath, "forms"));
            Directory.CreateDirectory(Path.Combine(_metadataPath, "tabs"));
            Directory.CreateDirectory(Path.Combine(_metadataPath, "widgets"));
            Directory.CreateDirectory(Path.Combine(_metadataPath, "schemas"));
            Directory.CreateDirectory(Path.Combine(_metadataPath, "queries"));
            Directory.CreateDirectory(Path.Combine(_metadataPath, "reports"));
            
            // Load default schemas
            LoadDefaultSchemas();
            
            // Load all metadata files
            LoadMetadataFiles();
        }

        private void LoadDefaultSchemas()
        {
            // Field Schema
            var fieldSchema = new
            {
                type = "object",
                properties = new
                {
                    fieldName = new { type = "string", minLength = 1 },
                    displayName = new { type = "string" },
                    displayNameFa = new { type = "string" },
                    fieldType = new { type = "string", @enum = new[] { "text", "number", "decimal", "date", "dropdown", "checkbox" } },
                    required = new { type = "boolean" },
                    visible = new { type = "boolean" },
                    editable = new { type = "boolean" },
                    defaultValue = new { type = "string" },
                    validation = new { type = "object" },
                    options = new { type = "array" }
                },
                required = new[] { "fieldName", "fieldType" }
            };
            
            SaveSchema("field", fieldSchema);
            
            // Form Schema
            var formSchema = new
            {
                type = "object",
                properties = new
                {
                    formId = new { type = "string" },
                    formName = new { type = "string" },
                    formNameFa = new { type = "string" },
                    groups = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                groupName = new { type = "string" },
                                groupNameFa = new { type = "string" },
                                columns = new { type = "integer", minimum = 1, maximum = 4 },
                                fields = new { type = "array" }
                            }
                        }
                    }
                },
                required = new[] { "formId", "formName" }
            };
            
            SaveSchema("form", formSchema);
            
            // Widget Schema
            var widgetSchema = new
            {
                type = "object",
                properties = new
                {
                    widgetId = new { type = "string" },
                    widgetName = new { type = "string" },
                    widgetType = new { type = "string", @enum = new[] { "table", "chart", "card", "kpi", "custom" } },
                    dataSource = new { type = "object" },
                    configuration = new { type = "object" },
                    refreshInterval = new { type = "integer" }
                },
                required = new[] { "widgetId", "widgetName", "widgetType" }
            };
            
            SaveSchema("widget", widgetSchema);
        }

        private void SaveSchema(string schemaName, object schemaObject)
        {
            var json = JsonConvert.SerializeObject(schemaObject, Formatting.Indented);
            var path = Path.Combine(_metadataPath, "schemas", $"{schemaName}.schema.json");
            File.WriteAllText(path, json);
            
            _schemaCache[schemaName] = JsonSchema.FromJsonAsync(json).Result;
        }

        private void LoadMetadataFiles()
        {
            try
            {
                // Load forms
                LoadMetadataFromDirectory("forms");
                
                // Load tabs
                LoadMetadataFromDirectory("tabs");
                
                // Load widgets
                LoadMetadataFromDirectory("widgets");
                
                // Load queries
                LoadMetadataFromDirectory("queries");
                
                // Load reports
                LoadMetadataFromDirectory("reports");
                
                Log.Information($"Loaded {_metadataCache.Count} metadata files");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading metadata files");
            }
        }

        private void LoadMetadataFromDirectory(string subdirectory)
        {
            var path = Path.Combine(_metadataPath, subdirectory);
            if (!Directory.Exists(path)) return;
            
            var files = Directory.GetFiles(path, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var metadata = JObject.Parse(json);
                    var key = $"{subdirectory}/{Path.GetFileNameWithoutExtension(file)}";
                    _metadataCache[key] = metadata;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error loading metadata file: {file}");
                }
            }
        }
// ابتدای بخش 2 فایل: Core/MetadataEngine/MetadataEngine.cs
// ادامه از بخش 1

        public async Task<JObject?> GetMetadataAsync(string key)
        {
            if (_metadataCache.TryGetValue(key, out var metadata))
            {
                return metadata;
            }
            
            // Try to load from file
            var parts = key.Split('/');
            if (parts.Length == 2)
            {
                var path = Path.Combine(_metadataPath, parts[0], $"{parts[1]}.json");
                if (File.Exists(path))
                {
                    var json = await File.ReadAllTextAsync(path);
                    metadata = JObject.Parse(json);
                    _metadataCache[key] = metadata;
                    return metadata;
                }
            }
            
            return null;
        }

        public async Task<bool> SaveMetadataAsync(string key, JObject metadata)
        {
            try
            {
                // Validate against schema if available
                var schemaName = key.Split('/')[0].TrimEnd('s'); // Remove plural 's'
                if (_schemaCache.TryGetValue(schemaName, out var schema))
                {
                    var errors = schema.Validate(metadata.ToString());
                    if (errors.Any())
                    {
                        Log.Error($"Metadata validation failed for {key}: {string.Join(", ", errors.Select(e => e.ToString()))}");
                        return false;
                    }
                }
                
                // Save to cache
                _metadataCache[key] = metadata;
                
                // Save to file
                var parts = key.Split('/');
                if (parts.Length == 2)
                {
                    var path = Path.Combine(_metadataPath, parts[0], $"{parts[1]}.json");
                    await File.WriteAllTextAsync(path, metadata.ToString(Formatting.Indented));
                    
                    Log.Information($"Metadata saved: {key}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error saving metadata: {key}");
                return false;
            }
        }

        public async Task<bool> DeleteMetadataAsync(string key)
        {
            try
            {
                // Remove from cache
                _metadataCache.Remove(key);
                
                // Delete file
                var parts = key.Split('/');
                if (parts.Length == 2)
                {
                    var path = Path.Combine(_metadataPath, parts[0], $"{parts[1]}.json");
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        Log.Information($"Metadata deleted: {key}");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error deleting metadata: {key}");
                return false;
            }
        }

        public async Task<List<string>> GetMetadataKeysAsync(string category)
        {
            var keys = new List<string>();
            var path = Path.Combine(_metadataPath, category);
            
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.json");
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    keys.Add($"{category}/{name}");
                }
            }
            
            return await Task.FromResult(keys);
        }

        public async Task<dynamic> CreateDynamicObjectAsync(JObject metadata)
        {
            dynamic obj = new ExpandoObject();
            var dict = (IDictionary<string, object>)obj;
            
            foreach (var prop in metadata.Properties())
            {
                dict[prop.Name] = ConvertJTokenToObject(prop.Value);
            }
            
            return await Task.FromResult(obj);
        }

        private object? ConvertJTokenToObject(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    return CreateDynamicObjectAsync(token as JObject).Result;
                case JTokenType.Array:
                    return token.Select(ConvertJTokenToObject).ToList();
                case JTokenType.Integer:
                    return token.Value<long>();
                case JTokenType.Float:
                    return token.Value<double>();
                case JTokenType.String:
                    return token.Value<string>();
                case JTokenType.Boolean:
                    return token.Value<bool>();
                case JTokenType.Null:
                    return null;
                case JTokenType.Date:
                    return token.Value<DateTime>();
                default:
                    return token.ToString();
            }
        }

        public async Task<bool> ExportAllMetadataAsync(string exportPath)
        {
            try
            {
                var exportData = new JObject
                {
                    ["version"] = "1.0.0",
                    ["exportDate"] = DateTime.Now,
                    ["metadata"] = new JObject()
                };
                
                foreach (var kvp in _metadataCache)
                {
                    exportData["metadata"][kvp.Key] = kvp.Value;
                }
                
                await File.WriteAllTextAsync(exportPath, exportData.ToString(Formatting.Indented));
                
                Log.Information($"Metadata exported to: {exportPath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting metadata");
                return false;
            }
        }

        public async Task<bool> ImportMetadataAsync(string importPath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(importPath);
                var importData = JObject.Parse(json);
                
                if (importData["metadata"] is JObject metadata)
                {
                    foreach (var prop in metadata.Properties())
                    {
                        if (prop.Value is JObject metaObj)
                        {
                            await SaveMetadataAsync(prop.Name, metaObj);
                        }
                    }
                }
                
                Log.Information($"Metadata imported from: {importPath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error importing metadata");
                return false;
            }
        }
    }
}

// پایان فایل: Core/MetadataEngine/MetadataEngine.cs
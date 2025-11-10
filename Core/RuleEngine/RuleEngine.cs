// ابتدای فایل: Core/RuleEngine/RuleEngine.cs
// مسیر: /Core/RuleEngine/RuleEngine.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamicExpresso;
using Newtonsoft.Json.Linq;
using Serilog;
using TradingJournal.Data;
using TradingJournal.Data.Models;

namespace TradingJournal.Core.RuleEngine
{
    public enum RuleType
    {
        Validation,
        Calculation,
        Trigger,
        Notification,
        Workflow
    }

    public enum RuleCondition
    {
        Always,
        OnCreate,
        OnUpdate,
        OnDelete,
        OnFieldChange,
        OnSchedule,
        Custom
    }

    public class Rule
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString();
        public string RuleName { get; set; } = string.Empty;
        public string RuleNameFa { get; set; } = string.Empty;
        public RuleType Type { get; set; }
        public RuleCondition Condition { get; set; }
        public string? TargetEntity { get; set; }
        public string? TargetField { get; set; }
        public string Expression { get; set; } = string.Empty;
        public Dictionary<string, object> Actions { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public int Priority { get; set; } = 0;
        public string? ErrorMessage { get; set; }
        public string? ErrorMessageFa { get; set; }
    }

    public class RuleEngine
    {
        private readonly Interpreter _interpreter;
        private readonly DatabaseContext _dbContext;
        private readonly Dictionary<string, List<Rule>> _rules;
        private readonly Dictionary<string, Func<object, Task<object>>> _customFunctions;

        public RuleEngine()
        {
            _interpreter = new Interpreter();
            _dbContext = new DatabaseContext();
            _rules = new Dictionary<string, List<Rule>>();
            _customFunctions = new Dictionary<string, Func<object, Task<object>>>();
            
            RegisterBuiltInFunctions();
            LoadRulesFromMetadata();
        }

        private void RegisterBuiltInFunctions()
        {
            // Math functions
            _interpreter.SetFunction("abs", (Func<double, double>)Math.Abs);
            _interpreter.SetFunction("round", (Func<double, int, double>)Math.Round);
            _interpreter.SetFunction("floor", (Func<double, double>)Math.Floor);
            _interpreter.SetFunction("ceiling", (Func<double, double>)Math.Ceiling);
            _interpreter.SetFunction("min", (Func<double, double, double>)Math.Min);
            _interpreter.SetFunction("max", (Func<double, double, double>)Math.Max);
            _interpreter.SetFunction("pow", (Func<double, double, double>)Math.Pow);
            _interpreter.SetFunction("sqrt", (Func<double, double>)Math.Sqrt);
            
            // String functions
            _interpreter.SetFunction("len", (Func<string, int>)(s => s?.Length ?? 0));
            _interpreter.SetFunction("upper", (Func<string, string>)(s => s?.ToUpper() ?? ""));
            _interpreter.SetFunction("lower", (Func<string, string>)(s => s?.ToLower() ?? ""));
            _interpreter.SetFunction("trim", (Func<string, string>)(s => s?.Trim() ?? ""));
            _interpreter.SetFunction("contains", (Func<string, string, bool>)((s, sub) => s?.Contains(sub) ?? false));
            
            // Date functions
            _interpreter.SetFunction("now", (Func<DateTime>)(() => DateTime.Now));
            _interpreter.SetFunction("today", (Func<DateTime>)(() => DateTime.Today));
            _interpreter.SetFunction("daysAgo", (Func<int, DateTime>)(days => DateTime.Now.AddDays(-days)));
            _interpreter.SetFunction("monthsAgo", (Func<int, DateTime>)(months => DateTime.Now.AddMonths(-months)));
            
            // Trading specific functions
            _interpreter.SetFunction("pipValue", (Func<string, double, double>)CalculatePipValue);
            _interpreter.SetFunction("lotSize", (Func<double, double, double, double>)CalculateLotSize);
            _interpreter.SetFunction("riskReward", (Func<double, double, double, string>)CalculateRiskReward);
            _interpreter.SetFunction("winRate", (Func<int, int, double>)CalculateWinRate);
        }

        private void LoadRulesFromMetadata()
        {
            try
            {
                // Load rules from JSON files or database
                var rulesPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TradingJournal",
                    "Metadata",
                    "rules"
                );

                if (System.IO.Directory.Exists(rulesPath))
                {
                    var ruleFiles = System.IO.Directory.GetFiles(rulesPath, "*.json");
                    foreach (var file in ruleFiles)
                    {
                        var json = System.IO.File.ReadAllText(file);
                        var ruleData = JObject.Parse(json);
                        var rule = ParseRule(ruleData);
                        AddRule(rule);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading rules from metadata");
            }
        }

        private Rule ParseRule(JObject ruleData)
        {
            return new Rule
            {
                RuleId = ruleData["ruleId"]?.ToString() ?? Guid.NewGuid().ToString(),
                RuleName = ruleData["ruleName"]?.ToString() ?? "",
                RuleNameFa = ruleData["ruleNameFa"]?.ToString() ?? "",
                Type = Enum.Parse<RuleType>(ruleData["type"]?.ToString() ?? "Validation"),
                Condition = Enum.Parse<RuleCondition>(ruleData["condition"]?.ToString() ?? "Always"),
                TargetEntity = ruleData["targetEntity"]?.ToString(),
                TargetField = ruleData["targetField"]?.ToString(),
                Expression = ruleData["expression"]?.ToString() ?? "",
                Actions = ruleData["actions"]?.ToObject<Dictionary<string, object>>() ?? new(),
                IsActive = ruleData["isActive"]?.Value<bool>() ?? true,
                Priority = ruleData["priority"]?.Value<int>() ?? 0,
                ErrorMessage = ruleData["errorMessage"]?.ToString(),
                ErrorMessageFa = ruleData["errorMessageFa"]?.ToString()
            };
        }

        public void AddRule(Rule rule)
        {
            var key = $"{rule.TargetEntity ?? "global"}:{rule.Type}";
            
            if (!_rules.ContainsKey(key))
            {
                _rules[key] = new List<Rule>();
            }
            
            _rules[key].Add(rule);
            
            // Sort by priority
            _rules[key] = _rules[key].OrderBy(r => r.Priority).ToList();
        }

        public async Task<RuleResult> ExecuteRulesAsync(string entity, object data, RuleType? type = null)
        {
            var result = new RuleResult { Success = true };
            var keys = new List<string>();
            
            if (type.HasValue)
            {
                keys.Add($"{entity}:{type}");
                keys.Add($"global:{type}");
            }
            else
            {
                foreach (RuleType ruleType in Enum.GetValues(typeof(RuleType)))
                {
                    keys.Add($"{entity}:{ruleType}");
                    keys.Add($"global:{ruleType}");
                }
            }

            foreach (var key in keys)
            {
                if (_rules.TryGetValue(key, out var rules))
                {
                    foreach (var rule in rules.Where(r => r.IsActive))
                    {
                        var ruleResult = await ExecuteRuleAsync(rule, data);
                        
                        if (!ruleResult.Success)
                        {
                            result.Success = false;
                            result.Errors.AddRange(ruleResult.Errors);
                            
                            if (rule.Type == RuleType.Validation)
                            {
                                // Stop on validation failure
                                return result;
                            }
                        }
                        
                        result.Results.AddRange(ruleResult.Results);
                    }
                }
            }

            return result;
        }

        private async Task<RuleResult> ExecuteRuleAsync(Rule rule, object data)
        {
            var result = new RuleResult { Success = true };
            
            try
            {
                // Set up interpreter context
                _interpreter.SetVariable("data", data);
                _interpreter.SetVariable("entity", data);
                
                // Add properties as variables
                if (data != null)
                {
                    var properties = data.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(data);
                        _interpreter.SetVariable(prop.Name.ToLower(), value);
                    }
                }

                // Evaluate expression
                var expressionResult = _interpreter.Eval(rule.Expression);
                
                // Handle result based on rule type
                switch (rule.Type)
                {
                    case RuleType.Validation:
                        if (!(expressionResult is bool isValid) || !isValid)
                        {
                            result.Success = false;
                            result.Errors.Add(new RuleError
                            {
                                RuleId = rule.RuleId,
                                Field = rule.TargetField,
                                Message = rule.ErrorMessageFa ?? rule.ErrorMessage ?? "Validation failed"
                            });
                        }
                        break;
                        
                    case RuleType.Calculation:
                        if (rule.TargetField != null && data != null)
                        {
                            var prop = data.GetType().GetProperty(rule.TargetField);
                            if (prop != null && prop.CanWrite)
                            {
                                prop.SetValue(data, Convert.ChangeType(expressionResult, prop.PropertyType));
                                result.Results.Add(new RuleActionResult
                                {
                                    Action = "SetField",
                                    Field = rule.TargetField,
                                    Value = expressionResult
                                });
                            }
                        }
                        break;
                        
                    case RuleType.Trigger:
                        if (expressionResult is bool shouldTrigger && shouldTrigger)
                        {
                            await ExecuteActionsAsync(rule, data, result);
                        }
                        break;
                        
                    case RuleType.Notification:
                        if (expressionResult is bool shouldNotify && shouldNotify)
                        {
                            result.Results.Add(new RuleActionResult
                            {
                                Action = "Notification",
                                Value = rule.Actions
                            });
                        }
                        break;
                        
                    case RuleType.Workflow:
                        await ExecuteWorkflowAsync(rule, data, result);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error executing rule: {rule.RuleName}");
                result.Success = false;
                result.Errors.Add(new RuleError
                {
                    RuleId = rule.RuleId,
                    Message = $"Rule execution error: {ex.Message}"
                });
            }

            return result;
        }

        private async Task ExecuteActionsAsync(Rule rule, object data, RuleResult result)
        {
            foreach (var action in rule.Actions)
            {
                try
                {
                    switch (action.Key.ToLower())
                    {
                        case "setfield":
                            if (action.Value is JObject fieldData)
                            {
                                var fieldName = fieldData["field"]?.ToString();
                                var value = fieldData["value"];
                                
                                if (fieldName != null && data != null)
                                {
                                    var prop = data.GetType().GetProperty(fieldName);
                                    if (prop != null && prop.CanWrite)
                                    {
                                        prop.SetValue(data, Convert.ChangeType(value, prop.PropertyType));
                                    }
                                }
                            }
                            break;
                            
                        case "calculate":
                            if (action.Value is string formula)
                            {
                                var calcResult = _interpreter.Eval(formula);
                                result.Results.Add(new RuleActionResult
                                {
                                    Action = "Calculate",
                                    Value = calcResult
                                });
                            }
                            break;
                            
                        case "notify":
                            result.Results.Add(new RuleActionResult
                            {
                                Action = "Notify",
                                Value = action.Value
                            });
                            break;
                            
                        case "log":
                            Log.Information($"Rule {rule.RuleName}: {action.Value}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error executing action {action.Key} in rule {rule.RuleName}");
                }
            }
            
            await Task.CompletedTask;
        }

        private async Task ExecuteWorkflowAsync(Rule rule, object data, RuleResult result)
        {
            // Execute workflow steps in sequence
            var steps = rule.Actions.GetValueOrDefault("steps") as JArray;
            if (steps != null)
            {
                foreach (JObject step in steps)
                {
                    var stepType = step["type"]?.ToString();
                    var condition = step["condition"]?.ToString();
                    
                    // Check condition if exists
                    if (!string.IsNullOrEmpty(condition))
                    {
                        var shouldExecute = _interpreter.Eval<bool>(condition);
                        if (!shouldExecute) continue;
                    }
                    
                    // Execute step based on type
                    switch (stepType?.ToLower())
                    {
                        case "validate":
                            // Execute validation
                            break;
                        case "calculate":
                            // Execute calculation
                            break;
                        case "save":
                            // Save to database
                            break;
                        case "notify":
                            // Send notification
                            break;
                    }
                }
            }
            
            await Task.CompletedTask;
        }

        // Trading calculation functions
        private double CalculatePipValue(string symbol, double lotSize)
        {
            // Simplified pip value calculation
            var pipValue = symbol.Contains("JPY") ? 0.01 : 0.0001;
            return pipValue * lotSize * 100000; // Standard lot = 100,000 units
        }

        private double CalculateLotSize(double balance, double riskPercent, double stopLossPips)
        {
            if (stopLossPips == 0) return 0.01;
            
            var riskAmount = balance * (riskPercent / 100);
            var lotSize = riskAmount / (stopLossPips * 10); // $10 per pip for standard lot
            
            return Math.Max(0.01, Math.Round(lotSize, 2));
        }

        private string CalculateRiskReward(double stopLoss, double takeProfit, double entryPrice)
        {
            var risk = Math.Abs(entryPrice - stopLoss);
            var reward = Math.Abs(takeProfit - entryPrice);
            
            if (risk == 0) return "N/A";
            
            var ratio = reward / risk;
            return $"1:{ratio:F2}";
        }

        private double CalculateWinRate(int wins, int losses)
        {
            var total = wins + losses;
            if (total == 0) return 0;
            
            return (double)wins / total * 100;
        }
    }

    public class RuleResult
    {
        public bool Success { get; set; }
        public List<RuleError> Errors { get; set; } = new();
        public List<RuleActionResult> Results { get; set; } = new();
    }

    public class RuleError
    {
        public string RuleId { get; set; } = string.Empty;
        public string? Field { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RuleActionResult
    {
        public string Action { get; set; } = string.Empty;
        public string? Field { get; set; }
        public object? Value { get; set; }
    }
}

// پایان فایل: Core/RuleEngine/RuleEngine.cs
// ابتدای فایل: Core/ExpressionEngine/ExpressionEngine.cs
// مسیر: /Core/ExpressionEngine/ExpressionEngine.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DynamicExpresso;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TradingJournal.Data;
using TradingJournal.Data.Models;

namespace TradingJournal.Core.ExpressionEngine
{
    public class ExpressionEngine
    {
        private readonly Interpreter _interpreter;
        private readonly DatabaseContext _dbContext;
        private readonly Dictionary<string, object> _globalVariables;
        private readonly Dictionary<string, Delegate> _customFunctions;

        public ExpressionEngine()
        {
            _interpreter = new Interpreter();
            _dbContext = new DatabaseContext();
            _globalVariables = new Dictionary<string, object>();
            _customFunctions = new Dictionary<string, Delegate>();
            
            Initialize();
        }

        private void Initialize()
        {
            // Register standard types
            _interpreter.Reference(typeof(Math));
            _interpreter.Reference(typeof(DateTime));
            _interpreter.Reference(typeof(TimeSpan));
            _interpreter.Reference(typeof(String));
            _interpreter.Reference(typeof(Convert));
            
            // Register custom functions
            RegisterTradingFunctions();
            RegisterStatisticalFunctions();
            RegisterDateFunctions();
            RegisterStringFunctions();
            RegisterConditionalFunctions();
            
            // Set global variables
            SetGlobalVariables();
        }

        private void RegisterTradingFunctions()
        {
            // Price calculations
            _interpreter.SetFunction("pips", new Func<double, double, string, double>((price1, price2, symbol) =>
            {
                var multiplier = symbol.Contains("JPY") ? 100 : 10000;
                return Math.Abs(price1 - price2) * multiplier;
            }));

            _interpreter.SetFunction("pipValue", new Func<string, double, double>((symbol, lotSize) =>
            {
                var pointValue = symbol.Contains("JPY") ? 0.01 : 0.0001;
                return pointValue * lotSize * 100000;
            }));

            _interpreter.SetFunction("profitLoss", new Func<double, double, double, string, double>(
                (entryPrice, exitPrice, volume, direction) =>
            {
                var diff = exitPrice - entryPrice;
                if (direction.ToLower() == "sell") diff = -diff;
                return diff * volume * 100000;
            }));

            _interpreter.SetFunction("margin", new Func<double, double, double, double>(
                (volume, price, leverage) => (volume * 100000 * price) / leverage
            ));

            _interpreter.SetFunction("riskAmount", new Func<double, double, double>(
                (balance, riskPercent) => balance * (riskPercent / 100)
            ));

            _interpreter.SetFunction("positionSize", new Func<double, double, double, double>(
                (riskAmount, stopLossPips, pipValue) =>
            {
                if (stopLossPips == 0 || pipValue == 0) return 0.01;
                return Math.Round(riskAmount / (stopLossPips * pipValue), 2);
            }));

            _interpreter.SetFunction("rr", new Func<double, double, double>(
                (reward, risk) => risk == 0 ? 0 : Math.Round(reward / risk, 2)
            ));

            _interpreter.SetFunction("expectancy", new Func<double, double, double, double>(
                (winRate, avgWin, avgLoss) =>
            {
                var lossRate = 1 - winRate;
                return (winRate * avgWin) - (lossRate * avgLoss);
            }));
        }

        private void RegisterStatisticalFunctions()
        {
            _interpreter.SetFunction("avg", new Func<double[], double>(values =>
                values.Length == 0 ? 0 : values.Average()
            ));

            _interpreter.SetFunction("sum", new Func<double[], double>(values => values.Sum()));

            _interpreter.SetFunction("count", new Func<object[], int>(values => values.Length));

            _interpreter.SetFunction("min", new Func<double[], double>(values =>
                values.Length == 0 ? 0 : values.Min()
            ));

            _interpreter.SetFunction("max", new Func<double[], double>(values =>
                values.Length == 0 ? 0 : values.Max()
            ));

            _interpreter.SetFunction("median", new Func<double[], double>(values =>
            {
                if (values.Length == 0) return 0;
                var sorted = values.OrderBy(v => v).ToArray();
                var mid = sorted.Length / 2;
                return sorted.Length % 2 == 0 
                    ? (sorted[mid - 1] + sorted[mid]) / 2 
                    : sorted[mid];
            }));

            _interpreter.SetFunction("stddev", new Func<double[], double>(values =>
            {
                if (values.Length == 0) return 0;
                var avg = values.Average();
                var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
                return Math.Sqrt(sumOfSquares / values.Length);
            }));

            _interpreter.SetFunction("variance", new Func<double[], double>(values =>
            {
                if (values.Length == 0) return 0;
                var avg = values.Average();
                return values.Sum(v => Math.Pow(v - avg, 2)) / values.Length;
            }));

            _interpreter.SetFunction("percentile", new Func<double[], double, double>((values, percentile) =>
            {
                if (values.Length == 0) return 0;
                var sorted = values.OrderBy(v => v).ToArray();
                var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
                return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
            }));
        }

        private void RegisterDateFunctions()
        {
            _interpreter.SetFunction("now", new Func<DateTime>(() => DateTime.Now));
            
            _interpreter.SetFunction("today", new Func<DateTime>(() => DateTime.Today));
            
            _interpreter.SetFunction("date", new Func<int, int, int, DateTime>(
                (year, month, day) => new DateTime(year, month, day)
            ));

            _interpreter.SetFunction("addDays", new Func<DateTime, int, DateTime>(
                (date, days) => date.AddDays(days)
            ));

            _interpreter.SetFunction("addMonths", new Func<DateTime, int, DateTime>(
                (date, months) => date.AddMonths(months)
            ));

            _interpreter.SetFunction("daysBetween", new Func<DateTime, DateTime, int>(
                (date1, date2) => (int)(date2 - date1).TotalDays
            ));

            _interpreter.SetFunction("weekday", new Func<DateTime, string>(
                date => date.DayOfWeek.ToString()
            ));

            _interpreter.SetFunction("month", new Func<DateTime, int>(date => date.Month));
            
            _interpreter.SetFunction("year", new Func<DateTime, int>(date => date.Year));
            
            _interpreter.SetFunction("quarter", new Func<DateTime, int>(
                date => (date.Month - 1) / 3 + 1
            ));

            _interpreter.SetFunction("isWeekend", new Func<DateTime, bool>(
                date => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
            ));
        }

        private void RegisterStringFunctions()
        {
            _interpreter.SetFunction("concat", new Func<string[], string>(
                values => string.Join("", values)
            ));

            _interpreter.SetFunction("join", new Func<string, string[], string>(
                (separator, values) => string.Join(separator, values)
            ));

            _interpreter.SetFunction("split", new Func<string, string, string[]>(
                (text, separator) => text.Split(new[] { separator }, StringSplitOptions.None)
            ));

            _interpreter.SetFunction("replace", new Func<string, string, string, string>(
                (text, oldValue, newValue) => text.Replace(oldValue, newValue)
            ));

            _interpreter.SetFunction("regex", new Func<string, string, bool>(
                (text, pattern) => Regex.IsMatch(text, pattern)
            ));

            _interpreter.SetFunction("format", new Func<string, object[], string>(
                (format, args) => string.Format(format, args)
            ));
        }

        private void RegisterConditionalFunctions()
        {
            _interpreter.SetFunction("if", new Func<bool, object, object, object>(
                (condition, trueValue, falseValue) => condition ? trueValue : falseValue
            ));

            _interpreter.SetFunction("switch", new Func<object, Dictionary<object, object>, object, object>(
                (value, cases, defaultValue) =>
                {
                    return cases.ContainsKey(value) ? cases[value] : defaultValue;
                }
            ));

            _interpreter.SetFunction("isNull", new Func<object, bool>(
                value => value == null
            ));

            _interpreter.SetFunction("isNotNull", new Func<object, bool>(
                value => value != null
            ));

            _interpreter.SetFunction("coalesce", new Func<object[], object>(
                values => values.FirstOrDefault(v => v != null)
            ));

            _interpreter.SetFunction("between", new Func<double, double, double, bool>(
                (value, min, max) => value >= min && value <= max
            ));
        }

        private void SetGlobalVariables()
        {
            _globalVariables["PI"] = Math.PI;
            _globalVariables["E"] = Math.E;
            
            // Trading constants
            _globalVariables["STANDARD_LOT"] = 100000;
            _globalVariables["MINI_LOT"] = 10000;
            _globalVariables["MICRO_LOT"] = 1000;
            
            // Update global variables in interpreter
            foreach (var kvp in _globalVariables)
            {
                _interpreter.SetVariable(kvp.Key, kvp.Value);
            }
        }

        public async Task<object> EvaluateAsync(string expression, Dictionary<string, object>? context = null)
        {
            try
            {
                // Set context variables
                if (context != null)
                {
                    foreach (var kvp in context)
                    {
                        _interpreter.SetVariable(kvp.Key, kvp.Value);
                    }
                }
                
                // Parse and evaluate expression
                var result = _interpreter.Eval(expression);
                
                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error evaluating expression: {expression}");
                throw new ExpressionException($"Expression evaluation failed: {ex.Message}", ex);
            }
        }

        public T Evaluate<T>(string expression, Dictionary<string, object>? context = null)
        {
            var result = EvaluateAsync(expression, context).Result;
            return (T)Convert.ChangeType(result, typeof(T));
        }

        public bool Validate(string expression)
        {
            try
            {
                var lambda = _interpreter.Parse(expression);
                return lambda != null;
            }
            catch
            {
                return false;
            }
        }

        public string[] GetVariables(string expression)
        {
            var pattern = @"\b[a-zA-Z_][a-zA-Z0-9_]*\b";
            var matches = Regex.Matches(expression, pattern);
            
            var variables = new HashSet<string>();
            foreach (Match match in matches)
            {
                var variable = match.Value;
                
                // Skip functions and keywords
                if (!IsFunction(variable) && !IsKeyword(variable))
                {
                    variables.Add(variable);
                }
            }
            
            return variables.ToArray();
        }

        private bool IsFunction(string name)
        {
            return _customFunctions.ContainsKey(name) || 
                   _interpreter.Parse($"{name}()") != null;
        }

        private bool IsKeyword(string name)
        {
            var keywords = new[] { "if", "else", "true", "false", "null", "new", "typeof", "and", "or", "not" };
            return keywords.Contains(name.ToLower());
        }

        public void RegisterFunction(string name, Delegate function)
        {
            _customFunctions[name] = function;
            _interpreter.SetFunction(name, function);
        }

        public void SetVariable(string name, object value)
        {
            _globalVariables[name] = value;
            _interpreter.SetVariable(name, value);
        }
    }

    public class ExpressionException : Exception
    {
        public ExpressionException(string message) : base(message) { }
        public ExpressionException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}

// پایان فایل: Core/ExpressionEngine/ExpressionEngine.cs
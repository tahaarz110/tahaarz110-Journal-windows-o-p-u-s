// ابتدای فایل: Core/ValidationEngine/ValidationEngine.cs
// مسیر: /Core/ValidationEngine/ValidationEngine.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Newtonsoft.Json.Linq;
using Serilog;
using TradingJournal.Data.Models;

namespace TradingJournal.Core.ValidationEngine
{
    public class ValidationRule
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString();
        public string RuleName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string ValidationType { get; set; } = string.Empty; // Required, Range, Pattern, Custom
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorMessageFa { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class ValidationContext
    {
        public object Entity { get; set; } = null!;
        public string EntityType { get; set; } = string.Empty;
        public Dictionary<string, object> AdditionalData { get; set; } = new();
        public List<ValidationRule> Rules { get; set; } = new();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public TimeSpan ValidationTime { get; set; }
    }

    public class ValidationError
    {
        public string FieldName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string Severity { get; set; } = "Error"; // Error, Warning, Info
        public object? AttemptedValue { get; set; }
    }

    public interface IValidationEngine
    {
        Task<ValidationResult> ValidateAsync(object entity, string entityType);
        Task<ValidationResult> ValidateAsync(ValidationContext context);
        Task<bool> ValidateFieldAsync(object entity, string fieldName, object? value);
        void RegisterValidator(string entityType, IValidator validator);
        void RegisterRule(ValidationRule rule);
        List<ValidationRule> GetRules(string entityType);
    }

    public class ValidationEngine : IValidationEngine
    {
        private readonly Dictionary<string, IValidator> _validators;
        private readonly Dictionary<string, List<ValidationRule>> _rules;
        private readonly TradeValidator _tradeValidator;

        public ValidationEngine()
        {
            _validators = new Dictionary<string, IValidator>();
            _rules = new Dictionary<string, List<ValidationRule>>();
            _tradeValidator = new TradeValidator();
            
            RegisterDefaultValidators();
            LoadValidationRules();
        }

        private void RegisterDefaultValidators()
        {
            RegisterValidator("Trade", _tradeValidator);
            RegisterValidator("DynamicField", new DynamicFieldValidator());
            RegisterValidator("TabConfiguration", new TabConfigurationValidator());
            RegisterValidator("WidgetConfiguration", new WidgetConfigurationValidator());
        }

        private void LoadValidationRules()
        {
            // Load from metadata or database
            var tradeRules = new List<ValidationRule>
            {
                new ValidationRule
                {
                    RuleName = "Symbol Required",
                    FieldName = "Symbol",
                    ValidationType = "Required",
                    ErrorMessage = "Symbol is required",
                    ErrorMessageFa = "نماد اجباری است",
                    Priority = 1
                },
                new ValidationRule
                {
                    RuleName = "Entry Price Range",
                    FieldName = "EntryPrice",
                    ValidationType = "Range",
                    Parameters = new Dictionary<string, object> { ["min"] = 0, ["max"] = 999999 },
                    ErrorMessage = "Entry price must be between 0 and 999999",
                    ErrorMessageFa = "قیمت ورود باید بین 0 و 999999 باشد",
                    Priority = 2
                },
                new ValidationRule
                {
                    RuleName = "Volume Range",
                    FieldName = "Volume",
                    ValidationType = "Range",
                    Parameters = new Dictionary<string, object> { ["min"] = 0.01, ["max"] = 100 },
                    ErrorMessage = "Volume must be between 0.01 and 100",
                    ErrorMessageFa = "حجم باید بین 0.01 و 100 باشد",
                    Priority = 3
                },
                new ValidationRule
                {
                    RuleName = "Risk Percentage",
                    FieldName = "RiskPercent",
                    ValidationType = "Range",
                    Parameters = new Dictionary<string, object> { ["min"] = 0, ["max"] = 10 },
                    ErrorMessage = "Risk percentage should not exceed 10%",
                    ErrorMessageFa = "درصد ریسک نباید بیشتر از 10% باشد",
                    Priority = 4
                }
            };

            _rules["Trade"] = tradeRules;
        }

        public async Task<ValidationResult> ValidateAsync(object entity, string entityType)
        {
            var context = new ValidationContext
            {
                Entity = entity,
                EntityType = entityType,
                Rules = GetRules(entityType)
            };

            return await ValidateAsync(context);
        }

        public async Task<ValidationResult> ValidateAsync(ValidationContext context)
        {
            var startTime = DateTime.Now;
            var result = new ValidationResult { IsValid = true };

            try
            {
                // FluentValidation
                if (_validators.TryGetValue(context.EntityType, out var validator))
                {
                    var fluentResult = await validator.ValidateAsync(
                        new FluentValidation.ValidationContext<object>(context.Entity)
                    );

                    if (!fluentResult.IsValid)
                    {
                        result.IsValid = false;
                        foreach (var error in fluentResult.Errors)
                        {
                            result.Errors.Add(new ValidationError
                            {
                                FieldName = error.PropertyName,
                                ErrorMessage = error.ErrorMessage,
                                ErrorCode = error.ErrorCode,
                                AttemptedValue = error.AttemptedValue,
                                Severity = error.Severity.ToString()
                            });
                        }
                    }
                }

                // Custom validation rules
                foreach (var rule in context.Rules.Where(r => r.IsActive).OrderBy(r => r.Priority))
                {
                    var validationError = await ValidateRuleAsync(context.Entity, rule);
                    if (validationError != null)
                    {
                        result.IsValid = false;
                        result.Errors.Add(validationError);
                    }
                }

                // Business logic validation
                var businessErrors = await ValidateBusinessLogicAsync(context);
                if (businessErrors.Any())
                {
                    result.IsValid = false;
                    result.Errors.AddRange(businessErrors);
                }

                result.ValidationTime = DateTime.Now - startTime;
                
                if (!result.IsValid)
                {
                    Log.Warning($"Validation failed for {context.EntityType} with {result.Errors.Count} errors");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error during validation of {context.EntityType}");
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    ErrorMessage = "خطا در اعتبارسنجی",
                    ErrorCode = "VALIDATION_ERROR"
                });
            }

            return result;
        }

        private async Task<ValidationError?> ValidateRuleAsync(object entity, ValidationRule rule)
        {
            try
            {
                var property = entity.GetType().GetProperty(rule.FieldName);
                if (property == null)
                    return null;

                var value = property.GetValue(entity);

                switch (rule.ValidationType.ToLower())
                {
                    case "required":
                        if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
                        {
                            return new ValidationError
                            {
                                FieldName = rule.FieldName,
                                ErrorMessage = rule.ErrorMessageFa ?? rule.ErrorMessage,
                                ErrorCode = "REQUIRED",
                                AttemptedValue = value
                            };
                        }
                        break;

                    case "range":
                        if (value != null && rule.Parameters.TryGetValue("min", out var min) && 
                            rule.Parameters.TryGetValue("max", out var max))
                        {
                            var numValue = Convert.ToDouble(value);
                            var minValue = Convert.ToDouble(min);
                            var maxValue = Convert.ToDouble(max);

                            if (numValue < minValue || numValue > maxValue)
                            {
                                return new ValidationError
                                {
                                    FieldName = rule.FieldName,
                                    ErrorMessage = rule.ErrorMessageFa ?? rule.ErrorMessage,
                                    ErrorCode = "RANGE",
                                    AttemptedValue = value
                                };
                            }
                        }
                        break;

                    case "pattern":
                        if (value is string strValue && rule.Parameters.TryGetValue("pattern", out var pattern))
                        {
                            var regex = new Regex(pattern.ToString()!);
                            if (!regex.IsMatch(strValue))
                            {
                                return new ValidationError
                                {
                                    FieldName = rule.FieldName,
                                    ErrorMessage = rule.ErrorMessageFa ?? rule.ErrorMessage,
                                    ErrorCode = "PATTERN",
                                    AttemptedValue = value
                                };
                            }
                        }
                        break;

                    case "custom":
                        if (rule.Parameters.TryGetValue("expression", out var expression))
                        {
                            // Evaluate custom expression
                            // Implementation depends on expression engine
                        }
                        break;
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error validating rule {rule.RuleName}");
                return null;
            }
        }

        private async Task<List<ValidationError>> ValidateBusinessLogicAsync(ValidationContext context)
        {
            var errors = new List<ValidationError>();

            if (context.Entity is Trade trade)
            {
                // Exit date must be after entry date
                if (trade.ExitDate.HasValue && trade.ExitDate < trade.EntryDate)
                {
                    errors.Add(new ValidationError
                    {
                        FieldName = "ExitDate",
                        ErrorMessage = "تاریخ خروج باید بعد از تاریخ ورود باشد",
                        ErrorCode = "INVALID_DATE_RANGE"
                    });
                }

                // Stop loss validation for direction
                if (trade.StopLoss.HasValue)
                {
                    if (trade.Direction == TradeDirection.Buy && trade.StopLoss >= trade.EntryPrice)
                    {
                        errors.Add(new ValidationError
                        {
                            FieldName = "StopLoss",
                            ErrorMessage = "حد ضرر برای معامله خرید باید کمتر از قیمت ورود باشد",
                            ErrorCode = "INVALID_STOP_LOSS"
                        });
                    }
                    else if (trade.Direction == TradeDirection.Sell && trade.StopLoss <= trade.EntryPrice)
                    {
                        errors.Add(new ValidationError
                        {
                            FieldName = "StopLoss",
                            ErrorMessage = "حد ضرر برای معامله فروش باید بیشتر از قیمت ورود باشد",
                            ErrorCode = "INVALID_STOP_LOSS"
                        });
                    }
                }

                // Risk management validation
                if (trade.RiskPercent.HasValue && trade.RiskPercent > 5)
                {
                    errors.Add(new ValidationError
                    {
                        FieldName = "RiskPercent",
                        ErrorMessage = "هشدار: ریسک بیش از 5% توصیه نمی‌شود",
                        ErrorCode = "HIGH_RISK",
                        Severity = "Warning"
                    });
                }
            }

            return await Task.FromResult(errors);
        }

        public async Task<bool> ValidateFieldAsync(object entity, string fieldName, object? value)
        {
            var property = entity.GetType().GetProperty(fieldName);
            if (property == null)
                return false;

            // Temporarily set the value
            var originalValue = property.GetValue(entity);
            property.SetValue(entity, value);

            // Get rules for this field
            var entityType = entity.GetType().Name;
            var fieldRules = GetRules(entityType)
                .Where(r => r.FieldName == fieldName && r.IsActive)
                .ToList();

            var isValid = true;
            foreach (var rule in fieldRules)
            {
                var error = await ValidateRuleAsync(entity, rule);
                if (error != null)
                {
                    isValid = false;
                    break;
                }
            }

            // Restore original value
            property.SetValue(entity, originalValue);

            return isValid;
        }

        public void RegisterValidator(string entityType, IValidator validator)
        {
            _validators[entityType] = validator;
        }

        public void RegisterRule(ValidationRule rule)
        {
            var entityType = rule.FieldName.Split('.')[0];
            
            if (!_rules.ContainsKey(entityType))
            {
                _rules[entityType] = new List<ValidationRule>();
            }

            _rules[entityType].Add(rule);
        }

        public List<ValidationRule> GetRules(string entityType)
        {
            return _rules.TryGetValue(entityType, out var rules) 
                ? rules 
                : new List<ValidationRule>();
        }
    }

    // FluentValidation Validators
    public class TradeValidator : AbstractValidator<Trade>
    {
        public TradeValidator()
        {
            RuleFor(t => t.Symbol)
                .NotEmpty().WithMessage("نماد اجباری است");

            RuleFor(t => t.EntryPrice)
                .GreaterThan(0).WithMessage("قیمت ورود باید بزرگتر از صفر باشد");

            RuleFor(t => t.Volume)
                .InclusiveBetween(0.01m, 100m).WithMessage("حجم باید بین 0.01 و 100 باشد");

            RuleFor(t => t.EntryDate)
                .NotEmpty().WithMessage("تاریخ ورود اجباری است");

            When(t => t.ExitDate.HasValue, () =>
            {
                RuleFor(t => t.ExitPrice)
                    .NotNull().WithMessage("قیمت خروج برای معامله بسته شده اجباری است")
                    .GreaterThan(0).WithMessage("قیمت خروج باید بزرگتر از صفر باشد");
            });

            When(t => t.RiskPercent.HasValue, () =>
            {
                RuleFor(t => t.RiskPercent)
                    .InclusiveBetween(0, 100).WithMessage("درصد ریسک باید بین 0 و 100 باشد");
            });
        }
    }

    public class DynamicFieldValidator : AbstractValidator<DynamicField>
    {
        public DynamicFieldValidator()
        {
            RuleFor(f => f.FieldName)
                .NotEmpty().WithMessage("نام فیلد اجباری است")
                .Matches("^[a-zA-Z][a-zA-Z0-9_]*$").WithMessage("نام فیلد باید با حرف شروع شود");

            RuleFor(f => f.DisplayName)
                .NotEmpty().WithMessage("نام نمایشی اجباری است");

            RuleFor(f => f.FieldType)
                .IsInEnum().WithMessage("نوع فیلد نامعتبر است");

            RuleFor(f => f.OrderIndex)
                .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش باید بزرگتر یا مساوی صفر باشد");
        }
    }

    public class TabConfigurationValidator : AbstractValidator<TabConfiguration>
    {
        public TabConfigurationValidator()
        {
            RuleFor(t => t.TabKey)
                .NotEmpty().WithMessage("کلید تب اجباری است")
                .Matches("^[a-z][a-z0-9_-]*$").WithMessage("کلید تب باید با حرف کوچک شروع شود");

            RuleFor(t => t.TabName)
                .NotEmpty().WithMessage("نام تب اجباری است");

            RuleFor(t => t.OrderIndex)
                .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش باید بزرگتر یا مساوی صفر باشد");
        }
    }

    public class WidgetConfigurationValidator : AbstractValidator<WidgetConfiguration>
    {
        public WidgetConfigurationValidator()
        {
            RuleFor(w => w.WidgetKey)
                .NotEmpty().WithMessage("کلید ویجت اجباری است");

            RuleFor(w => w.WidgetName)
                .NotEmpty().WithMessage("نام ویجت اجباری است");

            RuleFor(w => w.Row)
                .GreaterThanOrEqualTo(0).WithMessage("ردیف باید بزرگتر یا مساوی صفر باشد");

            RuleFor(w => w.Column)
                .GreaterThanOrEqualTo(0).WithMessage("ستون باید بزرگتر یا مساوی صفر باشد");

            RuleFor(w => w.RowSpan)
                .GreaterThan(0).WithMessage("تعداد ردیف باید بزرگتر از صفر باشد");

            RuleFor(w => w.ColumnSpan)
                .GreaterThan(0).WithMessage("تعداد ستون باید بزرگتر از صفر باشد");
        }
    }
}

// پایان فایل: Core/ValidationEngine/ValidationEngine.cs
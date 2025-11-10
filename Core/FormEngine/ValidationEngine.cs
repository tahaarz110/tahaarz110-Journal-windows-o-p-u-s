// مسیر فایل: Core/FormEngine/ValidationEngine.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TradingJournal.Core.FormEngine.Models;

namespace TradingJournal.Core.FormEngine
{
    public class ValidationEngine
    {
        private readonly Dictionary<string, IValidationRule> _rules;

        public ValidationEngine()
        {
            _rules = new Dictionary<string, IValidationRule>
            {
                ["required"] = new RequiredRule(),
                ["email"] = new EmailRule(),
                ["phone"] = new PhoneRule(),
                ["min"] = new MinRule(),
                ["max"] = new MaxRule(),
                ["regex"] = new RegexRule(),
                ["range"] = new RangeRule(),
                ["custom"] = new CustomRule()
            };
        }

        public ValidationResult ValidateForm(List<DynamicField> fields)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var field in fields)
            {
                var fieldResult = ValidateField(field);
                if (!fieldResult.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(fieldResult.Errors);
                }
            }

            return result;
        }

        public ValidationResult ValidateField(DynamicField field)
        {
            var result = new ValidationResult { IsValid = true };

            // بررسی فیلد اجباری
            if (field.IsRequired && !_rules["required"].Validate(field))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    FieldName = field.Name,
                    Message = $"فیلد {field.Label} اجباری است"
                });
            }

            // اعمال قوانین validation
            if (!string.IsNullOrEmpty(field.ValidationRule))
            {
                var rules = ParseValidationRules(field.ValidationRule);
                foreach (var rule in rules)
                {
                    if (_rules.ContainsKey(rule.Key))
                    {
                        if (!_rules[rule.Key].Validate(field, rule.Value))
                        {
                            result.IsValid = false;
                            result.Errors.Add(new ValidationError
                            {
                                FieldName = field.Name,
                                Message = GetErrorMessage(rule.Key, field.Label, rule.Value)
                            });
                        }
                    }
                }
            }

            return result;
        }

        private Dictionary<string, string> ParseValidationRules(string ruleString)
        {
            var rules = new Dictionary<string, string>();
            var parts = ruleString.Split('|');

            foreach (var part in parts)
            {
                var ruleParts = part.Split(':');
                if (ruleParts.Length == 2)
                {
                    rules[ruleParts[0]] = ruleParts[1];
                }
                else if (ruleParts.Length == 1)
                {
                    rules[ruleParts[0]] = "";
                }
            }

            return rules;
        }

        private string GetErrorMessage(string rule, string fieldLabel, string parameter)
        {
            return rule switch
            {
                "email" => $"فیلد {fieldLabel} باید یک ایمیل معتبر باشد",
                "phone" => $"فیلد {fieldLabel} باید یک شماره تلفن معتبر باشد",
                "min" => $"فیلد {fieldLabel} باید حداقل {parameter} کاراکتر باشد",
                "max" => $"فیلد {fieldLabel} نباید بیشتر از {parameter} کاراکتر باشد",
                "range" => $"فیلد {fieldLabel} باید بین {parameter} باشد",
                _ => $"فیلد {fieldLabel} معتبر نیست"
            };
        }

        public void RegisterRule(string name, IValidationRule rule)
        {
            _rules[name] = rule;
        }
    }

    public interface IValidationRule
    {
        bool Validate(DynamicField field, string parameter = null);
    }

    public class RequiredRule : IValidationRule
    {
        public bool Validate(DynamicField field, string parameter = null)
        {
            return field.Value != null && !string.IsNullOrWhiteSpace(field.Value.ToString());
        }
    }

    public class EmailRule : IValidationRule
    {
        public bool Validate(DynamicField field, string parameter = null)
        {
            if (field.Value == null) return true;
            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(field.Value.ToString(), pattern);
        }
    }

    public class PhoneRule : IValidationRule
    {
        public bool Validate(DynamicField field, string parameter = null)
        {
            if (field.Value == null) return true;
            var pattern = @"^(\+98|0)?9\d{9}$";
            return Regex.IsMatch(field.Value.ToString(), pattern);
        }
    }

    public class MinRule : IValidationRule
    {
        public bool Validate(DynamicField field, string parameter = null)
        {
            if (field.Value == null) return true;
            if (int.TryParse(parameter, out int min))
            {
                return field.Value.ToString().Length >= min;
            }
            return true;
        }
    }

    public class MaxRule : IValidationRule
    {
        public bool Validate(DynamicField field, string parameter = null)
        {
            if (field.Value == null) return true;
            if (int.TryParse(parameter, out int max))
            {
                return field.Value.ToString().Length <= max;
            }
            return true;
        }
    }

    public class RangeRule : IValidationRule
    {
        public bool Validate(DynamicField field, string parameter = null)
        {
            if (field.Value == null) return true;
            var parts = parameter.Split(',');
            if (parts.Length == 2 && 
                double.TryParse(parts[0], out double min) && 
                double.TryParse(parts[1], out double max) &&
                double.TryParse(field.Value.ToString(), out double value))
            {
                return value >= min && value <= max;
            }
            return true;
        }
    }

    public class RegexRule : IValidationRule
    {
        public bool Validate(DynamicField field, string parameter = null)
        {
            if (field.Value == null || string.IsNullOrEmpty(parameter)) return true;
            return Regex.IsMatch(field.Value.ToString(), parameter);
        }
    }

    public class CustomRule : IValidationRule
    {
        public bool Validate(DynamicField field, string parameter = null)
        {
            // این قانون می‌تواند برای اجرای منطق سفارشی استفاده شود
            return true;
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
    }

    public class ValidationError
    {
        public string FieldName { get; set; }
        public string Message { get; set; }
    }
}
// پایان کد
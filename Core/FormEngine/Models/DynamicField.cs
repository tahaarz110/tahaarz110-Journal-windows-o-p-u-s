// مسیر فایل: Core/FormEngine/Models/DynamicField.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TradingJournal.Core.FormEngine.Models
{
    public class DynamicField : INotifyPropertyChanged
    {
        private string _name;
        private string _label;
        private FieldType _fieldType;
        private object _value;
        private bool _isRequired;
        private bool _isReadOnly;
        private string _validationRule;
        private Dictionary<string, object> _metadata;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(); }
        }

        public FieldType FieldType
        {
            get => _fieldType;
            set { _fieldType = value; OnPropertyChanged(); }
        }

        public object Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public bool IsRequired
        {
            get => _isRequired;
            set { _isRequired = value; OnPropertyChanged(); }
        }

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set { _isReadOnly = value; OnPropertyChanged(); }
        }

        public string ValidationRule
        {
            get => _validationRule;
            set { _validationRule = value; OnPropertyChanged(); }
        }

        public Dictionary<string, object> Metadata
        {
            get => _metadata ??= new Dictionary<string, object>();
            set { _metadata = value; OnPropertyChanged(); }
        }

        // برای ComboBox و RadioButton
        public List<FieldOption> Options { get; set; } = new List<FieldOption>();

        // برای فیلدهای عددی
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }

        // برای فیلدهای متنی
        public int? MaxLength { get; set; }
        public string Placeholder { get; set; }

        // برای گروه‌بندی
        public string GroupName { get; set; }
        public int Order { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum FieldType
    {
        Text,
        Number,
        Decimal,
        Date,
        DateTime,
        CheckBox,
        ComboBox,
        RadioButton,
        TextArea,
        Image,
        File,
        Color,
        Currency,
        Percentage
    }

    public class FieldOption
    {
        public string Value { get; set; }
        public string Display { get; set; }
        public bool IsDefault { get; set; }
    }
}
// پایان کد
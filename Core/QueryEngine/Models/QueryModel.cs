// مسیر فایل: Core/QueryEngine/Models/QueryModel.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TradingJournal.Core.QueryEngine.Models
{
    public class QueryModel : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private List<QueryField> _selectedFields;
        private QueryFilter _rootFilter;
        private List<SortField> _sortFields;
        private int _pageSize = 50;
        private int _currentPage = 1;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public List<QueryField> SelectedFields
        {
            get => _selectedFields ??= new List<QueryField>();
            set { _selectedFields = value; OnPropertyChanged(); }
        }

        public QueryFilter RootFilter
        {
            get => _rootFilter ??= new QueryFilter { Logic = FilterLogic.AND };
            set { _rootFilter = value; OnPropertyChanged(); }
        }

        public List<SortField> SortFields
        {
            get => _sortFields ??= new List<SortField>();
            set { _sortFields = value; OnPropertyChanged(); }
        }

        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(); }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

        public bool IsSaved { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class QueryField
    {
        public string FieldName { get; set; }
        public string DisplayName { get; set; }
        public FieldDataType DataType { get; set; }
        public bool IsSelected { get; set; }
        public string Aggregation { get; set; } // SUM, AVG, COUNT, MIN, MAX
        public string Format { get; set; } // برای نمایش
        public int DisplayOrder { get; set; }
    }

    public class QueryFilter
    {
        public FilterLogic Logic { get; set; } = FilterLogic.AND;
        public List<FilterCondition> Conditions { get; set; } = new List<FilterCondition>();
        public List<QueryFilter> ChildFilters { get; set; } = new List<QueryFilter>();
    }

    public class FilterCondition
    {
        public string FieldName { get; set; }
        public string DisplayName { get; set; }
        public FieldDataType DataType { get; set; }
        public FilterOperator Operator { get; set; }
        public object Value { get; set; }
        public object Value2 { get; set; } // برای عملگر BETWEEN
    }

    public class SortField
    {
        public string FieldName { get; set; }
        public string DisplayName { get; set; }
        public SortDirection Direction { get; set; }
        public int Order { get; set; }
    }

    public enum FilterLogic
    {
        AND,
        OR
    }

    public enum FilterOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        Between,
        In,
        NotIn,
        IsNull,
        IsNotNull
    }

    public enum FieldDataType
    {
        String,
        Number,
        Decimal,
        Date,
        DateTime,
        Boolean,
        Enum
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }
}
// پایان کد
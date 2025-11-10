// مسیر فایل: Core/QueryEngine/DynamicQueryBuilder.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using TradingJournal.Core.QueryEngine.Models;

namespace TradingJournal.Core.QueryEngine
{
    public class DynamicQueryBuilder<T> where T : class
    {
        private readonly List<PropertyInfo> _properties;

        public DynamicQueryBuilder()
        {
            _properties = typeof(T).GetProperties().ToList();
        }

        public IQueryable<T> BuildQuery(IQueryable<T> source, QueryModel queryModel)
        {
            // اعمال فیلترها
            if (queryModel.RootFilter != null)
            {
                var predicate = BuildFilterExpression(queryModel.RootFilter);
                if (predicate != null)
                {
                    source = source.Where(predicate);
                }
            }

            // اعمال مرتب‌سازی
            if (queryModel.SortFields.Any())
            {
                source = ApplySorting(source, queryModel.SortFields);
            }

            // اعمال صفحه‌بندی
            if (queryModel.PageSize > 0)
            {
                var skip = (queryModel.CurrentPage - 1) * queryModel.PageSize;
                source = source.Skip(skip).Take(queryModel.PageSize);
            }

            return source;
        }

        private Expression<Func<T, bool>> BuildFilterExpression(QueryFilter filter)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var expression = BuildFilterGroup(filter, parameter);

            if (expression == null)
                return null;

            return Expression.Lambda<Func<T, bool>>(expression, parameter);
        }

        private Expression BuildFilterGroup(QueryFilter filter, ParameterExpression parameter)
        {
            var expressions = new List<Expression>();

            // شرط‌های ساده
            foreach (var condition in filter.Conditions)
            {
                var expr = BuildConditionExpression(condition, parameter);
                if (expr != null)
                    expressions.Add(expr);
            }

            // گروه‌های فرعی
            foreach (var childFilter in filter.ChildFilters)
            {
                var expr = BuildFilterGroup(childFilter, parameter);
                if (expr != null)
                    expressions.Add(expr);
            }

            if (!expressions.Any())
                return null;

            // ترکیب expressions با AND یا OR
            Expression combined = expressions[0];
            for (int i = 1; i < expressions.Count; i++)
            {
                combined = filter.Logic == FilterLogic.AND
                    ? Expression.AndAlso(combined, expressions[i])
                    : Expression.OrElse(combined, expressions[i]);
            }

            return combined;
        }

        private Expression BuildConditionExpression(FilterCondition condition, ParameterExpression parameter)
        {
            var property = _properties.FirstOrDefault(p => p.Name == condition.FieldName);
            if (property == null)
                return null;

            var propertyAccess = Expression.Property(parameter, property);
            var propertyType = property.PropertyType;

            // تبدیل مقدار به نوع صحیح
            var value = ConvertValue(condition.Value, propertyType);
            if (value == null && condition.Operator != FilterOperator.IsNull && condition.Operator != FilterOperator.IsNotNull)
                return null;

            Expression valueExpression = value != null ? Expression.Constant(value, propertyType) : null;

            return condition.Operator switch
            {
                FilterOperator.Equal => Expression.Equal(propertyAccess, valueExpression),
                FilterOperator.NotEqual => Expression.NotEqual(propertyAccess, valueExpression),
                FilterOperator.GreaterThan => Expression.GreaterThan(propertyAccess, valueExpression),
                FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(propertyAccess, valueExpression),
                FilterOperator.LessThan => Expression.LessThan(propertyAccess, valueExpression),
                FilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(propertyAccess, valueExpression),
                FilterOperator.Contains => BuildContainsExpression(propertyAccess, value?.ToString()),
                FilterOperator.NotContains => Expression.Not(BuildContainsExpression(propertyAccess, value?.ToString())),
                FilterOperator.StartsWith => BuildStartsWithExpression(propertyAccess, value?.ToString()),
                FilterOperator.EndsWith => BuildEndsWithExpression(propertyAccess, value?.ToString()),
                FilterOperator.Between => BuildBetweenExpression(propertyAccess, value, ConvertValue(condition.Value2, propertyType)),
                FilterOperator.IsNull => Expression.Equal(propertyAccess, Expression.Constant(null)),
                FilterOperator.IsNotNull => Expression.NotEqual(propertyAccess, Expression.Constant(null)),
                _ => null
            };
        }

        private Expression BuildContainsExpression(MemberExpression property, string value)
        {
            var method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
            var valueExpr = Expression.Constant(value);
            return Expression.Call(property, method, valueExpr);
        }

        private Expression BuildStartsWithExpression(MemberExpression property, string value)
        {
            var method = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
            var valueExpr = Expression.Constant(value);
            return Expression.Call(property, method, valueExpr);
        }

        private Expression BuildEndsWithExpression(MemberExpression property, string value)
        {
            var method = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });
            var valueExpr = Expression.Constant(value);
            return Expression.Call(property, method, valueExpr);
        }

        private Expression BuildBetweenExpression(MemberExpression property, object value1, object value2)
        {
            if (value1 == null || value2 == null)
                return null;

            var greaterThan = Expression.GreaterThanOrEqual(property, Expression.Constant(value1));
            var lessThan = Expression.LessThanOrEqual(property, Expression.Constant(value2));
            return Expression.AndAlso(greaterThan, lessThan);
        }

        private IQueryable<T> ApplySorting(IQueryable<T> source, List<SortField> sortFields)
        {
            var orderedFields = sortFields.OrderBy(s => s.Order).ToList();
            IOrderedQueryable<T> orderedQuery = null;

            for (int i = 0; i < orderedFields.Count; i++)
            {
                var field = orderedFields[i];
                var property = _properties.FirstOrDefault(p => p.Name == field.FieldName);
                
                if (property == null)
                    continue;

                var parameter = Expression.Parameter(typeof(T), "x");
                var propertyAccess = Expression.Property(parameter, property);
                var lambda = Expression.Lambda(propertyAccess, parameter);

                var methodName = i == 0 
                    ? (field.Direction == SortDirection.Ascending ? "OrderBy" : "OrderByDescending")
                    : (field.Direction == SortDirection.Ascending ? "ThenBy" : "ThenByDescending");

                var method = typeof(Queryable).GetMethods()
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 2)
                    ?.MakeGenericMethod(typeof(T), property.PropertyType);

                if (method != null)
                {
                    var result = method.Invoke(null, new object[] { orderedQuery ?? source, lambda });
                    orderedQuery = (IOrderedQueryable<T>)result;
                }
            }

            return orderedQuery ?? source;
        }

        private object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return null;

            try
            {
                if (targetType == typeof(string))
                    return value.ToString();

                if (targetType.IsEnum)
                    return Enum.Parse(targetType, value.ToString());

                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        public List<QueryField> GetAvailableFields()
        {
            return _properties.Select((p, index) => new QueryField
            {
                FieldName = p.Name,
                DisplayName = GetDisplayName(p),
                DataType = GetFieldDataType(p.PropertyType),
                IsSelected = false,
                DisplayOrder = index
            }).ToList();
        }

        private string GetDisplayName(PropertyInfo property)
        {
            // می‌توان از DisplayAttribute استفاده کرد
            var displayAttr = property.GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>();
            return displayAttr?.Name ?? property.Name;
        }

        private FieldDataType GetFieldDataType(Type type)
        {
            if (type == typeof(string))
                return FieldDataType.String;
            if (type == typeof(int) || type == typeof(long))
                return FieldDataType.Number;
            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
                return FieldDataType.Decimal;
            if (type == typeof(DateTime))
                return FieldDataType.DateTime;
            if (type == typeof(bool))
                return FieldDataType.Boolean;
            if (type.IsEnum)
                return FieldDataType.Enum;

            return FieldDataType.String;
        }
    }
}
// پایان کد
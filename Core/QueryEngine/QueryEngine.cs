// ابتدای فایل: Core/QueryEngine/QueryEngine.cs
// مسیر: /Core/QueryEngine/QueryEngine.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DynamicExpresso;
using Serilog;
using TradingJournal.Data;
using TradingJournal.Data.Models;

namespace TradingJournal.Core.QueryEngine
{
    public class QueryBuilder
    {
        public string EntityType { get; set; } = "Trade";
        public List<string> SelectFields { get; set; } = new();
        public List<FilterCondition> Filters { get; set; } = new();
        public List<SortCondition> Sorts { get; set; } = new();
        public List<string> GroupByFields { get; set; } = new();
        public int? Skip { get; set; }
        public int? Take { get; set; }
    }

    public class FilterCondition
    {
        public string Field { get; set; } = string.Empty;
        public FilterOperator Operator { get; set; }
        public object? Value { get; set; }
        public object? Value2 { get; set; } // For Between operator
        public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
    }

    public class SortCondition
    {
        public string Field { get; set; } = string.Empty;
        public bool Ascending { get; set; } = true;
    }

    public enum FilterOperator
    {
        Equals,
        NotEquals,
        Contains,
        StartsWith,
        EndsWith,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Between,
        In,
        NotIn,
        IsNull,
        IsNotNull
    }

    public enum LogicalOperator
    {
        And,
        Or
    }

    public class QueryEngine
    {
        private readonly DatabaseContext _dbContext;
        private readonly Interpreter _interpreter;

        public QueryEngine()
        {
            _dbContext = new DatabaseContext();
            _interpreter = new Interpreter();
        }

        public async Task<QueryResult<T>> ExecuteQueryAsync<T>(QueryBuilder query) where T : BaseEntity
        {
            try
            {
                IQueryable<T> queryable = _dbContext.Set<T>();

                // Apply filters
                queryable = ApplyFilters(queryable, query.Filters);

                // Apply sorting
                queryable = ApplySorting(queryable, query.Sorts);

                // Get total count before pagination
                var totalCount = await queryable.CountAsync();

                // Apply pagination
                if (query.Skip.HasValue)
                    queryable = queryable.Skip(query.Skip.Value);
                
                if (query.Take.HasValue)
                    queryable = queryable.Take(query.Take.Value);

                // Execute query
                var data = await queryable.ToListAsync();

                // Apply field selection if specified
                var result = new QueryResult<T>
                {
                    Data = data,
                    TotalCount = totalCount,
                    PageSize = query.Take ?? totalCount,
                    CurrentPage = (query.Skip ?? 0) / (query.Take ?? 1) + 1
                };

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing query");
                throw;
            }
        }

        private IQueryable<T> ApplyFilters<T>(IQueryable<T> queryable, List<FilterCondition> filters) where T : BaseEntity
        {
            if (!filters.Any()) return queryable;

            Expression<Func<T, bool>>? combinedExpression = null;

            foreach (var filter in filters)
            {
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, filter.Field);
                Expression? condition = null;

                switch (filter.Operator)
                {
                    case FilterOperator.Equals:
                        condition = Expression.Equal(property, Expression.Constant(filter.Value));
                        break;
                    
                    case FilterOperator.NotEquals:
                        condition = Expression.NotEqual(property, Expression.Constant(filter.Value));
                        break;
                    
                    case FilterOperator.GreaterThan:
                        condition = Expression.GreaterThan(property, Expression.Constant(filter.Value));
                        break;
                    
                    case FilterOperator.GreaterThanOrEqual:
                        condition = Expression.GreaterThanOrEqual(property, Expression.Constant(filter.Value));
                        break;
                    
                    case FilterOperator.LessThan:
                        condition = Expression.LessThan(property, Expression.Constant(filter.Value));
                        break;
                    
                    case FilterOperator.LessThanOrEqual:
                        condition = Expression.LessThanOrEqual(property, Expression.Constant(filter.Value));
                        break;
                    
                    case FilterOperator.Contains:
                        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                        condition = Expression.Call(property, containsMethod!, Expression.Constant(filter.Value));
                        break;
                    
                    case FilterOperator.IsNull:
                        condition = Expression.Equal(property, Expression.Constant(null));
                        break;
                    
                    case FilterOperator.IsNotNull:
                        condition = Expression.NotEqual(property, Expression.Constant(null));
                        break;
                }

                if (condition != null)
                {
                    var lambda = Expression.Lambda<Func<T, bool>>(condition, parameter);
                    
                    if (combinedExpression == null)
                    {
                        combinedExpression = lambda;
                    }
                    else
                    {
                        combinedExpression = filter.LogicalOperator == LogicalOperator.And
                            ? CombineExpressions(combinedExpression, lambda, Expression.AndAlso)
                            : CombineExpressions(combinedExpression, lambda, Expression.OrElse);
                    }
                }
            }

            return combinedExpression != null ? queryable.Where(combinedExpression) : queryable;
        }

        private IQueryable<T> ApplySorting<T>(IQueryable<T> queryable, List<SortCondition> sorts)
        {
            if (!sorts.Any()) return queryable;

            IOrderedQueryable<T>? orderedQuery = null;

            for (int i = 0; i < sorts.Count; i++)
            {
                var sort = sorts[i];
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, sort.Field);
                var lambda = Expression.Lambda(property, parameter);

                var methodName = i == 0
                    ? (sort.Ascending ? "OrderBy" : "OrderByDescending")
                    : (sort.Ascending ? "ThenBy" : "ThenByDescending");

                var method = typeof(Queryable).GetMethods()
                    .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T), property.Type);

                queryable = (IQueryable<T>)method.Invoke(null, new object[] { queryable, lambda })!;
            }

            return queryable;
        }

        private Expression<Func<T, bool>> CombineExpressions<T>(
            Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2,
            Func<Expression, Expression, BinaryExpression> combiner)
        {
            var parameter = Expression.Parameter(typeof(T));
            
            var leftVisitor = new ReplaceExpressionVisitor(expr1.Parameters[0], parameter);
            var left = leftVisitor.Visit(expr1.Body);
            
            var rightVisitor = new ReplaceExpressionVisitor(expr2.Parameters[0], parameter);
            var right = rightVisitor.Visit(expr2.Body);
            
            return Expression.Lambda<Func<T, bool>>(combiner(left, right), parameter);
        }

        private class ReplaceExpressionVisitor : ExpressionVisitor
        {
            private readonly Expression _oldValue;
            private readonly Expression _newValue;

            public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
            {
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override Expression Visit(Expression? node)
            {
                if (node == _oldValue)
                    return _newValue;
                return base.Visit(node)!;
            }
        }
    }

    public class QueryResult<T>
    {
        public List<T> Data { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}

// پایان فایل: Core/QueryEngine/QueryEngine.cs
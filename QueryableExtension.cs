using System.Collections.Generic;
using System.Data.SqlClient;
using Castle.Core.Internal;
using Cement.Core.Auth;
using Cement.Core.Entities;
using Cement.Core.Services.StoreParams;

namespace Cement.Core.Extensions
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;

    public static class QueryableExtension
    {
        public static IQueryable<T> Filter<T>(this IQueryable<T> query, StoreParams storeParams) 
        {
            if (storeParams == null /*|| (storeParams.FilterParams == null || !storeParams.FilterParams.Any() )*/)
            {
                return query;
            }

            var properties = typeof(T).GetProperties();

            storeParams
                .DetermineFilterOperators(properties)
                .Where(x => properties.Any(y => y.Name.ToLower() == x.PropertyName.ToLower()))
                .ForEach(x =>
                    {
                        var field = properties.First(y => y.Name.ToLower() == x.PropertyName.ToLower());

                        var filterExpression = FilterExpressionBuilder.GetFilterExpression<T>(field, x.Value, x.Operator, x.SubProperties);

                        if (filterExpression != null)
                        {
                            query = query.Where(filterExpression);
                        }
                    });

            return query;
        }

        public static IQueryable<T> OrganizationFilter<T>(this IQueryable<T> query, IUserPrincipal userPrincipal) where T : BaseOrganizationBasedEntity
        {
            return query.Where(x => x.Organization == userPrincipal.Organization);
        }

        public static IQueryable<T> OrganizationExclusiveFilter<T>(this IQueryable<T> query, IUserPrincipal userPrincipal, long organizationId) where T : BaseOrganizationBasedEntity
        {
            if (organizationId > 0)
            {
                return query.Where(x => x.Organization.Id == organizationId);
            }
            
            return query.OrganizationFilter(userPrincipal);
        }

        public static IQueryable<T> OrganizationFilter<T>(this IQueryable<T> query, Expression<Func<T, Organization>> memberExpression, IUserPrincipal userPrincipal)
        {
            var parameter = Expression.Parameter(typeof(T), "x");

            Expression expr;

            if (userPrincipal.Organization == null)
            {
                expr = Expression.Equal(memberExpression, Expression.Constant(null));
            }
            else
            {
                var idExpression = Expression.Property(memberExpression.Body, "Id");

                expr = Expression.Equal(idExpression, Expression.Constant(userPrincipal.Organization.Id));
            }

            query = query.Where(Expression.Lambda<Func<T, bool>>(expr, parameter));
            
            return query;
        }

        public static IQueryable<T> WarehouseFilter<T>(this IQueryable<T> query, Expression<Func<T, Warehouse>> memberExpression, IUserPrincipal userPrincipal, bool inverse = false)
        {
            var parameter = Expression.Parameter(typeof(T), "x");

            Expression expr;

            if (userPrincipal.WarehouseIds == null || userPrincipal.WarehouseIds.Count == 0)
            {
                expr = Expression.Constant(false);
            }
            else
            {
                var idExpression = Expression.Property(memberExpression.Body, "Id");

                var exprValue = Expression.Constant(userPrincipal.WarehouseIds);

                expr = Expression.Call(exprValue, typeof(List<long>).GetMethod("Contains", new[] { typeof(long) }), idExpression);

                if (inverse)
                {
                    expr = Expression.Not(expr);
                }
            }

            query = query.Where(Expression.Lambda<Func<T, bool>>(expr, parameter));

            return query;
        }

        public static IQueryable<T> WhereIf<T>(
            this IQueryable<T> query,
            bool condition,
            Expression<Func<T, bool>> predicate)
        {
            if (condition)
            {
                return query.Where(predicate);
            }

            return query;
        }

        public static IQueryable<T> WhereAny<T>(
            this IQueryable<T> query,
            Expression<Func<T, bool>> firstPredicate,
            params Expression<Func<T, bool>>[] predicates)
        {
            var finalPredicate = firstPredicate;
            
            foreach (var predicate in predicates)
            {
                finalPredicate = finalPredicate.Or(predicate);
            }
            
            return query.Where(finalPredicate);
        }

        public static IOrderedQueryable<T> Order<T>(this IQueryable<T> query, StoreParams storeParams)
        {
            var result = (IOrderedQueryable<T>) query;

            // Были ли запрос уже сортирован
            var isOrdered = query.Expression.Type == typeof (IOrderedQueryable<T>);

            if (storeParams != null && storeParams.SortParams != null)
            {
                var properties = typeof(T).GetProperties();

                foreach (var sortParam in storeParams.SortParams.DetermineSortFields(properties))
                {
                    // The order expressions
                    var x = Expression.Parameter(typeof(T), "x");

                    if (sortParam.NullSort != SortOrder.Unspecified)
                    {
                        var propertyExpression = Expression.Property(x, sortParam.PropertyName);

                        var eq = Expression.Equal(propertyExpression, Expression.Constant(null));

                        // Костыль, т.к. в оракле нет bool
                        // Чтобы ORDER BY получился подобным:
                        // cast(case when FIELD_NAME is null then 1 else 0 end as NUMBER(10,0))
                        var cond = Expression.Condition(eq, Expression.Constant(1), Expression.Constant(0));

                        var expr = Expression.Lambda<Func<T, int>>(cond, x);

                        result = isOrdered
                            ? sortParam.NullSort == SortOrder.Descending ? result.ThenByDescending(expr) : result.ThenBy(expr)
                            : sortParam.NullSort == SortOrder.Descending ? query.OrderByDescending(expr) : query.OrderBy(expr);

                        isOrdered = true;
                    }


                    var memberExpression = Expression.PropertyOrField(x, sortParam.PropertyName);
                    
                    if (sortParam.PropertyType != null && sortParam.PropertyType.IsSubclassOf(typeof (BaseEntity)))
                    {
                        if (string.IsNullOrWhiteSpace(sortParam.SubProperties))
                        {
                            var prop = sortParam.PropertyType.GetProperty("Name");
                            if (prop != null)
                            {
                                memberExpression = Expression.PropertyOrField(memberExpression, prop.Name);
                            }
                        }
                        else
                        {
                            var parts = sortParam.SubProperties.Split('.').ToList();

                            var type = sortParam.PropertyType;

                            foreach (var part in parts)
                            {
                                var prop = type.GetProperty(part);
                                if (prop != null)
                                {
                                    memberExpression = Expression.PropertyOrField(memberExpression, prop.Name);
                                    type = prop.PropertyType;
                                }
                            }
                        }
                    }

                    var expression = Expression.Lambda<Func<T, object>>(Expression.Convert(memberExpression, typeof(object)), x);

                    result = isOrdered
                        ? sortParam.Direction == SortOrder.Descending ? result.ThenByDescending(expression) : result.ThenBy(expression)
                        : sortParam.Direction == SortOrder.Descending ? query.OrderByDescending(expression) : query.OrderBy(expression);

                    isOrdered = true;
                }
            }
            
            return isOrdered ? result.OrderById() : query.OrderById();
        }

        public static IOrderedQueryable<T> OrderById<T>(this IQueryable<T> query)
        {
            var result = (IOrderedQueryable<T>)query;

            // Были ли запрос уже сортирован
            var isOrdered = query.Expression.Type == typeof(IOrderedQueryable<T>);

            var queryType = typeof(T);

            // Then order by ID to ensure stable sorting
            if (queryType.GetProperty("id") != null)
            {
                var x = Expression.Parameter(queryType, "x");

                var expression = Expression.Lambda<Func<T, object>>(
                        Expression.Convert(Expression.Property(x, "id"), typeof(object)), x);

                return isOrdered ? result.ThenBy(expression) : query.OrderBy(expression);
            }
            else if (queryType.GetProperty("Id") != null)
            {
                var x = Expression.Parameter(queryType, "x");

                var expression = Expression.Lambda<Func<T, object>>(
                        Expression.Convert(Expression.Property(x, "Id"), typeof(object)), x);

                return isOrdered ? result.ThenBy(expression) : query.OrderBy(expression);
            }

            return isOrdered ? result : query.OrderBy(x => "1");
        }

        public static IQueryable<T> Paging<T>(this IQueryable<T> data, StoreParams storeParams)
        {
            IQueryable<T> result = data;

            if (storeParams != null)
            {
                result = data.Skip(storeParams.Offset).Take(storeParams.Limit);
            }

            return result;
        }
    }

    public static class PredicateBuilder
    {
        public static Expression<Func<T, bool>> True<T>() { return f => true; }
        public static Expression<Func<T, bool>> False<T>() { return f => false; }

        public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expr1,
                                                            Expression<Func<T, bool>> expr2)
        {
            var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>());
            return Expression.Lambda<Func<T, bool>>
                  (Expression.OrElse(expr1.Body, invokedExpr), expr1.Parameters);
        }

        public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> expr1,
                                                             Expression<Func<T, bool>> expr2)
        {
            var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>());
            return Expression.Lambda<Func<T, bool>>
                  (Expression.AndAlso(expr1.Body, invokedExpr), expr1.Parameters);
        }
    }
}
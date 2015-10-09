using System.Collections.Generic;
using System.Linq;
using Cement.Core.Services.StoreParams;

namespace Cement.Core.Extensions
{
    public static class EnumerableExtension
    {
        public static IEnumerable<Dictionary<string, object>> Highlight<T>(this IEnumerable<T> enumerable, StoreParams storeParams)
        {
            var properties = typeof(T).GetProperties();

            var result = enumerable.Select(x => properties.ToDictionary(property => property.Name, property => property.GetValue(x)));

            return result.Highlight(storeParams);
        }

        public static IEnumerable<Dictionary<string, object>> Highlight(
            this IEnumerable<Dictionary<string, object>> enumerable, StoreParams storeParams)
        {
            if (storeParams == null || storeParams.FilterParams == null || !storeParams.FilterParams.Any())
            {
                return enumerable;
            }

            var highlightParams = storeParams.FilterParams.Where(x => x.PropertyName.Contains(Constants.HighLight)).ToList();

            if (!highlightParams.Any())
            {
                return enumerable;
            }

            var list = enumerable.ToList();

            var fieldNames = list.SelectMany(x => x.Keys).Distinct().ToList();

            var highlightOptions = highlightParams.DetermineHighlightOptions(fieldNames);

            list.ForEach(x =>
            {
                var style = GetHighlightStyle(highlightOptions, x);

                if (!string.IsNullOrWhiteSpace(style))
                {
                    x["p_grid_row_cls"] = style;
                }
            });

            return list;
        }

        private static string GetHighlightStyle(List<HighlightParam> highlightOptions, Dictionary<string, object> row)
        {
            foreach (var highlightOption in highlightOptions)
            {
                if (Services.StoreParams.Highlight.Test(highlightOption, row))
                {
                    return highlightOption.Style;
                }
            }

            return null;
        }
    }
}
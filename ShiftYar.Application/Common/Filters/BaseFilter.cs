using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Filters
{
    public abstract class BaseFilter<T> : IFilter<T>
    {
        public string SortBy { get; set; }
        public bool SortAscending { get; set; } = true;

        public abstract Expression<Func<T, bool>> GetExpression();

        // متد کمکی برای ترکیب دو شرط به صورت AND منطقی
        protected Expression<Func<T, bool>> CombineExpressions(Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
        {
            var parameter = Expression.Parameter(typeof(T));

            var combined = Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(
                    Expression.Invoke(first, parameter),
                    Expression.Invoke(second, parameter)
                ),
                parameter
            );

            return combined;
        }
    }
}

using System;
using System.Linq.Expressions;

namespace ShiftYar.Application.Common.Filters
{
    public class SimpleFilter<T> : BaseFilter<T>
    {
        private readonly Expression<Func<T, bool>> _expression;

        public SimpleFilter(Expression<Func<T, bool>> expression)
        {
            _expression = expression;
        }

        public override Expression<Func<T, bool>> GetExpression()
        {
            return _expression;
        }
    }
}



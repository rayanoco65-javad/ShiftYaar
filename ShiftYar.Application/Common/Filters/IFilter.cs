using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Filters
{
    //زیرساخت فیلتر
    public interface IFilter<T>
    {
        // متد عمومی برای تولید شرط جستجو به صورت Expression
        Expression<Func<T, bool>> GetExpression();
    }
}

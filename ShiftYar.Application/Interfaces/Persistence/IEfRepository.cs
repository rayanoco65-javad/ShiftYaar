using ShiftYar.Application.Common.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.Persistence
{
    //اینترفیس عمومی ریپازیتوری
    public interface IEfRepository<T> where T : class
    {
        // دریافت لیست براساس فیلتر و بارگذاری روابط
        Task<(List<T> Items, int TotalCount)> GetByFilterAsync(IFilter<T> filter = null, params string[] includes);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
        Task<T> GetByIdAsync(object id, params string[] includes);
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task SaveAsync();

        // سایر متدها می‌تونن اضافه بشن
    }
}

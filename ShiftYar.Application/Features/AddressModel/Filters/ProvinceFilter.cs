using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.AddressModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.AddressModel.Filters
{
    public class ProvinceFilter : BaseFilter<Province>
    {
        public int? Id { get; set; }
        public string? Name { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10000;

        public ProvinceFilter()
        {
            SortBy = "Name";
            SortAscending = true;
        }

        public override Expression<Func<Province, bool>> GetExpression()
        {
            Expression<Func<Province, bool>> expression = province => true;

            if (Id.HasValue)
            {
                Expression<Func<Province, bool>> idExpr = province => province.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if(!string.IsNullOrEmpty(Name))
            {
                Expression<Func<Province, bool>> nameExpr = province => province.Name == Name;
                expression = CombineExpressions(expression, nameExpr);
            }

            return expression;
        }
    }
}

using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.DepartmentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.DepartmentModel.Filters
{
    public class DepartmentNameFilter : BaseFilter<DepartmentName>
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Search { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<DepartmentName, bool>> GetExpression()
        {
            Expression<Func<DepartmentName, bool>> expression = departmentName => true;

            if (Id.HasValue)
            {
                Expression<Func<DepartmentName, bool>> idExpr = departmentName => departmentName.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (!string.IsNullOrEmpty(Name))
            {
                Expression<Func<DepartmentName, bool>> nameExpr = departmentName => departmentName.Name.Contains(Name);
                expression = CombineExpressions(expression, nameExpr);
            }

            if (!string.IsNullOrEmpty(Search))
            {
                Expression<Func<DepartmentName, bool>> searchExpr = departmentName =>
                    departmentName.Name != null && departmentName.Name.Contains(Search);

                expression = CombineExpressions(expression, searchExpr);
            }

            return expression;
        }
    }
}

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
    public class DepartmentSchedulingSettingsFilter : BaseFilter<DepartmentSchedulingSettings>
    {
        public int? DepartmentId { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<DepartmentSchedulingSettings, bool>> GetExpression()
        {
            Expression<Func<DepartmentSchedulingSettings, bool>> expression = s => true;

            if (DepartmentId.HasValue)
            {
                Expression<Func<DepartmentSchedulingSettings, bool>> deptExpr = s => s.DepartmentId == DepartmentId;
                expression = CombineExpressions(expression, deptExpr);
            }

            return expression;
        }
    }
}

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
    public class DepartmentFilter : BaseFilter<Department>
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public int? HospitalId { get; set; }
        public int? SupervisorId { get; set; }
        public string? Search { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<Department, bool>> GetExpression()
        {
            Expression<Func<Department, bool>> expression = department => true;

            if (Id.HasValue)
            {
                Expression<Func<Department, bool>> idExpr = department => department.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (!string.IsNullOrEmpty(Name))
            {
                Expression<Func<Department, bool>> nameExpr = department => department.Name.Contains(Name);
                expression = CombineExpressions(expression, nameExpr);
            }

            if (!string.IsNullOrEmpty(Description))
            {
                Expression<Func<Department, bool>> descriptionExpr = department => department.Description.Contains(Description);
                expression = CombineExpressions(expression, descriptionExpr);
            }

            if (IsActive.HasValue)
            {
                Expression<Func<Department, bool>> isActiveExpr = department => department.IsActive == IsActive;
                expression = CombineExpressions(expression, isActiveExpr);
            }

            if (HospitalId.HasValue)
            {
                Expression<Func<Department, bool>> hospitalExpr = department => department.HospitalId == HospitalId;
                expression = CombineExpressions(expression, hospitalExpr);
            }

            if (SupervisorId.HasValue)
            {
                Expression<Func<Department, bool>> supervisorExpr = department => department.SupervisorId == SupervisorId;
                expression = CombineExpressions(expression, supervisorExpr);
            }

            if (!string.IsNullOrEmpty(Search))
            {
                Expression<Func<Department, bool>> searchExpr = department =>
                    department.Name != null && department.Name.Contains(Search) ||
                    department.Description != null && department.Description.Contains(Search);

                expression = CombineExpressions(expression, searchExpr);
            }

            return expression;
        }
    }
}
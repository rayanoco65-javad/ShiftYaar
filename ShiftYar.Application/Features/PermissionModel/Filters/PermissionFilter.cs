using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.PermissionModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.PermissionModel.Filters
{
    public class PermissionFilter : BaseFilter<Permission>
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public string? Search { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<Permission, bool>> GetExpression()
        {
            Expression<Func<Permission, bool>> expression = permission => true;

            if (Id.HasValue)
            {
                Expression<Func<Permission, bool>> idExpr = permission => permission.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (!string.IsNullOrEmpty(Name))
            {
                Expression<Func<Permission, bool>> nameExpr = permission => permission.Name.Contains(Name);
                expression = CombineExpressions(expression, nameExpr);
            }

            if (!string.IsNullOrEmpty(Description))
            {
                Expression<Func<Permission, bool>> descriptionExpr = permission => permission.Description.Contains(Description);
                expression = CombineExpressions(expression, descriptionExpr);
            }

            if (IsActive.HasValue)
            {
                Expression<Func<Permission, bool>> isActiveExpr = permission => permission.IsActive == IsActive;
                expression = CombineExpressions(expression, isActiveExpr);
            }

            if (!string.IsNullOrEmpty(Search))
            {
                Expression<Func<Permission, bool>> searchExpr = permission =>
                    permission.Name != null && permission.Name.Contains(Search) ||
                    permission.Description != null && permission.Description.Contains(Search);

                expression = CombineExpressions(expression, searchExpr);
            }

            return expression;
        }
    }
}

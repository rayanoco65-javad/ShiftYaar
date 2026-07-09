using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.RoleModel;
using System.Linq.Expressions;

namespace ShiftYar.Application.Features.RoleModel.Filters
{
    public class RoleFilter : BaseFilter<Role>
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
        public string? Search { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<Role, bool>> GetExpression()
        {
            Expression<Func<Role, bool>> expression = role => true;

            if (Id.HasValue)
            {
                Expression<Func<Role, bool>> idExpr = role => role.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (!string.IsNullOrEmpty(Name))
            {
                Expression<Func<Role, bool>> nameExpr = role => role.Name.Contains(Name);
                expression = CombineExpressions(expression, nameExpr);
            }

            if (IsActive.HasValue)
            {
                Expression<Func<Role, bool>> isActiveExpr = role => role.IsActive == IsActive;
                expression = CombineExpressions(expression, isActiveExpr);
            }

            if (!string.IsNullOrEmpty(Search))
            {
                Expression<Func<Role, bool>> searchExpr = role =>
                    role.Name != null && role.Name.Contains(Search);

                expression = CombineExpressions(expression, searchExpr);
            }

            return expression;
        }
    }
}
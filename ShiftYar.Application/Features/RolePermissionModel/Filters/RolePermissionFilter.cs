using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.RoleModel;
using ShiftYar.Domain.Entities.RolePermissionModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.RolePermissionModel.Filters
{
    public class RolePermissionFilter : BaseFilter<RolePermission>
    {
        public int? RoleId { get; set; }
        public int? PermissionId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<RolePermission, bool>> GetExpression()
        {
            Expression<Func<RolePermission, bool>> expression = role => true;

            if (RoleId.HasValue)
            {
                Expression<Func<RolePermission, bool>> roleIdExpr = role => role.RoleId == RoleId;
                expression = CombineExpressions(expression, roleIdExpr);
            }
            if (PermissionId.HasValue)
            {
                Expression<Func<RolePermission, bool>> permissionIdExpr = permission => permission.PermissionId == PermissionId;
                expression = CombineExpressions(expression, permissionIdExpr);
            }

            return expression;
        }
    }
}

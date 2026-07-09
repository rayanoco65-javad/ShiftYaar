using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.RoleModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ShiftYar.Application.Features.SpecialtyModel.Filters
{
    public class SpecialtyFilter : BaseFilter<Specialty>
    {
        public int? Id { get; set; }
        public string? SpecialtyName { get; set; }
        public int? DepartmentId { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;


        public override Expression<Func<Specialty, bool>> GetExpression()
        {
            Expression<Func<Specialty, bool>> expression = specialty => true;

            if (Id.HasValue)
            {
                Expression<Func<Specialty, bool>> idExpr = specialty => specialty.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (!string.IsNullOrEmpty(SpecialtyName))
            {
                Expression<Func<Specialty, bool>> nameExpr = specialty => specialty.SpecialtyName.Contains(SpecialtyName);
                expression = CombineExpressions(expression, nameExpr);
            }

            if (DepartmentId.HasValue)
            {
                Expression<Func<Specialty, bool>> departmentExpr = specialty => specialty.DepartmentId == DepartmentId;
                expression = CombineExpressions(expression, departmentExpr);
            }

            return expression;
        }
    }
}

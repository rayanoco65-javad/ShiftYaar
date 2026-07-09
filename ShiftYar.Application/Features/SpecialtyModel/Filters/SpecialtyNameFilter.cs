using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.SpecialtyModel.Filters
{
    public class SpecialtyNameFilter : BaseFilter<SpecialtyName>
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Search { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<SpecialtyName, bool>> GetExpression()
        {
            Expression<Func<SpecialtyName, bool>> expression = specialtyName => true;

            if (Id.HasValue)
            {
                Expression<Func<SpecialtyName, bool>> idExpr = specialtyName => specialtyName.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (!string.IsNullOrEmpty(Name))
            {
                Expression<Func<SpecialtyName, bool>> nameExpr = specialtyName => specialtyName.Name.Contains(Name);
                expression = CombineExpressions(expression, nameExpr);
            }

            if (!string.IsNullOrEmpty(Search))
            {
                Expression<Func<SpecialtyName, bool>> searchExpr = specialtyName =>
                    specialtyName.Name != null && specialtyName.Name.Contains(Search);

                expression = CombineExpressions(expression, searchExpr);
            }

            return expression;
        }
    }
}

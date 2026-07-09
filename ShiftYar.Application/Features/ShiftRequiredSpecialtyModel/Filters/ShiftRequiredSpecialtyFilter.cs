using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftRequiredSpecialtyModel.Filters
{
    public class ShiftRequiredSpecialtyFilter : BaseFilter<ShiftRequiredSpecialty>
    {
        public int? ShiftId { get; set; }
        public int? SpecialtyId { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<ShiftRequiredSpecialty, bool>> GetExpression()
        {
            Expression<Func<ShiftRequiredSpecialty, bool>> expression = shiftRequiredSpciality => true;

            if (ShiftId.HasValue)
            {
                Expression<Func<ShiftRequiredSpecialty, bool>> shiftExp = shiftRequiredSpciality => shiftRequiredSpciality.ShiftId == ShiftId;
                expression = CombineExpressions(expression, shiftExp);
            }

            if(SpecialtyId.HasValue)
            {
                Expression<Func<ShiftRequiredSpecialty, bool>> specialityExp = shiftRequiredSpciality => shiftRequiredSpciality.SpecialtyId == SpecialtyId;
                expression = CombineExpressions(expression, specialityExp);
            }

            return expression;
        }
    }
}

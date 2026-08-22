using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Linq.Expressions;

namespace ShiftYar.Application.Features.UserModel.Filters
{
    public class UserMonthlyComboShiftQuotaFilter : BaseFilter<UserMonthlyComboShiftQuota>
    {
        public int? DepartmentId { get; set; }
        public int? UserId { get; set; }
        public int? PersianYear { get; set; }
        public int? PersianMonth { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        public override Expression<Func<UserMonthlyComboShiftQuota, bool>> GetExpression()
        {
            Expression<Func<UserMonthlyComboShiftQuota, bool>> expression = q => true;

            if (UserId.HasValue)
            {
                expression = CombineExpressions(expression, q => q.UserId == UserId.Value);
            }

            if (PersianYear.HasValue)
            {
                expression = CombineExpressions(expression, q => q.PersianYear == PersianYear.Value);
            }

            if (PersianMonth.HasValue)
            {
                expression = CombineExpressions(expression, q => q.PersianMonth == PersianMonth.Value);
            }

            if (DepartmentId.HasValue)
            {
                expression = CombineExpressions(expression,
                    q => q.User != null && q.User.DepartmentId == DepartmentId.Value);
            }

            return expression;
        }
    }
}

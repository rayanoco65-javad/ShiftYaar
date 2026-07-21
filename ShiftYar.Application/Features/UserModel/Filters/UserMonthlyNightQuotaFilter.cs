using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Linq.Expressions;

namespace ShiftYar.Application.Features.UserModel.Filters
{
    public class UserMonthlyNightQuotaFilter : BaseFilter<UserMonthlyNightQuota>
    {
        public int? DepartmentId { get; set; }
        public int? UserId { get; set; }
        public int? PersianYear { get; set; }
        public int? PersianMonth { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        public override Expression<Func<UserMonthlyNightQuota, bool>> GetExpression()
        {
            Expression<Func<UserMonthlyNightQuota, bool>> expression = q => true;

            if (UserId.HasValue)
            {
                Expression<Func<UserMonthlyNightQuota, bool>> expr = q => q.UserId == UserId.Value;
                expression = CombineExpressions(expression, expr);
            }

            if (PersianYear.HasValue)
            {
                Expression<Func<UserMonthlyNightQuota, bool>> expr = q => q.PersianYear == PersianYear.Value;
                expression = CombineExpressions(expression, expr);
            }

            if (PersianMonth.HasValue)
            {
                Expression<Func<UserMonthlyNightQuota, bool>> expr = q => q.PersianMonth == PersianMonth.Value;
                expression = CombineExpressions(expression, expr);
            }

            if (DepartmentId.HasValue)
            {
                Expression<Func<UserMonthlyNightQuota, bool>> expr =
                    q => q.User != null && q.User.DepartmentId == DepartmentId.Value;
                expression = CombineExpressions(expression, expr);
            }

            return expression;
        }
    }
}

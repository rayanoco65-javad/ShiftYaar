using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Linq.Expressions;

namespace ShiftYar.Application.Features.UserModel.Filters
{
    public class UserMonthlyRequiredHourFilter : BaseFilter<UserMonthlyRequiredHour>
    {
        public int? DepartmentId { get; set; }
        public int? UserId { get; set; }
        public int? PersianYear { get; set; }
        public int? PersianMonth { get; set; }
        public bool? IsManuallyEdited { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        public override Expression<Func<UserMonthlyRequiredHour, bool>> GetExpression()
        {
            Expression<Func<UserMonthlyRequiredHour, bool>> expression = q => true;

            if (UserId.HasValue)
            {
                Expression<Func<UserMonthlyRequiredHour, bool>> expr = q => q.UserId == UserId.Value;
                expression = CombineExpressions(expression, expr);
            }

            if (PersianYear.HasValue)
            {
                Expression<Func<UserMonthlyRequiredHour, bool>> expr = q => q.PersianYear == PersianYear.Value;
                expression = CombineExpressions(expression, expr);
            }

            if (PersianMonth.HasValue)
            {
                Expression<Func<UserMonthlyRequiredHour, bool>> expr = q => q.PersianMonth == PersianMonth.Value;
                expression = CombineExpressions(expression, expr);
            }

            if (DepartmentId.HasValue)
            {
                Expression<Func<UserMonthlyRequiredHour, bool>> expr = q => q.DepartmentId == DepartmentId.Value;
                expression = CombineExpressions(expression, expr);
            }

            if (IsManuallyEdited.HasValue)
            {
                Expression<Func<UserMonthlyRequiredHour, bool>> expr = q => q.IsManuallyEdited == IsManuallyEdited.Value;
                expression = CombineExpressions(expression, expr);
            }

            return expression;
        }
    }
}

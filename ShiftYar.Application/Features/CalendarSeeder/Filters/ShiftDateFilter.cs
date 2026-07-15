using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.ShiftDateModel;
using System;
using System.Linq.Expressions;

namespace ShiftYar.Application.Features.CalendarSeeder.Filters
{
    public class ShiftDateFilter : BaseFilter<ShiftDate>
    {
        public int? Id { get; set; }
        public string? DayTitle { get; set; }
        public string? PersianDate { get; set; }
        public string? PersianDateStart { get; set; }
        public string? PersianDateEnd { get; set; }
        public bool? IsHoliday { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 31;

        private Expression<Func<ShiftDate, bool>>? _expression;

        public ShiftDateFilter()
        {
        }

        public ShiftDateFilter(Expression<Func<ShiftDate, bool>> expression)
        {
            _expression = expression;
        }

        public override Expression<Func<ShiftDate, bool>> GetExpression()
        {
            if (_expression != null)
            {
                return _expression;
            }

            Expression<Func<ShiftDate, bool>> expression = shiftDate => true;

            if (Id.HasValue)
            {
                Expression<Func<ShiftDate, bool>> idExpr = shiftDate => shiftDate.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (!string.IsNullOrEmpty(DayTitle))
            {
                Expression<Func<ShiftDate, bool>> dayTitleExpr = shiftDate => shiftDate.DayTitle == DayTitle;
                expression = CombineExpressions(expression, dayTitleExpr);
            }

            if (!string.IsNullOrEmpty(PersianDate))
            {
                DateTime gregorianDate = DateConverter.ConvertToGregorianDate(PersianDate);

                Expression<Func<ShiftDate, bool>> dateExpr = shiftDate => shiftDate.Date == gregorianDate;
                expression = CombineExpressions(expression, dateExpr);
            }

            if (!string.IsNullOrEmpty(PersianDateStart))
            {
                DateTime gregorianDateStart = DateConverter.ConvertToGregorianDate(PersianDateStart);

                Expression<Func<ShiftDate, bool>> dateStartExpr = shiftDate => shiftDate.Date >= gregorianDateStart;
                expression = CombineExpressions(expression, dateStartExpr);
            }

            if (!string.IsNullOrEmpty(PersianDateEnd))
            {
                DateTime gregorianDateEnd = DateConverter.ConvertToGregorianDate(PersianDateEnd);

                Expression<Func<ShiftDate, bool>> dateEndExpr = shiftDate => shiftDate.Date <= gregorianDateEnd;
                expression = CombineExpressions(expression, dateEndExpr);
            }

            if (IsHoliday.HasValue)
            {
                Expression<Func<ShiftDate, bool>> isHolidayExpr = shiftDate => shiftDate.IsHoliday.Value == IsHoliday.Value;
                expression = CombineExpressions(expression, isHolidayExpr);
            }

            return expression;
        }
    }
}

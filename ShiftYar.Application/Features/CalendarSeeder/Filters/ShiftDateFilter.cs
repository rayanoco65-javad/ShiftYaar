using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.ShiftDateModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

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
                DateTime gregorianDate = ConvertToGregorianDate(PersianDate);

                Expression<Func<ShiftDate, bool>> dateExpr = shiftDate => shiftDate.Date == gregorianDate;
                expression = CombineExpressions(expression, dateExpr);
            }

            if (!string.IsNullOrEmpty(PersianDateStart))
            {
                DateTime gregorianDateStart = ConvertToGregorianDate(PersianDateStart);

                Expression<Func<ShiftDate, bool>> dateStartExpr = shiftDate => shiftDate.Date >= gregorianDateStart;
                expression = CombineExpressions(expression, dateStartExpr);
            }

            if (!string.IsNullOrEmpty(PersianDateEnd))
            {
                DateTime gregorianDateEnd = ConvertToGregorianDate(PersianDateEnd);

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

        private DateTime ConvertToGregorianDate(string persianDate)
        {
            if (string.IsNullOrWhiteSpace(persianDate))
                throw new ArgumentException("تاریخ وارد شده نامعتبر است");

            // رشته تاریخ را به بخش‌های سال، ماه و روز تقسیم می‌کنیم
            var parts = persianDate.Split('/');
            if (parts.Length != 3)
                throw new FormatException("فرمت تاریخ وارد شده صحیح نیست");

            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);

            var persianCalendar = new PersianCalendar();
            return persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
    }
}

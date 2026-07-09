using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftModel.Filters
{
    // 2.1 فیلتر شیفت با جستجوی ترکیبی
    public class ShiftFilter : BaseFilter<Shift>
    {
        public int? Id { get; set; } // شناسه شیفت
        public int? DepartmentId { get; set; } // شناسه بخش
        public int? SupervisorId { get; set; } // شناسه کاربر سوپروایزر

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        //public DateTime? StartDate { get; set; } // تاریخ شروع
        //public DateTime? EndDate { get; set; } // تاریخ پایان
        //public string? Search { get; set; } // 🔍 جستجوی ترکیبی

        public override Expression<Func<Shift, bool>> GetExpression()
        {
            Expression<Func<Shift, bool>> expression = shift => true;

            if (Id.HasValue)
            {
                Expression<Func<Shift, bool>> idExpr = shift => shift.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (DepartmentId.HasValue)
            {
                Expression<Func<Shift, bool>> deptExpr = shift => shift.DepartmentId == DepartmentId;
                expression = CombineExpressions(expression, deptExpr);
            }

            if (SupervisorId.HasValue)
            {
                Expression<Func<Shift, bool>> userExpr = shift => shift.Department.SupervisorId == SupervisorId;
                expression = CombineExpressions(expression, userExpr);
            }

            //if (StartDate.HasValue)
            //{
            //    Expression<Func<Shift, bool>> startExpr = shift => shift.StartTime >= StartDate;
            //    expression = CombineExpressions(expression, startExpr);
            //}

            //if (EndDate.HasValue)
            //{
            //    Expression<Func<Shift, bool>> endExpr = shift => shift.EndTime <= EndDate;
            //    expression = CombineExpressions(expression, endExpr);
            //}

            //if (!string.IsNullOrEmpty(Search))
            //{
            //    Expression<Func<Shift, bool>> searchExpr = shift =>
            //        (shift.Description != null && shift.Description.Contains(Search));
            //    expression = CombineExpressions(expression, searchExpr);
            //}

            return expression;
        }
    }

}

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
    public class ShiftScheduleFilter : BaseFilter<ShiftAssignment>
    {
        public int? Id { get; set; }

        public int? DepartmentId { get; set; }
        public int? UserId { get; set; }
        public int? ShiftId { get; set; }
        public int? ShiftDateId { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public bool? IsOnCall { get; set; }
        public bool? OnlyActiveUsers { get; set; }

        public int? HospitalId { get; set; }    // اگر Hospital از طریق Department.HospitalId مرتبط است
        public int? SpecialtyId { get; set; }

        public string? Search { get; set; }     // جستجو روی نام کاربر، کد پرسنلی، توضیحات و ...

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override Expression<Func<ShiftAssignment, bool>> GetExpression()
        {
            Expression<Func<ShiftAssignment, bool>> expression = sa => true;

            if (Id.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> idExpr = sa => sa.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (DepartmentId.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> deptExpr = sa =>
                    sa.User != null && sa.User.DepartmentId == DepartmentId;
                expression = CombineExpressions(expression, deptExpr);
            }

            if (UserId.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> userExpr = sa => sa.UserId == UserId;
                expression = CombineExpressions(expression, userExpr);
            }

            if (ShiftId.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> shiftExpr = sa => sa.ShiftId == ShiftId;
                expression = CombineExpressions(expression, shiftExpr);
            }

            if (ShiftDateId.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> shiftDateIdExpr = sa => sa.ShiftDateId == ShiftDateId;
                expression = CombineExpressions(expression, shiftDateIdExpr);
            }

            if (FromDate.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> fromDateExpr = sa =>
                    sa.ShiftDate != null && sa.ShiftDate.Date >= FromDate;
                expression = CombineExpressions(expression, fromDateExpr);
            }

            if (ToDate.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> toDateExpr = sa =>
                    sa.ShiftDate != null && sa.ShiftDate.Date <= ToDate;
                expression = CombineExpressions(expression, toDateExpr);
            }

            if (IsOnCall.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> onCallExpr = sa => sa.IsOnCall == IsOnCall;
                expression = CombineExpressions(expression, onCallExpr);
            }

            if (OnlyActiveUsers.HasValue && OnlyActiveUsers.Value)
            {
                Expression<Func<ShiftAssignment, bool>> activeUserExpr = sa =>
                    sa.User != null && (sa.User.IsActive ?? false);
                expression = CombineExpressions(expression, activeUserExpr);
            }

            if (HospitalId.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> hospitalExpr = sa =>
                    sa.User != null &&
                    sa.User.Department != null &&
                    sa.User.Department.HospitalId == HospitalId;
                expression = CombineExpressions(expression, hospitalExpr);
            }

            if (SpecialtyId.HasValue)
            {
                Expression<Func<ShiftAssignment, bool>> specialtyExpr = sa =>
                    sa.User != null && sa.User.SpecialtyId == SpecialtyId;
                expression = CombineExpressions(expression, specialtyExpr);
            }

            if (!string.IsNullOrEmpty(Search))
            {
                Expression<Func<ShiftAssignment, bool>> searchExpr = sa =>
                    (sa.User != null && (
                        (sa.User.FullName != null && sa.User.FullName.Contains(Search)) ||
                        (sa.User.PersonnelCode != null && sa.User.PersonnelCode.Contains(Search)) ||
                        (sa.User.NationalCode != null && sa.User.NationalCode.Contains(Search))
                    )) ||
                    (sa.Notes != null && sa.Notes.Contains(Search));

                expression = CombineExpressions(expression, searchExpr);
            }

            return expression;
        }
    }

}

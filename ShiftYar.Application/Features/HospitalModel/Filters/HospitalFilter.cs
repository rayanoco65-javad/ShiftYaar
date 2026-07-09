using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.UserModel;
using System.Linq.Expressions;

namespace ShiftYar.Application.Features.HospitalModel.Filters
{
    public class HospitalFilter : BaseFilter<Hospital>
    {
        public int? Id { get; set; }
        public string? SiamCode { get; set; }
        public string? Name { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Description { get; set; }
        public string? PhoneNumber { get; set; }
        public string Search { get; set; } // 🔍 جستجوی ترکیبی

        // Pagination parameters
        public int PageNumber { get; set; } = 1; // شماره صفحه پیش‌فرض: 1
        public int PageSize { get; set; } = 10; // تعداد رکورد در هر صفحه پیش‌فرض: 10


        public override Expression<Func<Hospital, bool>> GetExpression()
        {
            Expression<Func<Hospital, bool>> expression = hospital => true; // مقدار اولیه بدون شرط

            // فیلتر براساس شناسه
            if (Id.HasValue)
            {
                Expression<Func<Hospital, bool>> idExpr = hospital => hospital.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            // فیلتر براساس کد سیام
            if (!string.IsNullOrEmpty(SiamCode))
            {
                Expression<Func<Hospital, bool>> siamCodeExpr = hospital => hospital.SiamCode.Contains(SiamCode);
                expression = CombineExpressions(expression, siamCodeExpr);
            }

            // فیلتر براساس نام
            if (!string.IsNullOrEmpty(Name))
            {
                Expression<Func<Hospital, bool>> nameExpr = hospital => hospital.Name.Contains(Name);
                expression = CombineExpressions(expression, nameExpr);
            }

            // فیلتر براساس نام استان
            if (!string.IsNullOrEmpty(Province))
            {
                Expression<Func<Hospital, bool>> provinceExpr = hospital => hospital.Province.Contains(Province);
                expression = CombineExpressions(expression, provinceExpr);
            }

            // فیلتر براساس نام شهر
            if (!string.IsNullOrEmpty(City))
            {
                Expression<Func<Hospital, bool>> cityExpr = hospital => hospital.City.Contains(City);
                expression = CombineExpressions(expression, cityExpr);
            }

            // فیلتر براساس آدرس
            if (!string.IsNullOrEmpty(Address))
            {
                Expression<Func<Hospital, bool>> addressExpr = hospital => hospital.Address.Contains(Address);
                expression = CombineExpressions(expression, addressExpr);
            }

            // فیلتر براساس توضیحات
            if (!string.IsNullOrEmpty(Description))
            {
                Expression<Func<Hospital, bool>> descriptionExpr = hospital => hospital.Description.Contains(Description);
                expression = CombineExpressions(expression, descriptionExpr);
            }

            // فیلتر براساس شماره تماس
            if (!string.IsNullOrEmpty(PhoneNumber))
            {
                Expression<Func<Hospital, bool>> phoneNumberExpr = hospital => hospital.PhoneNumbers.Any(p => p.PhoneNumber.Contains(PhoneNumber));
                expression = CombineExpressions(expression, phoneNumberExpr);
            }

            // جستجوی ترکیبی
            if (!string.IsNullOrEmpty(Search))
            {
                Expression<Func<Hospital, bool>> searchExpr = hospital =>
                    hospital.SiamCode != null && hospital.SiamCode.Contains(Search) ||
                    hospital.Name != null && hospital.Name.Contains(Search) ||
                    hospital.Description != null && hospital.Description.Contains(Search) ||
                    hospital.Address != null && hospital.Address.Contains(Search) ||
                    hospital.PhoneNumbers != null && hospital.PhoneNumbers.Any(p => p.PhoneNumber.Contains(Search));

                expression = CombineExpressions(expression, searchExpr);
            }

            return expression;
        }
    }
}

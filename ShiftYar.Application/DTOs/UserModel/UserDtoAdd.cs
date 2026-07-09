using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserDtoAdd
    {
        //public int? Id { get; set; }

        [Required(ErrorMessage = "نام کامل الزامی است.")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "شماره تلفن الزامی است.")]
        [Phone(ErrorMessage = "شماره تلفن نامعتبر است.")]
        public string? PhoneNumberMembership { get; set; }

        [Required(ErrorMessage = "کد ملی الزامی است.")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید 10 رقم باشد.")]
        public string? NationalCode { get; set; }
        public string? PersonnelCode { get; set; }
        public UserGender? Gender { get; set; }
        public string? DateOfEmployment { get; set; } // تاریخ استخدام (شمسی)
        public bool? IsProjectPersonnel { get; set; }

        [EmailAddress(ErrorMessage = "ایمیل نامعتبر است.")]
        public string? Email { get; set; }
        public string? Password { get; set; } // اختیاری: ست کردن پسورد هنگام ایجاد کاربر
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public bool? IsActive { get; set; }
        public bool? CanBeShiftManager { get; set; }
        public bool? IncludedProductivityPlan { get; set; }
        public string? Image { get; set; }
        public int? DepartmentId { get; set; }
        public int? SpecialtyId { get; set; }

        //کاربر چه نوع شیفتی میدهد؟
        public ShiftTypes? ShiftType { get; set; }
        public ShiftSubTypes? ShiftSubType { get; set; }
        //اگر دو نوبت کاری هست، کدوم شیفت ها رو قراره بیاد
        public TwoShiftRotationPattern? TwoShiftRotationPattern { get; set; }

        //هر کاربر میتواند چند شماره داشته باشد
        public List<string>? OtherPhoneNumbers { get; set; }

        //هر کاربر می تواند یک یا چند نقش داشته باشد
        public List<int>? UserRoles { get; set; }
    }
}

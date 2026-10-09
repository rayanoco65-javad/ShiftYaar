using ShiftYar.Application.DTOs.RoleModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.DTOs.UserModel
{
    public class UserDtoGet
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumberMembership { get; set; }
        public string? NationalCode { get; set; }
        public string? PersonnelCode { get; set; }
        public UserGender? Gender { get; set; }
        public DateTime? DateOfEmployment { get; set; }
        public bool? IsProjectPersonnel { get; set; }
        public string? Email { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public bool? IsActive { get; set; }
        public bool? CanBeShiftManager { get; set; }
        /// <summary>null = مسئول نیست؛ 1 = سطح ۱؛ 2 = سطح ۲</summary>
        public byte? ShiftManagerLevel { get; set; }
        public bool? IncludedProductivityPlan { get; set; }
        public decimal? HardshipPercent { get; set; }
        /// <summary>درصد سختی کار (۰–۱۰۰).</summary>
        public decimal? HardshipPercentage
        {
            get => HardshipPercent;
            set => HardshipPercent = value;
        }
        /// <summary>امتیاز سختی کار قانون مدیریت خدمات کشوری (اختیاری/Nullable).</summary>
        public decimal? HardshipScore { get; set; }
        /// <summary>نام مستعار امتیاز سختی کار.</summary>
        public decimal? HardshipPoints
        {
            get => HardshipScore;
            set => HardshipScore = value;
        }
        /// <summary>پست سازمانی پرسنل</summary>
        public string? Position { get; set; }
        /// <summary>عنوان شغلی پرسنل</summary>
        public string? JobTitle { get; set; }
        /// <summary>آیا سوپروایزر است (ماده ۴ دستورالعمل بهره‌وری)</summary>
        public bool? IsSupervisor { get; set; }
        /// <summary>آیا سرپرستار است (ماده ۴ دستورالعمل بهره‌وری)</summary>
        public bool? IsHeadNurse { get; set; }
        public bool? OvertimeConsent { get; set; }
        /// <summary>حداکثر ساعت موظفی دستی (ساعت). null = محاسبه خودکار.</summary>
        public decimal? MaxProductivityRequiredHours { get; set; }
        public string? Image { get; set; }
        public int? DepartmentId { get; set; }
        public UserDepartmentDtoGet? Department { get; set; }
        public int? SpecialtyId { get; set; }
        public UserSpecialtyDtoGet? Specialty { get; set; }

        /// <summary>لیست مسئولیت‌های تخصصی این پرسنل در بخش (اسکراب ۱، سیرکولر، وینیست، ...)</summary>
        public List<DepartmentModel.DepartmentResponsibilityModel.UserDepartmentResponsibilityDtoGet>? Responsibilities { get; set; }

        //کاربر چه نوع شیفتی میدهد؟
        public ShiftTypes? ShiftType { get; set; }
        public ShiftSubTypes? ShiftSubType { get; set; }
        //اگر دو نوبت کاری هست، کدوم شیفت ها رو قراره بیاد
        public TwoShiftRotationPattern? TwoShiftRotationPattern { get; set; }

        /// <summary>مجوزهای صریح نوع شیفت (flags). null = مشتق از ShiftType.</summary>
        public UserShiftPermission? AllowedShiftPermissions { get; set; }

        /// <summary>اگر کاربر در هر روز شیفت صبح گرفت، شیفت عصر همان روز را هم بگیرد.</summary>
        public bool? MorningRequiresEvening { get; set; }

        /// <summary>اگر کاربر در هر روز شیفت صبح گرفت، شیفت شب همان روز را هم بگیرد.</summary>
        public bool? MorningRequiresNight { get; set; }

        /// <summary>اگر کاربر در هر روز شیفت عصر گرفت، شیفت صبح همان روز را هم بگیرد.</summary>
        public bool? EveningRequiresMorning { get; set; }

        /// <summary>اگر کاربر در هر روز شیفت شب گرفت، شیفت صبح همان روز را هم بگیرد.</summary>
        public bool? NightRequiresMorning { get; set; }

        /// <summary>عصر بعد از شب مجاز است (true = مجاز، false = ممنوع، null = تبعیت از دپارتمان).</summary>
        public bool? AllowEveningAfterNightShift { get; set; }

        /// <summary>شب بعد از شب مجاز است (true = مجاز، false = ممنوع، null = تبعیت از دپارتمان).</summary>
        public bool? AllowNightShiftAfterNightShift { get; set; }

        public List<UserPhoneNumberDtoGet>? OtherPhoneNumbers { get; set; }
        public List<UserRoleDtoGet>? UserRoles { get; set; }
    }

    public class UserDepartmentDtoGet
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public int? HospitalId { get; set; }
    }

    public class UserSpecialtyDtoGet
    {
        public int? Id { get; set; }
        public int? DepartmentId { get; set; }
        public string? SpecialtyName { get; set; }
    }

    public class UserPhoneNumberDtoGet
    {
        public int? Id { get; set; }
        public int? UserId { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UserRoleDtoGet
    {
        public int? Id { get; set; }
        public int? UserId { get; set; }
        public int? RoleId { get; set; }
        public RoleDtoGet? Role { get; set; }
    }
}

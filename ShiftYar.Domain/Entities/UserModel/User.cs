using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.SecurityModel;
using ShiftYar.Domain.Entities.ShiftModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Domain.Entities.UserModel
{
    public class User : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumberMembership { get; set; }
        public string? Password { get; set; }
        public string? NationalCode { get; set; }
        public string? PersonnelCode { get; set; }
        public UserGender? Gender { get; set; }
        public DateTime? DateOfEmployment { get; set; } // تاریخ استخدام
        public bool? IsProjectPersonnel { get; set; } // آیا پرسنل طرحی است یا خیر
        public string? Email { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public bool? IsActive { get; set; }
        public bool? CanBeShiftManager { get; set; }   //آیا میتونه مسئول شیفت باشه؟
        public bool? IncludedProductivityPlan { get; set; }  //آیا مشمول طرح بهره وری هست؟ (برای محاسبه درست ساعت شیفت شب و تعطیلات) ر
        public string? Image { get; set; }

        [ForeignKey("Department")]
        public int? DepartmentId { get; set; } // Foreign key to Department table
        public Department? Department { get; set; } // Navigation property to Department table

        [ForeignKey("Specialty")]
        public int? SpecialtyId { get; set; }    //اول باید تخصص های درون دپارتمان بیمارستان را که قبلا توسط سوپروایزر ایجاد شده بگیریم و بعد از آن، تخصص را به کاربر نسبت بدهیم
        public Specialty? Specialty { get; set; }

        //کاربر چه نوع شیفتی میدهد؟
        public ShiftTypes? ShiftType { get; set; }
        public ShiftSubTypes? ShiftSubType { get; set; }
        //اگر دو نوبت کاری هست، کدوم زوج شیفت رو قراره بیاد
        public TwoShiftRotationPattern? TwoShiftRotationPattern { get; set; }

        public List<UserPhoneNumber>? OtherPhoneNumbers { get; set; }

        //هر کاربر می تواند یک یا چند نقش داشته باشد
        public ICollection<UserRole>? UserRoles { get; set; } // Admin, User, etc.
        public ICollection<RefreshToken> RefreshTokens { get; set; }
        public ICollection<LoginHistory> LoginHistories { get; set; } // تاریخچه ورود

        public User()
        {
            this.Id = null;
            this.FullName = null;
            this.PhoneNumberMembership = null;
            this.Password = null;
            this.NationalCode = null;
            this.PersonnelCode = null;
            this.Gender = null;
            this.DateOfEmployment = null;
            this.IsProjectPersonnel = null;
            this.IsActive = null;
            this.CanBeShiftManager = null;
            this.Email = null;
            this.OtherPhoneNumbers = new List<UserPhoneNumber>();
            this.Province = null;
            this.City = null;
            this.Address = null;
            this.Image = null;
            this.UserRoles = new List<UserRole>();
            this.RefreshTokens = new List<RefreshToken>();
            this.LoginHistories = new List<LoginHistory>();
            this.DepartmentId = null;
            this.Department = null;
            this.SpecialtyId = null;
            this.Specialty = null;
            this.ShiftSubType = null;
            this.ShiftType = null;
            this.TwoShiftRotationPattern = null;
        }
    }
}

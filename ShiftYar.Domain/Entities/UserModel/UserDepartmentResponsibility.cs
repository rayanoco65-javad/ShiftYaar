using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.UserModel
{
    /// <summary>
    /// صلاحیت/مسئولیت‌های اختصاص‌یافته به یک پرسنل در بخش مربوطه
    /// </summary>
    public class UserDepartmentResponsibility : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User? User { get; set; }

        [ForeignKey("DepartmentResponsibility")]
        public int? DepartmentResponsibilityId { get; set; }
        public DepartmentResponsibility? DepartmentResponsibility { get; set; }

        public UserDepartmentResponsibility()
        {
            this.Id = null;
            this.UserId = null;
            this.User = null;
            this.DepartmentResponsibilityId = null;
            this.DepartmentResponsibility = null;
        }
    }
}

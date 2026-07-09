using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.HospitalModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.DepartmentModel
{
    public class Department : BaseEntity
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }

        [ForeignKey("Hospital")]
        public int? HospitalId { get; set; }
        public Hospital? Hospital { get; set; }

        [ForeignKey("Supervisor")]
        public int? SupervisorId { get; set; }   //تعیین مسئول بخش
        public User? Supervisor { get; set; }

        public bool? IsNightLover { get; set; }  //این بخش شب دوست است یا شب گریز. برای تقسیم شیفتهای شب

        public ICollection<User>? DepartmentUsers { get; set; } // لیست کاربرانی که به این دپارتمان تعلق دارند

        public Department()
        {
            this.Id = null;
            this.Name = null;
            this.Description = null;
            this.IsActive = null;
            this.HospitalId = null;
            this.Hospital = null;
            this.SupervisorId = null;
            this.Supervisor = null;
            this.IsNightLover = null;
            this.DepartmentUsers = new List<User>();
        }
    }
}

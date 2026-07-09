using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.DepartmentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.HospitalModel
{
    public class Hospital : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public string? SiamCode { get; set; }
        public string? Name { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public List<HospitalPhoneNumber>? PhoneNumbers { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public string? Logo { get; set; }

        public ICollection<Department>? Departments { get; set; } // لیست دپارتمان های بیمارستان

        public Hospital()
        {
            this.Id = null;
            this.SiamCode = null;
            this.Name = null;
            this.Province = null;
            this.City = null;
            this.Address = null;
            this.PhoneNumbers = null;
            this.Email = null;
            this.Website = null;
            this.Description = null;
            this.IsActive = null;
            this.Logo = null;
            this.Departments = null;
        }
    }
}

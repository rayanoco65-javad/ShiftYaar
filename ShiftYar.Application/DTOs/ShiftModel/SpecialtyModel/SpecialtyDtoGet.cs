using ShiftYar.Application.DTOs.DepartmentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftModel.SpecialtyModel
{
    public class SpecialtyDtoGet
    {
        public int? Id { get; set; }
        public int? DepartmentId { get; set; }
        public DepartmentDto? Department { get; set; }
        public string? SpecialtyName { get; set; } // نام تخصص مثل: هوشبری، اتاق عمل، پرستاری
    }

    public class DepartmentDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public int? HospitalId { get; set; }
        public HospitalDto? Hospital { get; set; }
        public int? SupervisorId { get; set; }
        public SupervisorDto? Supervisor { get; set; }
    }
}

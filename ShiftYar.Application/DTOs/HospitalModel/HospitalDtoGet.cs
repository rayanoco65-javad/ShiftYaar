using ShiftYar.Domain.Entities.HospitalModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.HospitalModel
{
    public class HospitalDtoGet
    {
        public int? Id { get; set; }
        public string? SiamCode { get; set; }
        public string? Name { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public string? Logo { get; set; }
        public List<HospitalPhoneNumber>? PhoneNumbers { get; set; }
    }
}

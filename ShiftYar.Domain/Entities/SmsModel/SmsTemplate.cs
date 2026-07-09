using ShiftYar.Domain.Entities.BaseModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.SmsModel
{
    public class SmsTemplate : BaseEntity
    {
        [Key]
        public int? Id { get; set; }
        public string TemplateKey { get; set; } // مثل login, forgotPassword
        public string TemplateText { get; set; } // متن قالب
        public bool IsActive { get; set; } = true;
    }
}

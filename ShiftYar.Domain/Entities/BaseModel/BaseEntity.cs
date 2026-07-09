using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.BaseModel
{
    public class BaseEntity
    {
        public DateTime? CreateDate { get; set; }
        public DateTime? UpdateDate { get; set; }
        public int? TheUserId { get; set; }

        public BaseEntity()
        {
            this.CreateDate = null;
            this.UpdateDate = null;
            this.TheUserId = null;
        }
    }
}

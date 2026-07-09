using ShiftYar.Domain.Entities.BaseModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Entities.SecurityModel
{
    public class RefreshToken : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        public string? Token { get; set; }
        public DateTime? Expires { get; set; }
        public bool? IsRevoked { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User? User { get; set; }

        public RefreshToken()
        {
            this.Id = null;
            this.Token = null;
            this.Expires = null;
            this.IsRevoked = null;
            this.UserId = null;
            this.User = null;
        }
    }
}

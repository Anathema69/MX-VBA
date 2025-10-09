using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_vendor")]
    public class VendorTableDb : BaseModel
    {
        [PrimaryKey("f_vendor", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_vendorname")]
        public string VendorName { get; set; }

        [Column("f_user_id")]
        public int? UserId { get; set; }

        [Column("f_commission_rate")]
        public decimal? CommissionRate { get; set; }

        [Column("f_phone")]
        public string Phone { get; set; }

        [Column("f_email")]
        public string Email { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class VendorDb
    {
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;
        public string VendorName { get; set; }

        public static VendorDb FromUser(UserDb user)
        {
            return new VendorDb
            {
                Id = user.Id,
                VendorName = user.FullName
            };
        }
    }
}

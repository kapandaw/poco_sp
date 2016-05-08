namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Transactor")]
    public partial class Transactor
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Transactor()
        {
            Credits = new HashSet<Credit>();
        }

        public int Id { get; set; }

        [StringLength(255)]
        public string TransactorName { get; set; }

        [Required]
        [StringLength(75)]
        public string Type { get; set; }

        [Required]
        [StringLength(75)]
        public string State { get; set; }

        public decimal? Amount { get; set; }

        [Column(TypeName = "numeric")]
        public decimal? Amount2 { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime ModifiedDate { get; set; }

        [Required]
        [StringLength(50)]
        public string CreatedUser { get; set; }

        [Required]
        [StringLength(50)]
        public string ModifiedUser { get; set; }

        [Timestamp]
        [Column(TypeName = "timestamp")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [MaxLength(8)]
        public byte[] timestamp { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Credit> Credits { get; set; }

        public virtual Type Type1 { get; set; }
    }
}

namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Sca.Credit")]
    public partial class Credit
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Key]
        public Guid Uid { get; set; }

        public int TransactorId { get; set; }

        [Required]
        [StringLength(255)]
        public string CreditName { get; set; }

        [Required]
        [StringLength(75)]
        public string Type { get; set; }

        [Required]
        [StringLength(75)]
        public string State { get; set; }

        [Column(TypeName = "money")]
        public decimal? AmountSum { get; set; }

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

        public virtual Transactor Transactor { get; set; }
    }
}

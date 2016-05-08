namespace Test
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public partial class Rmc : DbContext
    {
        public Rmc()
            : base("name=Rmc")
        {
        }

        public virtual DbSet<Transactor> Transactors { get; set; }
        public virtual DbSet<Type> Types { get; set; }
        public virtual DbSet<Credit> Credits { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Transactor>()
                .Property(e => e.TransactorName)
                .IsUnicode(false);

            modelBuilder.Entity<Transactor>()
                .Property(e => e.Type)
                .IsUnicode(false);

            modelBuilder.Entity<Transactor>()
                .Property(e => e.State)
                .IsUnicode(false);

            modelBuilder.Entity<Transactor>()
                .Property(e => e.Amount2)
                .HasPrecision(22, 4);

            modelBuilder.Entity<Transactor>()
                .Property(e => e.CreatedUser)
                .IsUnicode(false);

            modelBuilder.Entity<Transactor>()
                .Property(e => e.ModifiedUser)
                .IsUnicode(false);

            modelBuilder.Entity<Transactor>()
                .Property(e => e.timestamp)
                .IsFixedLength();

            modelBuilder.Entity<Transactor>()
                .HasMany(e => e.Credits)
                .WithRequired(e => e.Transactor)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Type>()
                .Property(e => e.Name)
                .IsUnicode(false);

            modelBuilder.Entity<Type>()
                .HasMany(e => e.Transactors)
                .WithRequired(e => e.Type1)
                .HasForeignKey(e => e.Type)
                .WillCascadeOnDelete(false);
                

            modelBuilder.Entity<Credit>()
                .Property(e => e.CreditName)
                .IsUnicode(false);

            modelBuilder.Entity<Credit>()
                .Property(e => e.Type)
                .IsUnicode(false);

            modelBuilder.Entity<Credit>()
                .Property(e => e.State)
                .IsUnicode(false);

            modelBuilder.Entity<Credit>()
                .Property(e => e.AmountSum)
                .HasPrecision(19, 4);

            modelBuilder.Entity<Credit>()
                .Property(e => e.CreatedUser)
                .IsUnicode(false);

            modelBuilder.Entity<Credit>()
                .Property(e => e.ModifiedUser)
                .IsUnicode(false);

            modelBuilder.Entity<Credit>()
                .Property(e => e.timestamp)
                .IsFixedLength();

            modelBuilder.HasDefaultSchema("EF");
            modelBuilder.Types().Configure(x => x.MapToStoredProcedures());
        }
    }
}

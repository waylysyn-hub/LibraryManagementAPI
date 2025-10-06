using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations
{
    public class BookConfiguration : IEntityTypeConfiguration<Book>
    {
        public void Configure(EntityTypeBuilder<Book> b)
        {
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Author).IsRequired().HasMaxLength(150);
            b.Property(x => x.ISBN).IsRequired().HasMaxLength(32);

            b.HasIndex(x => x.Title);
            b.HasIndex(x => x.Author);
            b.HasIndex(x => x.ISBN).IsUnique(); // يمنع تكرار ISBN
        }
    }
}

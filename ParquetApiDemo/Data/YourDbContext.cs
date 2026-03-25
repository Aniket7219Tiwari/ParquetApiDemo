using Microsoft.EntityFrameworkCore;
//using ParquetApiDemo.Models;

namespace ParquetApiDemo.Data
{
    public class YourDbContext : DbContext
    {
        public YourDbContext(DbContextOptions<YourDbContext> options) : base(options) { }

       // public DbSet<YourEntity> YourEntities { get; set; }
        //public DbSet<DynamicEntity> DynamicEntities { get; set; }

    }
}
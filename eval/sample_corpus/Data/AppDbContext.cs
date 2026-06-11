using Microsoft.EntityFrameworkCore;

namespace SampleShop.Data;

// Entity Framework Core database context. Defines the entity sets (tables)
// and the model configuration used to talk to the PostgreSQL database.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();

    // Configures table mappings, keys, and relationships between entities.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasKey(u => u.Username);
        modelBuilder.Entity<Order>().HasKey(o => o.Id);
        modelBuilder.Entity<Order>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.CustomerUsername);
    }
}

public class User
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
}

public class Order
{
    public int Id { get; set; }
    public string CustomerUsername { get; set; } = "";
    public decimal Total { get; set; }
}

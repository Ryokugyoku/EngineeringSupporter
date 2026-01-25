using EngineeringSupporter.DB.Entity.Todo;
using Microsoft.EntityFrameworkCore;

namespace EngineeringSupporter.DB;

public class AppDbContext : DbContext
{
    public DbSet<CategoryEntity> CategoryEntities => Set<CategoryEntity>();
    public DbSet<IssueEntity> IssueEntities => Set<IssueEntity>();
    public DbSet<StatusEntity> StatusEntities => Set<StatusEntity>();
    public DbSet<TaskEntity> TaskEntities => Set<TaskEntity>();
    public DbSet<TaskProgressManagementEntity> TaskProgressManagementEntities => Set<TaskProgressManagementEntity>();
    public DbSet<UserEntity> UserEntities => Set<UserEntity>();
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CategoryEntity>().HasKey(entity => entity.CategoryId);
        modelBuilder.Entity<IssueEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<StatusEntity>().HasKey(entity => entity.StatusId);
        modelBuilder.Entity<TaskEntity>().HasKey(entity => entity.TaskId);
        modelBuilder.Entity<TaskProgressManagementEntity>().HasKey(entity => entity.TaskProgressManagementId);
        modelBuilder.Entity<UserEntity>().HasKey(entity => entity.Id);
    }   
}

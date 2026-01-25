using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EngineeringSupporter.DB.Entity.Todo;

public class IssueEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string IssueName { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public StatusEntity StatusEntity { get; set; } = null!;
    
    public int UserId { get; set; }
    public UserEntity UserEntity { get; set; } = null!;
    public List<TaskEntity> TaskEntities { get; set; } = new();
    public DateOnly PlanStartDate { get; set; }
    public DateOnly PlanEndDate { get; set; }
    public DateOnly PredictionEndDate { get; set; }
}

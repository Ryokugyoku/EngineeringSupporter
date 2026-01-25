using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EngineeringSupporter.DB.Entity.Todo;

/// <summary>
/// タスクのエンティティクラス
/// Issueから派生した作業名称を登録するためのEntity
/// </summary>
public class TaskEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TaskId { get; set; }
    public int? IssueId { get; set; }
    public int? CategoryId { get; set; }
    public int? UserId { get; set; }
    public int? StatusId { get; set; }
    public IssueEntity? IssueEntity { get; set; }
    public CategoryEntity? CategoryEntity { get; set; }
    public UserEntity? UserEntity { get; set; }
    public List<TaskProgressManagementEntity> TaskProgressManagementEntities { get; set; } = new();
    
    public string TaskName { get; set; } = string.Empty;
    
    public StatusEntity? StatusEntity { get; set; }
    
    public DateOnly PlanStartDate { get; set; }
    public DateOnly PlanEndDate { get; set; }
    public DateOnly PredictionEndDate { get; set; }
    
}

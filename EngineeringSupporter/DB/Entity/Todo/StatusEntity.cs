using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EngineeringSupporter.DB.Entity.Todo;

public class StatusEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public List<IssueEntity> IssueEntities { get; set; } = new();
    public List<TaskEntity> TaskEntities { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EngineeringSupporter.DB.Entity.Todo;

public class CategoryEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public List<TaskEntity> TaskEntities { get; set; } = new();
}

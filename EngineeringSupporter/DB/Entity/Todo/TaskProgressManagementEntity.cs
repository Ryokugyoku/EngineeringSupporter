using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EngineeringSupporter.DB.Entity.Todo;

/// <summary>
/// タスクの進捗の履歴を管理するテーブル
/// 利用用としては、進捗の推移を元に同じペースで進んだ際の予測を立てるために活用する
/// </summary>
public class TaskProgressManagementEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TaskProgressManagementId { get; set; }
    public int TaskId { get; set; }
    public TaskEntity TaskEntity { get; set; } = null!;
    public double Progress { get; set; }
    public DateOnly ProgressDate { get; set; }
}

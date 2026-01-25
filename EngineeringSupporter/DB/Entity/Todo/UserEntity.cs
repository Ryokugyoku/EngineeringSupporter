using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EngineeringSupporter.DB.Entity.Todo;

public class UserEntity
{
    /// <summary>
    /// ユーザを一位にするためのID
    /// 画面側では表示しない
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    /// <summary>
    /// ユーザ名を定義する
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public List<IssueEntity> IssueEntities { get; set; } = new();
    public List<TaskEntity> TaskEntities { get; set; } = new();
}

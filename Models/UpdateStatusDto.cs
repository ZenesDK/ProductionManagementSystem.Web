namespace ProductionManagementSystem.Web.Models;

/// <summary>
/// DTO для обновления статуса производственной линии
/// </summary>
public class UpdateStatusDto
{
    /// <summary>
    /// Новый статус: "Active" или "Stopped"
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
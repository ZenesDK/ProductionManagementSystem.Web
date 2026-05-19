namespace ProductionManagementSystem.Web.Models;

/// <summary>
/// DTO для обновления прогресса выполнения заказа
/// </summary>
public class UpdateProgressDto
{
    /// <summary>
    /// Процент выполнения (0-100)
    /// </summary>
    public int Percent { get; set; }
}
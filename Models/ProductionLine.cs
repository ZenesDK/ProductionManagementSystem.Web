#nullable enable
using System.ComponentModel.DataAnnotations;

namespace ProductionManagementSystem.Web.Models;

public class ProductionLine
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Status { get; set; } = "Stopped"; // "Active" или "Stopped"

    /// <summary>
    /// Коэффициент эффективности (0.5 - 2.0)
    /// </summary>
    public float EfficiencyFactor { get; set; } = 1.0f;

    // Внешний ключ на текущий заказ (может быть null)
    public int? CurrentWorkOrderId { get; set; }

    // Навигационное свойство
    public WorkOrder? CurrentWorkOrder { get; set; }
    public List<WorkOrder> AssignedWorkOrders { get; set; } = new();
}
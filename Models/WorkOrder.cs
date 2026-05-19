#nullable enable
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace ProductionManagementSystem.Web.Models;

public class WorkOrder
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? ProductionLineId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть положительным числом.")]
    public int Quantity { get; set; }
    public DateTime StartDate { get; set; }
    [BindNever]
    public DateTime EstimatedEndDate { get; set; }
    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    // Новое поле для прогресса
    public int Progress { get; set; } = 0; // Инициализируем нулём

    // Навигационные свойства
    public Product? Product { get; set; }
    public ProductionLine? ProductionLine { get; set; }
}
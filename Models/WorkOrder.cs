#nullable enable
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding; // <-- КРИТИЧНО: для [BindNever]

namespace ProductionManagementSystem.Web.Models;

public class WorkOrder
{
    public int Id { get; set; }

    // === ВНЕШНИЕ КЛЮЧИ (привязываются из формы) ===
    public int ProductId { get; set; }
    public int? ProductionLineId { get; set; }

    // === НАВИГАЦИОННЫЕ СВОЙСТВА (игнорируются при привязке) ===
    [BindNever]
    public Product? Product { get; set; }  // <-- Обратите внимание: Product? (nullable)

    [BindNever]
    public ProductionLine? ProductionLine { get; set; }

    // === ОСТАЛЬНЫЕ ПОЛЯ ===
    public int Quantity { get; set; }

    public DateTime StartDate { get; set; }

    [BindNever]
    public DateTime EstimatedEndDate { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "Pending";
}
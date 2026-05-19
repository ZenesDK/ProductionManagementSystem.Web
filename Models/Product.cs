#nullable enable
using System.ComponentModel.DataAnnotations;

namespace ProductionManagementSystem.Web.Models;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Поле 'Название' обязательно для заполнения.")]
    [StringLength(200, ErrorMessage = "Длина поля 'Название' не должна превышать 200 символов.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Длина поля 'Описание' не должна превышать 1000 символов.")]
    public string? Description { get; set; }

    [StringLength(1000, ErrorMessage = "Длина поля 'Технические характеристики' не должна превышать 1000 символов.")]
    public string? Specifications { get; set; }

    [StringLength(100, ErrorMessage = "Длина поля 'Категория' не должна превышать 100 символов.")]
    public string? Category { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Поле 'Минимальный запас' должно быть неотрицательным числом.")]
    public int MinimalStock { get; set; } = 0;

    [Range(0, int.MaxValue, ErrorMessage = "Поле 'Время производства' должно быть неотрицательным числом.")]
    public int ProductionTimePerUnit { get; set; }

    public List<ProductMaterial> MaterialsNeeded { get; set; } = new();
    public List<WorkOrder> WorkOrders { get; set; } = new();
}
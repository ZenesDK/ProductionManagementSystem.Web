#nullable enable
using System.ComponentModel.DataAnnotations;

namespace ProductionManagementSystem.Web.Models;

public class Material
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Поле 'Название' обязательно для заполнения.")]
    [StringLength(200, ErrorMessage = "Длина названия не должна превышать 200 символов.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Количество должно быть неотрицательным числом.")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "Поле 'Единицы измерения' обязательно для заполнения.")]
    [StringLength(20, ErrorMessage = "Длина поля не должна превышать 20 символов.")]
    public string UnitOfMeasure { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Минимальный запас должен быть неотрицательным числом.")]
    public decimal MinimalStock { get; set; }

    public List<ProductMaterial> ProductsUsedIn { get; set; } = new();
}
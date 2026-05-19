#nullable enable
// using System.ComponentModel.DataAnnotations.Schema; // Убираем, если не используется для других целей
// using System.ComponentModel.DataAnnotations; // Убираем, если не используется для других целей

namespace ProductionManagementSystem.Web.Models;

// Убираем [PrimaryKey(nameof(ProductId), nameof(MaterialId))] - не поддерживается в текущей версии
public class ProductMaterial
{
    public int ProductId { get; set; }
    // Убираем [ForeignKey("ProductId")] - EF Core сам определит связь по соглашению
    public Product Product { get; set; } = null!; // Инициализируется EF

    public int MaterialId { get; set; }
    // Убираем [ForeignKey("MaterialId")] - EF Core сам определит связь по соглашению
    public Material Material { get; set; } = null!; // Инициализируется EF

    /// <summary>
    /// Необходимое количество материала для производства одной единицы продукта.
    /// </summary>
    public decimal QuantityNeeded { get; set; }
}
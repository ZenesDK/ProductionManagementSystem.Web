namespace ProductionManagementSystem.Web.Models;

/// <summary>
/// ViewModel для форм создания/редактирования продукта
/// </summary>
public class ProductFormViewModel
{
    public Product Product { get; set; } = null!;
    public List<Material> AvailableMaterials { get; set; } = new();
    public List<ProductMaterialViewModel> SelectedMaterials { get; set; } = new();
}

/// <summary>
/// ViewModel для отображения материала в форме продукта
/// </summary>
public class ProductMaterialViewModel
{
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal QuantityNeeded { get; set; }
}
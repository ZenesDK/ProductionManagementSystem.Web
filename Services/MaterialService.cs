using Microsoft.EntityFrameworkCore;
using ProductionManagementSystem.Web.Data;
using ProductionManagementSystem.Web.Models;

namespace ProductionManagementSystem.Web.Services;

public class MaterialService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaterialService> _logger;

    public MaterialService(ApplicationDbContext context, ILogger<MaterialService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Проверяет, достаточно ли материалов для выполнения заказа
    /// </summary>
    public async Task<(bool HasEnough, string Message)> CheckMaterialsAsync(WorkOrder order)
    {
        if (order.Product == null)
        {
            order.Product = await _context.Products
                .Include(p => p.MaterialsNeeded)
                .ThenInclude(pm => pm.Material)
                .FirstOrDefaultAsync(p => p.Id == order.ProductId);
        }

        if (order.Product?.MaterialsNeeded == null || !order.Product.MaterialsNeeded.Any())
        {
            return (true, "Для этого продукта не требуются материалы");
        }

        var insufficientMaterials = new List<string>();

        foreach (var materialNeeded in order.Product.MaterialsNeeded)
        {
            var totalNeeded = materialNeeded.QuantityNeeded * order.Quantity;
            var currentStock = await _context.Materials
                .Where(m => m.Id == materialNeeded.MaterialId)
                .Select(m => m.Quantity)
                .FirstOrDefaultAsync();

            if (currentStock < totalNeeded)
            {
                var material = await _context.Materials.FindAsync(materialNeeded.MaterialId);
                insufficientMaterials.Add($"{material?.Name}: нужно {totalNeeded} {material?.UnitOfMeasure}, есть {currentStock}");
            }
        }

        if (insufficientMaterials.Any())
        {
            return (false, $"Недостаточно материалов:\n{string.Join("\n", insufficientMaterials)}");
        }

        return (true, "Материалов достаточно");
    }

    /// <summary>
    /// Списывает материалы для выполнения заказа
    /// </summary>
    public async Task<bool> DeductMaterialsAsync(WorkOrder order)
    {
        try
        {
            if (order.Product == null)
            {
                order.Product = await _context.Products
                    .Include(p => p.MaterialsNeeded)
                    .ThenInclude(pm => pm.Material)
                    .FirstOrDefaultAsync(p => p.Id == order.ProductId);
            }

            if (order.Product?.MaterialsNeeded == null || !order.Product.MaterialsNeeded.Any())
            {
                _logger.LogInformation($"Заказ #{order.Id}: нет материалов для списания");
                return true;
            }

            foreach (var materialNeeded in order.Product.MaterialsNeeded)
            {
                var totalNeeded = materialNeeded.QuantityNeeded * order.Quantity;
                var material = await _context.Materials.FindAsync(materialNeeded.MaterialId);
                
                if (material == null)
                {
                    _logger.LogError($"Материал {materialNeeded.MaterialId} не найден");
                    return false;
                }

                if (material.Quantity < totalNeeded)
                {
                    _logger.LogWarning($"Недостаточно материала {material.Name}: нужно {totalNeeded}, есть {material.Quantity}");
                    return false;
                }

                material.Quantity -= totalNeeded;
                _logger.LogInformation($"Списано {totalNeeded} {material.UnitOfMeasure} материала {material.Name} для заказа #{order.Id}");
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при списании материалов для заказа #{order.Id}");
            return false;
        }
    }

    /// <summary>
    /// Возвращает материалы при отмене заказа
    /// </summary>
    public async Task<bool> ReturnMaterialsAsync(WorkOrder order)
    {
        try
        {
            if (order.Product == null)
            {
                order.Product = await _context.Products
                    .Include(p => p.MaterialsNeeded)
                    .ThenInclude(pm => pm.Material)
                    .FirstOrDefaultAsync(p => p.Id == order.ProductId);
            }

            if (order.Product?.MaterialsNeeded == null || !order.Product.MaterialsNeeded.Any())
            {
                _logger.LogInformation($"Заказ #{order.Id}: нет материалов для возврата");
                return true;
            }

            foreach (var materialNeeded in order.Product.MaterialsNeeded)
            {
                var totalToReturn = materialNeeded.QuantityNeeded * order.Quantity;
                var material = await _context.Materials.FindAsync(materialNeeded.MaterialId);
                
                if (material == null)
                {
                    _logger.LogError($"Материал {materialNeeded.MaterialId} не найден");
                    continue;
                }

                material.Quantity += totalToReturn;
                _logger.LogInformation($"Возвращено {totalToReturn} {material.UnitOfMeasure} материала {material.Name} для заказа #{order.Id}");
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при возврате материалов для заказа #{order.Id}");
            return false;
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionManagementSystem.Web.Data;
using ProductionManagementSystem.Web.Models;

namespace ProductionManagementSystem.Web.Controllers;

public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Products
    public async Task<IActionResult> Index(string category = "", string searchString = "")
    {
        IQueryable<Product> query = _context.Products
            .Include(p => p.MaterialsNeeded)
            .ThenInclude(pm => pm.Material);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrEmpty(searchString))
            query = query.Where(p => p.Name.Contains(searchString));

        var products = await query.OrderBy(p => p.Name).ToListAsync();
        return View(products);
    }

    // GET: Products/Create
    public async Task<IActionResult> Create()
    {
        var viewModel = new ProductFormViewModel
        {
            Product = new Product(),
            AvailableMaterials = await _context.Materials.ToListAsync(),
            SelectedMaterials = new List<ProductMaterialViewModel>()
        };
        return View(viewModel);
    }

    // POST: Products/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel viewModel,
        [FromForm] string selectedMaterialIds,
        [FromForm] string materialQuantities)
    {
        if (ModelState.IsValid)
        {
            var product = viewModel.Product;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Привязка материалов
            if (!string.IsNullOrEmpty(selectedMaterialIds))
            {
                var ids = selectedMaterialIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse).ToList();
                var quantities = !string.IsNullOrEmpty(materialQuantities)
                    ? materialQuantities.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(decimal.Parse).ToList()
                    : new List<decimal>();

                for (int i = 0; i < ids.Count; i++)
                {
                    if (quantities.Count > i && quantities[i] > 0)
                    {
                        _context.ProductMaterials.Add(new ProductMaterial
                        {
                            ProductId = product.Id,
                            MaterialId = ids[i],
                            QuantityNeeded = quantities[i]
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        viewModel.AvailableMaterials = await _context.Materials.ToListAsync();
        return View(viewModel);
    }

    // GET: Products/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products
            .Include(p => p.MaterialsNeeded)
            .ThenInclude(pm => pm.Material)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return NotFound();

        var viewModel = new ProductFormViewModel
        {
            Product = product,
            AvailableMaterials = await _context.Materials.ToListAsync(),
            SelectedMaterials = product.MaterialsNeeded.Select(pm => new ProductMaterialViewModel
            {
                MaterialId = pm.MaterialId,
                MaterialName = pm.Material.Name,
                QuantityNeeded = pm.QuantityNeeded
            }).ToList()
        };
        return View(viewModel);
    }

    // POST: Products/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel viewModel,
        [FromForm] string selectedMaterialIds,
        [FromForm] string materialQuantities)
    {
        if (id != viewModel.Product.Id) return NotFound();

        if (ModelState.IsValid)
        {
            var product = await _context.Products
                .Include(p => p.MaterialsNeeded)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            // Обновление основных полей
            product.Name = viewModel.Product.Name;
            product.Description = viewModel.Product.Description;
            product.Specifications = viewModel.Product.Specifications;
            product.Category = viewModel.Product.Category;
            product.MinimalStock = viewModel.Product.MinimalStock;
            product.ProductionTimePerUnit = viewModel.Product.ProductionTimePerUnit;

            // Обновление материалов
            var existingLinks = product.MaterialsNeeded.ToList();
            
            var newMaterialIds = string.IsNullOrEmpty(selectedMaterialIds)
                ? new List<int>()
                : selectedMaterialIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse).ToList();

            // Удаляем связи, которые больше не выбраны
            foreach (var link in existingLinks)
            {
                if (!newMaterialIds.Contains(link.MaterialId))
                {
                    _context.ProductMaterials.Remove(link);
                }
            }

            // Добавляем/обновляем выбранные связи
            var quantities = !string.IsNullOrEmpty(materialQuantities)
                ? materialQuantities.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(decimal.Parse).ToList()
                : new List<decimal>();

            for (int i = 0; i < newMaterialIds.Count; i++)
            {
                var existing = existingLinks.FirstOrDefault(l => l.MaterialId == newMaterialIds[i]);
                if (existing != null)
                {
                    existing.QuantityNeeded = quantities.Count > i ? quantities[i] : existing.QuantityNeeded;
                }
                else if (quantities.Count > i && quantities[i] > 0)
                {
                    _context.ProductMaterials.Add(new ProductMaterial
                    {
                        ProductId = product.Id,
                        MaterialId = newMaterialIds[i],
                        QuantityNeeded = quantities[i]
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        viewModel.AvailableMaterials = await _context.Materials.ToListAsync();
        return View(viewModel);
    }

    // GET: Products/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        return View(product);
    }

    // POST: Products/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            // Удаляем связанные материалы
            var productMaterials = _context.ProductMaterials.Where(pm => pm.ProductId == id);
            _context.ProductMaterials.RemoveRange(productMaterials);
            
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
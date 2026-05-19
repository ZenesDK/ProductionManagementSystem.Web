using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionManagementSystem.Web.Data;
using ProductionManagementSystem.Web.Models;

namespace ProductionManagementSystem.Web.Controllers;

public class MaterialsController : Controller
{
    private readonly ApplicationDbContext _context;

    public MaterialsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Materials
    public async Task<IActionResult> Index(string low_stock = "")
    {
        IQueryable<Material> query = _context.Materials;

        bool shouldFilterLowStock = (low_stock == "true");
        if (shouldFilterLowStock)
        {
            query = query.Where(m => m.Quantity <= m.MinimalStock);
        }

        ViewBag.LowStock = shouldFilterLowStock;
        var materials = await query.OrderBy(m => m.Name).ToListAsync();
        return View(materials);
    }

    // GET: Materials/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Materials/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Quantity,UnitOfMeasure,MinimalStock")] Material material)
    {
        // Проверка на дубликат названия (регистронезависимое)
        var existingMaterial = await _context.Materials
            .FirstOrDefaultAsync(m => m.Name.ToLower() == material.Name.ToLower());
        
        if (existingMaterial != null)
        {
            ModelState.AddModelError("Name", "Материал с таким названием уже существует.");
        }

        if (ModelState.IsValid)
        {
            _context.Add(material);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(material);
    }

    // GET: Materials/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var material = await _context.Materials.FindAsync(id);
        if (material == null) return NotFound();

        return View(material);
    }

    // POST: Materials/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Quantity,UnitOfMeasure,MinimalStock")] Material material)
    {
        if (id != material.Id) return NotFound();

        // Проверка на дубликат названия (регистронезависимое, исключая текущий материал)
        var existingMaterial = await _context.Materials
            .FirstOrDefaultAsync(m => m.Name.ToLower() == material.Name.ToLower() && m.Id != id);
        
        if (existingMaterial != null)
        {
            ModelState.AddModelError("Name", "Материал с таким названием уже существует.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(material);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Materials.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(material);
    }

    // GET: Materials/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == id);
        if (material == null) return NotFound();

        return View(material);
    }

    // POST: Materials/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var material = await _context.Materials.FindAsync(id);
        if (material != null)
        {
            _context.Materials.Remove(material);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // POST: Materials/AddStock/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStock(int id, [FromBody] AddStockDto requestData)
    {
        if (requestData == null || requestData.Amount <= 0)
            return BadRequest("Amount must be a positive number.");

        var material = await _context.Materials.FindAsync(id);
        if (material == null) return NotFound();

        material.Quantity += requestData.Amount;
        await _context.SaveChangesAsync();

        return Ok(new { newQuantity = material.Quantity });
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductionManagementSystem.Web.Data;
using ProductionManagementSystem.Web.Models;
using ProductionManagementSystem.Web.Services; 

namespace ProductionManagementSystem.Web.Controllers;

public class ProductionLinesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductionLinesController> _logger;
    private readonly MaterialService _materialService;

    public ProductionLinesController(
        ApplicationDbContext context, 
        ILogger<ProductionLinesController> logger,
        MaterialService materialService)
    {
        _context = context;
        _logger = logger;
        _materialService = materialService;
    }

    // GET: ProductionLines
    public async Task<IActionResult> Index(string available = "")
    {
        IQueryable<ProductionLine> query = _context.ProductionLines
            .Include(pl => pl.CurrentWorkOrder)
            .ThenInclude(wo => wo.Product);

        bool shouldFilterAvailable = (available == "true");
        if (shouldFilterAvailable)
        {
            query = query.Where(pl => pl.Status == "Active");
        }

        ViewBag.Available = shouldFilterAvailable;
        var productionLines = await query.OrderBy(pl => pl.Name).ToListAsync();
        return View(productionLines);
    }

    // GET: ProductionLines/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: ProductionLines/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,EfficiencyFactor")] ProductionLine productionLine)
    {
        // Проверка на дубликат названия (без учёта регистра)
        if (await _context.ProductionLines.AnyAsync(p => p.Name.ToLower() == productionLine.Name.ToLower()))
        {
            ModelState.AddModelError("Name", "Производственная линия с таким названием уже существует.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                productionLine.Status = "Stopped";
                _context.Add(productionLine);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message.Contains("UNIQUE constraint failed: ProductionLines.Name") == true)
                {
                    ModelState.AddModelError("Name", "Производственная линия с таким названием уже существует.");
                }
                else
                {
                    ModelState.AddModelError("", "Произошла ошибка при сохранении данных. Попробуйте ещё раз.");
                    _logger.LogError(ex, "Ошибка при создании производственной линии");
                }
            }
        }
        
        return View(productionLine);
    }

    // GET: ProductionLines/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var productionLine = await _context.ProductionLines.FindAsync(id);
        if (productionLine == null) return NotFound();

        return View(productionLine);
    }

    // POST: ProductionLines/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Status,EfficiencyFactor")] ProductionLine productionLine)
    {
        if (id != productionLine.Id) return NotFound();

        // Проверка на дубликат названия (без учёта регистра, исключая текущую линию)
        if (await _context.ProductionLines.AnyAsync(p => p.Name.ToLower() == productionLine.Name.ToLower() && p.Id != id))
        {
            ModelState.AddModelError("Name", "Производственная линия с таким названием уже существует.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(productionLine);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.ProductionLines.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                throw;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message.Contains("UNIQUE constraint failed: ProductionLines.Name") == true)
                {
                    ModelState.AddModelError("Name", "Производственная линия с таким названием уже существует.");
                }
                else
                {
                    ModelState.AddModelError("", "Произошла ошибка при сохранении данных. Попробуйте ещё раз.");
                    _logger.LogError(ex, "Ошибка при обновлении производственной линии");
                }
            }
        }
        
        return View(productionLine);
    }

    // GET: ProductionLines/Delete/5
    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var productionLine = await _context.ProductionLines.FirstOrDefaultAsync(p => p.Id == id);
        if (productionLine == null) return NotFound();

        return View(productionLine);
    }

    // POST: ProductionLines/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var productionLine = await _context.ProductionLines.FindAsync(id);
        if (productionLine != null)
        {
            // Отвязываем все заказы от удаляемой линии
            var orders = _context.WorkOrders.Where(wo => wo.ProductionLineId == id);
            foreach (var order in orders)
            {
                order.ProductionLineId = null;
            }

            _context.ProductionLines.Remove(productionLine);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // GET: ProductionLines/Schedule/5
    public async Task<IActionResult> Schedule(int? id)
    {
        if (id == null) return NotFound();

        var productionLine = await _context.ProductionLines
            .Include(pl => pl.AssignedWorkOrders)
            .ThenInclude(wo => wo.Product)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (productionLine == null) return NotFound();

        ViewBag.AvailableOrders = await _context.WorkOrders
            .Where(wo => wo.ProductionLineId == null && wo.Status == "Pending")
            .Include(wo => wo.Product)
            .ToListAsync();

        return View(productionLine);
    }

    // POST: ProductionLines/AssignOrder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignOrder(int productionLineId, int workOrderId)
    {
        _logger.LogInformation($"AssignOrder called: productionLineId={productionLineId}, workOrderId={workOrderId}");

        var line = await _context.ProductionLines.FindAsync(productionLineId);
        var order = await _context.WorkOrders
            .Include(wo => wo.Product)
            .ThenInclude(p => p.MaterialsNeeded)
            .ThenInclude(pm => pm.Material)
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);

        if (line == null || order == null)
        {
            return NotFound();
        }

        // Проверка материалов через сервис
        var (hasEnough, message) = await _materialService.CheckMaterialsAsync(order);
        
        if (!hasEnough)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction(nameof(Schedule), new { id = productionLineId });
        }

        // Списываем материалы
        var deductSuccess = await _materialService.DeductMaterialsAsync(order);
        if (!deductSuccess)
        {
            TempData["ErrorMessage"] = "Ошибка при списании материалов. Попробуйте ещё раз.";
            return RedirectToAction(nameof(Schedule), new { id = productionLineId });
        }

        // Назначаем заказ на линию
        order.ProductionLineId = productionLineId;
        order.Status = "InProgress";
        line.CurrentWorkOrderId = order.Id;
        line.Status = "Active";
        
        await _context.SaveChangesAsync();
        
        TempData["SuccessMessage"] = $"Заказ #{order.Id} запущен. Материалы списаны.";
        return RedirectToAction(nameof(Schedule), new { id = productionLineId });
    }

    // POST: ProductionLines/UpdateStatus/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto requestData)
    {
        if (requestData == null || string.IsNullOrEmpty(requestData.Status))
            return BadRequest("Status is required.");

        string status = requestData.Status;
        if (status != "Active" && status != "Stopped")
            return BadRequest("Invalid status. Expected 'Active' or 'Stopped'.");

        var line = await _context.ProductionLines.FindAsync(id);
        if (line == null) return NotFound();

        if (status == "Active" && line.CurrentWorkOrderId.HasValue)
        {
            var order = await _context.WorkOrders
                .Include(wo => wo.Product)
                .ThenInclude(p => p.MaterialsNeeded)
                .ThenInclude(pm => pm.Material)
                .FirstOrDefaultAsync(wo => wo.Id == line.CurrentWorkOrderId);

            if (order != null && order.Status == "Pending")
            {
                order.Status = "InProgress";
                _context.Update(order);
            }
        }

        line.Status = status;
        _context.Update(line);
        await _context.SaveChangesAsync();

        return Ok(new { newStatus = line.Status });
    }

    // GET: ProductionLines/StartLine/5
    [HttpGet]
    public async Task<IActionResult> StartLine(int? id)
    {
        if (id == null) return NotFound();

        var line = await _context.ProductionLines
            .Include(pl => pl.CurrentWorkOrder)
            .ThenInclude(wo => wo != null ? wo.Product : null)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (line == null) return NotFound();

        // Получаем все pending заказы, которые можно запустить
        ViewBag.AvailableOrders = await _context.WorkOrders
            .Where(wo => wo.Status == "Pending" && wo.ProductionLineId == null)
            .Include(wo => wo.Product)
            .ToListAsync();

        return View(line);
    }

    // POST: ProductionLines/StartLine
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartLine(int id, int? workOrderId)
    {
        var line = await _context.ProductionLines.FindAsync(id);
        if (line == null) return NotFound();

        if (workOrderId.HasValue)
        {
            var order = await _context.WorkOrders
                .Include(wo => wo.Product)
                .ThenInclude(p => p.MaterialsNeeded)
                .ThenInclude(pm => pm.Material)
                .FirstOrDefaultAsync(wo => wo.Id == workOrderId.Value);

            if (order == null) return NotFound();

            // Проверка материалов
            var (hasEnough, message) = await _materialService.CheckMaterialsAsync(order);
            
            if (!hasEnough)
            {
                TempData["ErrorMessage"] = message;
                return RedirectToAction(nameof(StartLine), new { id = line.Id });
            }

            // Списываем материалы
            var deductSuccess = await _materialService.DeductMaterialsAsync(order);
            if (!deductSuccess)
            {
                TempData["ErrorMessage"] = "Ошибка при списании материалов.";
                return RedirectToAction(nameof(StartLine), new { id = line.Id });
            }

            order.ProductionLineId = id;
            order.Status = "InProgress";
            line.CurrentWorkOrderId = order.Id;
            line.Status = "Active";
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Заказ #{order.Id} запущен. Материалы списаны.";
        }
        else
        {
            line.Status = "Active";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Линия запущена без заказа.";
        }

        return RedirectToAction(nameof(Index));
    }
}
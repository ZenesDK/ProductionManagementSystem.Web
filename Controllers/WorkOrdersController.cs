using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionManagementSystem.Web.Data;
using ProductionManagementSystem.Web.Models;
using Microsoft.Extensions.Logging;

namespace ProductionManagementSystem.Web.Controllers;

public class WorkOrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkOrdersController> _logger; 

    public WorkOrdersController(ApplicationDbContext context, ILogger<WorkOrdersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: WorkOrders
    public async Task<IActionResult> Index(string status = "", DateTime? date = null)
    {
        IQueryable<WorkOrder> query = _context.WorkOrders
            .Include(wo => wo.Product)
            .Include(wo => wo.ProductionLine);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(wo => wo.Status == status);
        }

        if (date.HasValue)
        {
            // Filter by date (e.g., estimated end date)
            query = query.Where(wo => wo.EstimatedEndDate.Date == date.Value.Date);
        }

        var workOrders = await query.OrderBy(wo => wo.StartDate).ToListAsync();
        return View(workOrders);
    }

    // GET: WorkOrders/Create
    public async Task<IActionResult> Create()
    {
        // Load products and lines for dropdowns
        ViewBag.Products = await _context.Products.ToListAsync();
        ViewBag.Lines = await _context.ProductionLines.ToListAsync();
        return View();
    }

    // POST: WorkOrders/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ProductId,Quantity,StartDate,ProductionLineId")] WorkOrder workOrder)
    {
        // === ДЕТАЛЬНОЕ ЛОГИРОВАНИЕ ===
        _logger.LogInformation($"=== CREATE POST CALLED ===");
        _logger.LogInformation($"ProductId: {workOrder.ProductId}");
        _logger.LogInformation($"Quantity: {workOrder.Quantity}");
        _logger.LogInformation($"StartDate: {workOrder.StartDate}");
        _logger.LogInformation($"ProductionLineId: {workOrder.ProductionLineId}");
        _logger.LogInformation($"ModelState.IsValid: {ModelState.IsValid}");
        
        if (!ModelState.IsValid)
        {
            _logger.LogError($"ModelState has {ModelState.ErrorCount} errors:");
            foreach (var key in ModelState.Keys)
            {
                var state = ModelState[key];
                if (state.Errors.Count > 0)
                {
                    foreach (var error in state.Errors)
                    {
                        _logger.LogError($"  [{key}]: {error.ErrorMessage}");
                    }
                }
            }
        }
        // === КОНЕЦ ЛОГИРОВАНИЯ ===

        if (ModelState.IsValid)
        {
            // --- РАСЧЁТ EstimatedEndDate НА СЕРВЕРЕ ---
            var product = await _context.Products.FindAsync(workOrder.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("", "Выбранный продукт не найден.");
                ViewBag.Products = await _context.Products.ToListAsync();
                ViewBag.Lines = await _context.ProductionLines.ToListAsync();
                return View(workOrder);
            }

            var line = await _context.ProductionLines.FindAsync(workOrder.ProductionLineId);
            float efficiency = line?.EfficiencyFactor ?? 1.0f;

            int totalTimeMinutes = (int)Math.Ceiling((workOrder.Quantity * product.ProductionTimePerUnit) / efficiency);
            workOrder.EstimatedEndDate = workOrder.StartDate.AddMinutes(totalTimeMinutes);
            // --- КОНЕЦ РАСЧЁТА ---

            workOrder.Status = "Pending";

            // Check material sufficiency
            bool hasSufficientMaterials = true;
            foreach (var pm in product.MaterialsNeeded)
            {
                var materialInStock = await _context.Materials
                    .Where(m => m.Id == pm.MaterialId)
                    .Select(m => m.Quantity)
                    .FirstOrDefaultAsync();
                if (materialInStock < pm.QuantityNeeded * workOrder.Quantity)
                {
                    hasSufficientMaterials = false;
                    break;
                }
            }

            if (!hasSufficientMaterials)
            {
                ModelState.AddModelError("", "Недостаточно материалов для выполнения заказа.");
                ViewBag.Products = await _context.Products.ToListAsync();
                ViewBag.Lines = await _context.ProductionLines.ToListAsync();
                return View(workOrder);
            }

            _context.Add(workOrder);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Products = await _context.Products.ToListAsync();
        ViewBag.Lines = await _context.ProductionLines.ToListAsync();
        return View(workOrder);
    }

    // GET: WorkOrders/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var workOrder = await _context.WorkOrders
            .Include(wo => wo.Product)
            .Include(wo => wo.ProductionLine)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (workOrder == null) return NotFound();

        ViewBag.Products = await _context.Products.ToListAsync();
        ViewBag.Lines = await _context.ProductionLines.ToListAsync();
        return View(workOrder);
    }

    // POST: WorkOrders/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,ProductId,ProductionLineId,Quantity,StartDate,EstimatedEndDate,Status")] WorkOrder workOrder)
    {
        if (id != workOrder.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                // Recalculate EstimatedEndDate if needed based on changes
                var product = await _context.Products.FindAsync(workOrder.ProductId);
                if (product == null)
                {
                    ModelState.AddModelError("", "Выбранный продукт не найден.");
                    ViewBag.Products = await _context.Products.ToListAsync();
                    ViewBag.Lines = await _context.ProductionLines.ToListAsync();
                    return View(workOrder);
                }

                var line = await _context.ProductionLines.FindAsync(workOrder.ProductionLineId);
                float efficiency = line?.EfficiencyFactor ?? 1.0f;

                int totalTimeMinutes = (int)Math.Ceiling((workOrder.Quantity * product.ProductionTimePerUnit) / efficiency);
                workOrder.EstimatedEndDate = workOrder.StartDate.AddMinutes(totalTimeMinutes);

                _context.Update(workOrder);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.WorkOrders.Any(e => e.Id == id))
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

        ViewBag.Products = await _context.Products.ToListAsync();
        ViewBag.Lines = await _context.ProductionLines.ToListAsync();
        return View(workOrder);
    }

    // POST: WorkOrders/Cancel/5
    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await _context.WorkOrders.FindAsync(id);
        if (order == null) return NotFound();

        order.Status = "Cancelled";
        // If this was the current order on a line, clear it
        if (order.ProductionLine?.CurrentWorkOrderId == order.Id)
        {
            order.ProductionLine.CurrentWorkOrderId = null;
        }
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: WorkOrders/UpdateProgress/5
    [HttpPost]
    public async Task<IActionResult> UpdateProgress(int id, [FromBody] dynamic data)
    {
        int percent = data.percent;
        if (percent < 0 || percent > 100) return BadRequest("Percent must be between 0 and 100.");

        var order = await _context.WorkOrders.FindAsync(id);
        if (order == null) return NotFound();

        // This is a simplified progress update. In reality, progress might be tracked differently.
        // For now, we just acknowledge the update.
        // You might store actual progress percentage in the database.
        // order.ProgressPercentage = percent;
        // if (percent == 100) order.Status = "Completed";

        await _context.SaveChangesAsync();

        return Ok(new { newPercent = percent });
    }

    // GET: api/orders/{id}/details (API endpoint simulation via action)
    [HttpGet]
    public async Task<IActionResult> GetOrderDetails(int id)
    {
        var orderDetails = await _context.WorkOrders
            .Where(wo => wo.Id == id)
            .Include(wo => wo.Product)
            .ThenInclude(p => p.MaterialsNeeded)
            .ThenInclude(pm => pm.Material)
            .Include(wo => wo.ProductionLine)
            .Select(wo => new
            {
                Id = wo.Id,
                ProductName = wo.Product.Name,
                ProductDescription = wo.Product.Description,
                Quantity = wo.Quantity,
                StartDate = wo.StartDate,
                EstimatedEndDate = wo.EstimatedEndDate,
                // --- ИСПРАВЛЕНО: Заменяем switch expression на тернарные операторы ---
                Status = wo.Status == "Pending" ? "Ожидает" :
                        wo.Status == "InProgress" ? "В процессе" :
                        wo.Status == "Completed" ? "Завершён" :
                        wo.Status == "Cancelled" ? "Отменён" :
                        wo.Status, // Если статус неизвестен, возвращаем как есть
                // --- КОНЕЦ ИСПРАВЛЕНИЯ ---
                ProductionLineName = wo.ProductionLine != null ? wo.ProductionLine.Name : "Не назначена",
                MaterialsNeeded = wo.Product.MaterialsNeeded.Select(pm => new
                {
                    Name = pm.Material.Name,
                    Unit = pm.Material.UnitOfMeasure,
                    NeededPerUnit = pm.QuantityNeeded,
                    TotalNeeded = pm.QuantityNeeded * wo.Quantity
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (orderDetails == null) return NotFound();

        return Json(orderDetails);
    }
    // GET: WorkOrders/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var workOrder = await _context.WorkOrders
            .Include(w => w.Product)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (workOrder == null) return NotFound();

        return View(workOrder);
    }

    // POST: WorkOrders/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder != null)
        {
            // Если заказ был текущим на линии, сбрасываем CurrentWorkOrderId
            if (workOrder.ProductionLineId.HasValue)
            {
                var line = await _context.ProductionLines.FindAsync(workOrder.ProductionLineId.Value);
                if (line?.CurrentWorkOrderId == id)
                {
                    line.CurrentWorkOrderId = null;
                }
            }
            
            _context.WorkOrders.Remove(workOrder);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
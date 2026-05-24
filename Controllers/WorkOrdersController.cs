using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionManagementSystem.Web.Data;
using ProductionManagementSystem.Web.Models;
using Microsoft.Extensions.Logging;
using ProductionManagementSystem.Web.Services;

namespace ProductionManagementSystem.Web.Controllers;

public class WorkOrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkOrdersController> _logger;

    private readonly MaterialService _materialService;

    public WorkOrdersController(
        ApplicationDbContext context, 
        ILogger<WorkOrdersController> logger,
        MaterialService materialService) // Добавлено
    {
        _context = context;
        _logger = logger;
        _materialService = materialService;
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
            query = query.Where(wo => wo.EstimatedEndDate.Date == date.Value.Date);
        }

        var workOrders = await query.OrderBy(wo => wo.StartDate).ToListAsync();
        
        // === РАСЧЕТ ПРОГРЕССА НА ОСНОВЕ ВРЕМЕНИ ===
        var now = DateTime.Now;
        foreach (var order in workOrders)
        {
            if (order.Status == "Completed")
            {
                order.Progress = 100;
            }
            else if (order.Status == "InProgress")
            {
                var totalDuration = order.EstimatedEndDate - order.StartDate;
                if (totalDuration.TotalMinutes > 0)
                {
                    var elapsed = now - order.StartDate;
                    if (elapsed.TotalMinutes < 0) elapsed = TimeSpan.Zero;
                    
                    var percent = (elapsed.TotalMinutes / totalDuration.TotalMinutes) * 100;
                    order.Progress = (int)Math.Clamp(percent, 0, 100);
                }
            }
        }
        // ==========================================
        
        return View(workOrders);
    }

    // GET: WorkOrders/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.Products = await _context.Products.ToListAsync();
        ViewBag.Lines = await _context.ProductionLines.ToListAsync();
        return View();
    }

    // POST: WorkOrders/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ProductId,Quantity,StartDate,ProductionLineId")] WorkOrder workOrder)
    {
        if (ModelState.IsValid)
        {
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
            workOrder.Status = "Pending";
            workOrder.Progress = 0;

            // УБИРАЕМ проверку материалов здесь - она будет при запуске
            // if (!hasSufficientMaterials) ...

            _context.Add(workOrder);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Заказ #{workOrder.Id} создан. Для запуска назначьте его на линию.";
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
                workOrder.Progress = 0;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.WorkOrders.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Products = await _context.Products.ToListAsync();
        ViewBag.Lines = await _context.ProductionLines.ToListAsync();
        return View(workOrder);
    }

    // POST: WorkOrders/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await _context.WorkOrders
            .Include(wo => wo.Product)
            .ThenInclude(p => p.MaterialsNeeded)
            .ThenInclude(pm => pm.Material)
            .FirstOrDefaultAsync(wo => wo.Id == id);
        
        if (order == null) return NotFound();

        // Возвращаем материалы только если заказ был в процессе или ожидании
        if (order.Status == "InProgress" || order.Status == "Pending")
        {
            var returnSuccess = await _materialService.ReturnMaterialsAsync(order);
            if (!returnSuccess)
            {
                _logger.LogWarning($"Не удалось вернуть материалы для заказа #{id}");
                TempData["WarningMessage"] = "Заказ отменён, но возникла проблема при возврате материалов. Проверьте склад.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Заказ #{id} отменён. Материалы возвращены на склад.";
            }
        }

        order.Status = "Cancelled";
        
        // Если заказ был текущим на линии, сбрасываем CurrentWorkOrderId
        if (order.ProductionLineId.HasValue)
        {
            var line = await _context.ProductionLines.FindAsync(order.ProductionLineId.Value);
            if (line?.CurrentWorkOrderId == order.Id)
            {
                line.CurrentWorkOrderId = null;
                line.Status = "Stopped";
            }
        }
        
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: WorkOrders/UpdateProgress/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgress(int id, [FromBody] UpdateProgressDto requestData)
    {
        if (requestData == null || requestData.Percent < 0 || requestData.Percent > 100)
            return BadRequest("Percent must be between 0 and 100.");

        var order = await _context.WorkOrders.FindAsync(id);
        if (order == null) return NotFound();

        // Обновляем прогресс (если поле есть в модели)
        // order.ProgressPercentage = requestData.Percent;
        order.Progress = requestData.Percent;

        if (requestData.Percent == 100)
        {
            order.Status = "Completed";
        }

        await _context.SaveChangesAsync();
        return Ok(new { newPercent = requestData.Percent });
    }

    // GET: WorkOrders/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var order = await _context.WorkOrders
            .Include(wo => wo.Product)
            .ThenInclude(p => p.MaterialsNeeded)
            .ThenInclude(pm => pm.Material)
            .Include(wo => wo.ProductionLine)
            .FirstOrDefaultAsync(wo => wo.Id == id);

        if (order == null) return NotFound();

        return View(order);
    }

    // GET: WorkOrders/Delete/5
    [HttpGet]
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
            // Отвязка от линии при удалении
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

    // GET: api/WorkOrders/GetProgress/4
    [HttpGet]
    [Route("api/WorkOrders/GetProgress/{id}")]
    public async Task<IActionResult> GetProgress(int id)
    {
        var order = await _context.WorkOrders.FindAsync(id);
        if (order == null) return NotFound();

        return Json(new { progress = order.Progress, status = order.Status });
    }
}
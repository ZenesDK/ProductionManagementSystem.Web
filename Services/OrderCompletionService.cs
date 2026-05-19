// Services/OrderCompletionService.cs
using ProductionManagementSystem.Web.Data;
using Microsoft.EntityFrameworkCore;
public class OrderCompletionService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OrderCompletionService> _logger;

    public OrderCompletionService(IServiceProvider services, ILogger<OrderCompletionService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var now = DateTime.Now;
                var ordersToComplete = await context.WorkOrders
                    .Where(wo => wo.Status == "InProgress" && wo.EstimatedEndDate <= now)
                    .ToListAsync(stoppingToken);

                foreach (var order in ordersToComplete)
                {
                    order.Status = "Completed";
                    _logger.LogInformation($"Заказ #{order.Id} автоматически завершён");
                }

                if (ordersToComplete.Any())
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
            }

            // Проверять каждые 30 секунд
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
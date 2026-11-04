using System.Diagnostics;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    public const string OrderListCacheKey = "OrderList_CacheKey";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OrderController> _logger;

    public OrderController(
        LogiTrackContext context,
        IMemoryCache cache,
        ILogger<OrderController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    // GET: /api/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
        var stopwatch = Stopwatch.StartNew();

        if (_cache.TryGetValue(OrderListCacheKey, out List<Order>? cachedOrders) && cachedOrders is not null)
        {
            stopwatch.Stop();
            Response?.Headers.Append("X-Cache-Status", "HIT");
            Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));
            _logger.LogInformation("Orders list served from IMemoryCache in {ElapsedMs:F3} ms.", stopwatch.Elapsed.TotalMilliseconds);
            return Ok(cachedOrders);
        }

        // Eager-load Items with .Include() and disable tracking with .AsNoTracking() to avoid N+1 queries and tracking overhead
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.DatePlaced)
            .ToListAsync();

        _cache.Set(OrderListCacheKey, orders, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });

        stopwatch.Stop();
        Response?.Headers.Append("X-Cache-Status", "MISS");
        Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));
        _logger.LogInformation("Orders list queried with .Include(o => o.Items).AsNoTracking() in {ElapsedMs:F3} ms.", stopwatch.Elapsed.TotalMilliseconds);

        return Ok(orders);
    }

    // GET: /api/orders/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> GetOrderById(int id)
    {
        var stopwatch = Stopwatch.StartNew();

        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        stopwatch.Stop();
        Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));

        if (order is null)
        {
            return NotFound(new { message = $"Order with ID {id} was not found." });
        }

        return Ok(order);
    }

    // POST: /api/orders
    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] Order order)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (order.DatePlaced == default)
        {
            order.DatePlaced = DateTime.UtcNow;
        }

        var incomingItems = order.Items?.ToList() ?? new List<InventoryItem>();
        order.Items = new List<InventoryItem>();

        // Copilot query optimization: batch-load all referenced existing ItemIds in a single SQL query
        // instead of issuing N separate FindAsync() round-trips inside a loop.
        var existingIds = incomingItems
            .Where(i => i.ItemId > 0)
            .Select(i => i.ItemId)
            .Distinct()
            .ToList();

        var existingItemsById = existingIds.Count > 0
            ? await _context.InventoryItems
                .Where(i => existingIds.Contains(i.ItemId))
                .ToDictionaryAsync(i => i.ItemId)
            : new Dictionary<int, InventoryItem>();

        foreach (var item in incomingItems)
        {
            if (item.ItemId > 0 && existingItemsById.TryGetValue(item.ItemId, out var existingItem))
            {
                order.AddItem(existingItem);
            }
            else
            {
                order.AddItem(item);
            }
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Invalidate cached order and inventory lists
        _cache.Remove(OrderListCacheKey);
        _cache.Remove(InventoryController.InventoryListCacheKey);

        return CreatedAtAction(nameof(GetOrderById), new { id = order.OrderId }, order);
    }

    // DELETE: /api/orders/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order is null)
        {
            return NotFound(new { message = $"Order with ID {id} was not found." });
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        _cache.Remove(OrderListCacheKey);
        _cache.Remove(InventoryController.InventoryListCacheKey);

        return NoContent();
    }
}

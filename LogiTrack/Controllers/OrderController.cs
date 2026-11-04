using System.Diagnostics;
using System.Security.Claims;
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
    private static readonly TimeSpan ListCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SessionStateAbsoluteDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SessionStateSlidingDuration = TimeSpan.FromMinutes(5);

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

    public static string GetOrderByIdCacheKey(int id) => $"Order_{id}_CacheKey";
    public static string GetSessionCacheKey(string sessionId) => $"OrderSession_{sessionId}_CacheKey";

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
            AbsoluteExpirationRelativeToNow = ListCacheDuration
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
        var cacheKey = GetOrderByIdCacheKey(id);

        if (_cache.TryGetValue(cacheKey, out Order? cachedOrder) && cachedOrder is not null)
        {
            stopwatch.Stop();
            Response?.Headers.Append("X-Cache-Status", "HIT");
            Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));
            return Ok(cachedOrder);
        }

        // Rehydrate from SQLite database if not in memory cache (e.g., after cache expiry or server restart)
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        stopwatch.Stop();
        Response?.Headers.Append("X-Cache-Status", "REHYDRATED");
        Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));

        if (order is null)
        {
            return NotFound(new { message = $"Order with ID {id} was not found." });
        }

        _cache.Set(cacheKey, order, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionStateAbsoluteDuration,
            SlidingExpiration = SessionStateSlidingDuration
        });

        return Ok(order);
    }

    // GET: /api/orders/{id}/summary
    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<object>> GetOrderSummary(int id)
    {
        var result = await GetOrderById(id);
        if (result.Result is NotFoundObjectResult notFound)
        {
            return notFound;
        }

        var okResult = result.Result as OkObjectResult;
        var order = okResult?.Value as Order;
        if (order is null)
        {
            return NotFound(new { message = $"Order with ID {id} was not found." });
        }

        return Ok(new
        {
            order.OrderId,
            order.CustomerName,
            order.DatePlaced,
            order.SessionId,
            ItemCount = order.Items.Count,
            Summary = order.GetOrderSummary()
        });
    }

    // GET: /api/orders/session/{sessionId}
    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrdersBySession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { message = "Session ID cannot be empty." });
        }

        var stopwatch = Stopwatch.StartNew();
        var cacheKey = GetSessionCacheKey(sessionId);

        if (_cache.TryGetValue(cacheKey, out List<Order>? sessionOrders) && sessionOrders is not null)
        {
            stopwatch.Stop();
            Response?.Headers.Append("X-Cache-Status", "HIT");
            Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));
            return Ok(sessionOrders);
        }

        // Rehydrate persistent session orders from SQLite database across restarts
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.SessionId == sessionId)
            .OrderByDescending(o => o.DatePlaced)
            .ToListAsync();

        _cache.Set(cacheKey, orders, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionStateAbsoluteDuration,
            SlidingExpiration = SessionStateSlidingDuration
        });

        stopwatch.Stop();
        Response?.Headers.Append("X-Cache-Status", "REHYDRATED");
        Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));
        _logger.LogInformation("Rehydrated {Count} orders for session '{SessionId}' in {ElapsedMs:F3} ms.", orders.Count, sessionId, stopwatch.Elapsed.TotalMilliseconds);

        return Ok(orders);
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

        // Associate order with authenticated user and client session for persistent state tracking
        var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            order.CreatedByUserId = userId;
        }

        if (string.IsNullOrWhiteSpace(order.SessionId))
        {
            var headerSessionId = Request?.Headers["X-Session-Id"].FirstOrDefault();
            order.SessionId = !string.IsNullOrWhiteSpace(headerSessionId)
                ? headerSessionId
                : $"sess-{userId ?? "anon"}-{DateTime.UtcNow:yyyyMMdd}";
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

        // Populate individual order cache and invalidate aggregate/session caches
        _cache.Set(GetOrderByIdCacheKey(order.OrderId), order, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionStateAbsoluteDuration,
            SlidingExpiration = SessionStateSlidingDuration
        });
        _cache.Remove(OrderListCacheKey);
        _cache.Remove(InventoryController.InventoryListCacheKey);
        if (!string.IsNullOrWhiteSpace(order.SessionId))
        {
            _cache.Remove(GetSessionCacheKey(order.SessionId));
        }

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

        var sessionId = order.SessionId;
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        _cache.Remove(GetOrderByIdCacheKey(id));
        _cache.Remove(OrderListCacheKey);
        _cache.Remove(InventoryController.InventoryListCacheKey);
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _cache.Remove(GetSessionCacheKey(sessionId));
        }

        return NoContent();
    }
}

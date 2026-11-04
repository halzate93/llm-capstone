using System.Diagnostics;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    public const string InventoryListCacheKey = "InventoryList_CacheKey";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        LogiTrackContext context,
        IMemoryCache cache,
        ILogger<InventoryController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    // GET: /api/inventory
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventoryItems()
    {
        var stopwatch = Stopwatch.StartNew();

        if (_cache.TryGetValue(InventoryListCacheKey, out List<InventoryItem>? cachedItems) && cachedItems is not null)
        {
            stopwatch.Stop();
            Response?.Headers.Append("X-Cache-Status", "HIT");
            Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));
            _logger.LogInformation("Inventory list served from IMemoryCache in {ElapsedMs:F3} ms.", stopwatch.Elapsed.TotalMilliseconds);
            return Ok(cachedItems);
        }

        var items = await _context.InventoryItems
            .AsNoTracking()
            .OrderBy(i => i.ItemId)
            .ToListAsync();

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration)
            .SetPriority(CacheItemPriority.Normal);

        _cache.Set(InventoryListCacheKey, items, cacheOptions);

        stopwatch.Stop();
        Response?.Headers.Append("X-Cache-Status", "MISS");
        Response?.Headers.Append("X-Response-Time-Ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));
        _logger.LogInformation("Inventory list queried from SQLite and cached for 30s in {ElapsedMs:F3} ms.", stopwatch.Elapsed.TotalMilliseconds);

        return Ok(items);
    }

    // GET: /api/inventory/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InventoryItem>> GetInventoryItemById(int id)
    {
        var item = await _context.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ItemId == id);

        if (item is null)
        {
            return NotFound(new { message = $"Inventory item with ID {id} was not found." });
        }

        return Ok(item);
    }

    // POST: /api/inventory
    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<InventoryItem>> CreateInventoryItem([FromBody] InventoryItem item)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        // Invalidate cached inventory list after mutation
        _cache.Remove(InventoryListCacheKey);

        return CreatedAtAction(nameof(GetInventoryItemById), new { id = item.ItemId }, item);
    }

    // DELETE: /api/inventory/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> DeleteInventoryItem(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item is null)
        {
            return NotFound(new { message = $"Inventory item with ID {id} was not found." });
        }

        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();

        // Invalidate cached inventory list after deletion
        _cache.Remove(InventoryListCacheKey);

        return NoContent();
    }
}

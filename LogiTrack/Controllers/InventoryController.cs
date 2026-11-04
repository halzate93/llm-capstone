using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;

    public InventoryController(LogiTrackContext context)
    {
        _context = context;
    }

    // GET: /api/inventory
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventoryItems()
    {
        var items = await _context.InventoryItems.ToListAsync();
        return Ok(items);
    }

    // GET: /api/inventory/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InventoryItem>> GetInventoryItemById(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item is null)
        {
            return NotFound(new { message = $"Inventory item with ID {id} was not found." });
        }

        return Ok(item);
    }

    // POST: /api/inventory
    [HttpPost]
    public async Task<ActionResult<InventoryItem>> CreateInventoryItem([FromBody] InventoryItem item)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetInventoryItemById), new { id = item.ItemId }, item);
    }

    // DELETE: /api/inventory/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteInventoryItem(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item is null)
        {
            return NotFound(new { message = $"Inventory item with ID {id} was not found." });
        }

        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext _context;

    public OrderController(LogiTrackContext context)
    {
        _context = context;
    }

    // GET: /api/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
            .ToListAsync();

        return Ok(orders);
    }

    // GET: /api/orders/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> GetOrderById(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);

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

        foreach (var item in incomingItems)
        {
            if (item.ItemId > 0)
            {
                var existingItem = await _context.InventoryItems.FindAsync(item.ItemId);
                if (existingItem is not null)
                {
                    order.AddItem(existingItem);
                    continue;
                }
            }

            order.AddItem(item);
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrderById), new { id = order.OrderId }, order);
    }

    // DELETE: /api/orders/{id}
    [HttpDelete("{id:int}")]
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

        return NoContent();
    }
}

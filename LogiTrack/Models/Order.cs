using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;

public class Order
{
    [Key]
    public int OrderId { get; set; }

    [Required]
    [MaxLength(150)]
    public string CustomerName { get; set; } = string.Empty;

    public DateTime DatePlaced { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional session or cart identifier used to persist and rehydrate order state across client sessions.
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }

    /// <summary>
    /// Identity User ID of the authenticated user who placed or owns the order.
    /// </summary>
    [MaxLength(100)]
    public string? CreatedByUserId { get; set; }

    public List<InventoryItem> Items { get; set; } = new();

    public void AddItem(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Copilot optimization: if an item with the same positive ItemId already exists in the order,
        // aggregate the quantity instead of adding a duplicate entry.
        if (item.ItemId > 0)
        {
            var existing = Items.FirstOrDefault(i => i.ItemId == item.ItemId);
            if (existing is not null)
            {
                existing.Quantity += item.Quantity;
                return;
            }
        }

        Items.Add(item);
    }

    public bool RemoveItem(int itemId)
    {
        var index = Items.FindIndex(i => i.ItemId == itemId);
        if (index < 0)
        {
            return false;
        }

        Items.RemoveAt(index);
        return true;
    }

    public string GetOrderSummary()
    {
        return $"Order #{OrderId} for {CustomerName} | Items: {Items.Count} | Placed: {DatePlaced:M/d/yyyy}";
    }
}

using LogiTrack;
using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<LogiTrackContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=logitrack.db"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Activity 1: Seed and test the database & domain models
using (var context = new LogiTrackContext())
{
    context.Database.Migrate();

    // Add test inventory item if none exist
    if (!context.InventoryItems.Any())
    {
        context.InventoryItems.Add(new InventoryItem
        {
            Name = "Pallet Jack",
            Quantity = 12,
            Location = "Warehouse A"
        });

        context.SaveChanges();
    }

    // Retrieve and print inventory to confirm
    var items = context.InventoryItems.AsNoTracking().ToList();
    foreach (var item in items)
    {
        item.DisplayInfo(); // Prints: Item: Pallet Jack | Quantity: 12 | Location: Warehouse A
    }
}

// Test Order business logic (AddItem, RemoveItem, GetOrderSummary)
var testOrder = new Order
{
    OrderId = 1001,
    CustomerName = "Samir",
    DatePlaced = new DateTime(2025, 4, 5)
};
testOrder.AddItem(new InventoryItem { ItemId = 1, Name = "Pallet Jack", Quantity = 2, Location = "Warehouse A" });
testOrder.AddItem(new InventoryItem { ItemId = 2, Name = "Forklift Strap", Quantity = 5, Location = "Warehouse B" });
testOrder.AddItem(new InventoryItem { ItemId = 3, Name = "Barcode Scanner", Quantity = 1, Location = "Warehouse A" });
testOrder.RemoveItem(3);
Console.WriteLine(testOrder.GetOrderSummary()); // Prints: Order #1001 for Samir | Items: 2 | Placed: 4/5/2025

if (args.Contains("--verify-seed"))
{
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

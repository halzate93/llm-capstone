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

if (args.Contains("--verify-api"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
    var invCtrl = new LogiTrack.Controllers.InventoryController(db);
    var ordCtrl = new LogiTrack.Controllers.OrderController(db);

    var createdInv = (await invCtrl.CreateInventoryItem(new InventoryItem
    {
        Name = "Forklift",
        Quantity = 3,
        Location = "Warehouse B"
    })).Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
    var createdItem = (InventoryItem)createdInv!.Value!;
    Console.WriteLine($"[API Verify] POST /api/inventory -> 201 Created (ItemId={createdItem.ItemId}, Name={createdItem.Name})");

    var listInv = (await invCtrl.GetInventoryItems()).Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
    var allItems = (IEnumerable<InventoryItem>)listInv!.Value!;
    Console.WriteLine($"[API Verify] GET /api/inventory -> 200 OK (Count={allItems.Count()})");

    var createdOrd = (await ordCtrl.CreateOrder(new Order
    {
        CustomerName = "Acme Logistics",
        Items = new List<InventoryItem>
        {
            new() { Name = "Conveyor Roller", Quantity = 10, Location = "Warehouse A" },
            new() { Name = "Safety Vest", Quantity = 20, Location = "Warehouse C" }
        }
    })).Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
    var orderObj = (Order)createdOrd!.Value!;
    Console.WriteLine($"[API Verify] POST /api/orders -> 201 Created ({orderObj.GetOrderSummary()})");

    var fetchedOrd = (await ordCtrl.GetOrderById(orderObj.OrderId)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
    var fetchedOrderObj = (Order)fetchedOrd!.Value!;
    Console.WriteLine($"[API Verify] GET /api/orders/{orderObj.OrderId} -> 200 OK (Items={fetchedOrderObj.Items.Count})");

    var delOrd = await ordCtrl.DeleteOrder(orderObj.OrderId) as Microsoft.AspNetCore.Mvc.NoContentResult;
    Console.WriteLine($"[API Verify] DELETE /api/orders/{orderObj.OrderId} -> {delOrd!.StatusCode} NoContent");

    var notFoundOrd = (await ordCtrl.GetOrderById(orderObj.OrderId)).Result as Microsoft.AspNetCore.Mvc.NotFoundObjectResult;
    Console.WriteLine($"[API Verify] GET /api/orders/{orderObj.OrderId} (after delete) -> {notFoundOrd!.StatusCode} NotFound");

    var delInv = await invCtrl.DeleteInventoryItem(createdItem.ItemId) as Microsoft.AspNetCore.Mvc.NoContentResult;
    Console.WriteLine($"[API Verify] DELETE /api/inventory/{createdItem.ItemId} -> {delInv!.StatusCode} NoContent");
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

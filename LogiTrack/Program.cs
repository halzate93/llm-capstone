using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LogiTrack;
using LogiTrack.Controllers;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var verifyAuth = args.Contains("--verify-auth");
var verifyCache = args.Contains("--verify-cache");
var verifyAll = args.Contains("--verify-all");
if (verifyAuth || verifyCache || verifyAll)
{
    builder.WebHost.UseTestServer();
}

// Add services to the container.
builder.Services.AddDbContext<LogiTrackContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=logitrack.db"));

// Activity 4 & 5: Enable In-Memory Caching and ProblemDetails error handling
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();

// Activity 3: Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<LogiTrackContext>()
.AddDefaultTokenProviders();

// Activity 3: Configure JWT Bearer Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "LogiTrack_SuperSecret_SigningKey_2026_AtLeast32Bytes!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LogiTrackApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "LogiTrackClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "LogiTrack Order Management API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the format: Bearer {token}"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed database, roles, and warm up state on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
    context.Database.Migrate();

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

    var items = context.InventoryItems.AsNoTracking().ToList();
    foreach (var item in items)
    {
        item.DisplayInfo();
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var roleName in new[] { "Manager", "User" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (verifyAuth || verifyCache || verifyAll)
{
    await app.StartAsync();
    using var client = app.GetTestClient();

    // 1. Verify unauthenticated rejection (401)
    var unauthResp = await client.GetAsync("/api/inventory");
    Console.WriteLine($"[Workflow Verify] Unauthenticated GET /api/inventory -> {(int)unauthResp.StatusCode} {unauthResp.StatusCode}");

    // 2. Register and login User & Manager accounts
    var userSuffix = Guid.NewGuid().ToString("N")[..8];
    var userName = $"usr_{userSuffix}";
    var mgrName = $"mgr_{userSuffix}";

    await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
        Username = userName,
        Email = $"{userName}@logitrack.io",
        Password = "UserPass123!",
        Role = "User"
    });
    await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
        Username = mgrName,
        Email = $"{mgrName}@logitrack.io",
        Password = "ManagerPass123!",
        Role = "Manager"
    });

    var userLogin = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
    {
        Username = userName,
        Password = "UserPass123!"
    })).Content.ReadFromJsonAsync<AuthResponse>();

    var mgrLogin = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
    {
        Username = mgrName,
        Password = "ManagerPass123!"
    })).Content.ReadFromJsonAsync<AuthResponse>();

    // 3. Verify RBAC: standard user gets 403 on Manager route
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userLogin!.Token);
    var forbiddenResp = await client.PostAsJsonAsync("/api/inventory", new InventoryItem
    {
        Name = "Unauthorized Item",
        Quantity = 1,
        Location = "Warehouse Z"
    });
    Console.WriteLine($"[Workflow Verify] Standard User POST /api/inventory -> {(int)forbiddenResp.StatusCode} {forbiddenResp.StatusCode}");

    // 4. Switch to Manager token, test Caching (MISS -> HIT) & Inventory creation
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrLogin!.Token);
    var invMiss = await client.GetAsync("/api/inventory");
    var invHit = await client.GetAsync("/api/inventory");
    Console.WriteLine($"[Workflow Verify] GET /api/inventory 1st={invMiss.Headers.GetValues("X-Cache-Status").First()} ({invMiss.Headers.GetValues("X-Response-Time-Ms").First()} ms), 2nd={invHit.Headers.GetValues("X-Cache-Status").First()} ({invHit.Headers.GetValues("X-Response-Time-Ms").First()} ms)");

    var createInvResp = await client.PostAsJsonAsync("/api/inventory", new InventoryItem
    {
        Name = "Automated Pallet Wrapper",
        Quantity = 6,
        Location = "Warehouse B"
    });
    var createdInv = await createInvResp.Content.ReadFromJsonAsync<InventoryItem>();
    Console.WriteLine($"[Workflow Verify] Manager POST /api/inventory -> {(int)createInvResp.StatusCode} (ItemId={createdInv!.ItemId})");

    // 5. Verify State Persistence across sessions and simulated cache eviction / restart
    var sessionId = $"session-{userSuffix}";
    client.DefaultRequestHeaders.Remove("X-Session-Id");
    client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);

    var createOrderResp = await client.PostAsJsonAsync("/api/orders", new Order
    {
        CustomerName = "Global Freight Corp",
        Items = new List<InventoryItem>
        {
            new() { Name = "Heavy Duty Pallet", Quantity = 15, Location = "Warehouse A" },
            new() { Name = "RFID Tag Pack", Quantity = 50, Location = "Warehouse B" }
        }
    });
    var createdOrder = await createOrderResp.Content.ReadFromJsonAsync<Order>();
    Console.WriteLine($"[Workflow Verify] POST /api/orders -> {(int)createOrderResp.StatusCode} ({createdOrder!.GetOrderSummary()}, SessionId={createdOrder.SessionId})");

    // Evict IMemoryCache to simulate server restart / cold cache and test SQLite state rehydration
    var memoryCache = app.Services.GetRequiredService<IMemoryCache>();
    memoryCache.Remove(OrderController.GetOrderByIdCacheKey(createdOrder.OrderId));
    memoryCache.Remove(OrderController.GetSessionCacheKey(sessionId));

    var rehydratedSessionResp = await client.GetAsync($"/api/orders/session/{sessionId}");
    var rehydratedOrders = await rehydratedSessionResp.Content.ReadFromJsonAsync<List<Order>>();
    var cacheHeader = rehydratedSessionResp.Headers.GetValues("X-Cache-Status").First();
    Console.WriteLine($"[Workflow Verify] GET /api/orders/session/{sessionId} (after cache eviction) -> {(int)rehydratedSessionResp.StatusCode} Cache={cacheHeader}, RehydratedOrders={rehydratedOrders!.Count}");

    var cachedSessionResp = await client.GetAsync($"/api/orders/session/{sessionId}");
    Console.WriteLine($"[Workflow Verify] GET /api/orders/session/{sessionId} (subsequent) -> Cache={cachedSessionResp.Headers.GetValues("X-Cache-Status").First()}");

    // 6. Verify 400 BadRequest & 404 NotFound error handling
    var badOrderResp = await client.PostAsJsonAsync("/api/orders", new { customerName = "" });
    var missingOrderResp = await client.GetAsync("/api/orders/999999");
    Console.WriteLine($"[Workflow Verify] Invalid input POST /api/orders -> {(int)badOrderResp.StatusCode}, Missing GET /api/orders/999999 -> {(int)missingOrderResp.StatusCode}");

    // Cleanup test records
    await client.DeleteAsync($"/api/orders/{createdOrder.OrderId}");
    await client.DeleteAsync($"/api/inventory/{createdInv.ItemId}");

    await app.StopAsync();
    return;
}

app.Run();

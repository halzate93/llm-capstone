using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LogiTrack;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var verifyAuth = args.Contains("--verify-auth");
if (verifyAuth)
{
    builder.WebHost.UseTestServer();
}

// Add services to the container.
builder.Services.AddDbContext<LogiTrackContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=logitrack.db"));

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

// Seed database & roles on startup
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

if (verifyAuth)
{
    await app.StartAsync();
    using var client = app.GetTestClient();

    // 1. Unauthenticated request should return 401
    var unauthResp = await client.GetAsync("/api/inventory");
    Console.WriteLine($"[Auth Verify] Unauthenticated GET /api/inventory -> {(int)unauthResp.StatusCode} {unauthResp.StatusCode}");

    // 2. Register standard user and manager user
    var userEmail = $"user_{Guid.NewGuid():N}@logitrack.io";
    var userName = $"user_{Guid.NewGuid():N}"[..14];
    var regUserResp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
        Username = userName,
        Email = userEmail,
        Password = "UserPass123!",
        Role = "User"
    });
    Console.WriteLine($"[Auth Verify] POST /api/auth/register (User) -> {(int)regUserResp.StatusCode} {regUserResp.StatusCode}");

    var mgrEmail = $"mgr_{Guid.NewGuid():N}@logitrack.io";
    var mgrName = $"mgr_{Guid.NewGuid():N}"[..14];
    var regMgrResp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
        Username = mgrName,
        Email = mgrEmail,
        Password = "ManagerPass123!",
        Role = "Manager"
    });
    Console.WriteLine($"[Auth Verify] POST /api/auth/register (Manager) -> {(int)regMgrResp.StatusCode} {regMgrResp.StatusCode}");

    // 3. Login as standard user
    var loginUserResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
    {
        Username = userName,
        Password = "UserPass123!"
    });
    var userAuth = await loginUserResp.Content.ReadFromJsonAsync<AuthResponse>();
    Console.WriteLine($"[Auth Verify] POST /api/auth/login (User) -> {(int)loginUserResp.StatusCode} (Token Issued: {!string.IsNullOrEmpty(userAuth?.Token)})");

    // 4. Standard user can GET /api/inventory (200) but cannot POST /api/inventory (403 Forbidden)
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAuth!.Token);
    var userGetResp = await client.GetAsync("/api/inventory");
    var userPostResp = await client.PostAsJsonAsync("/api/inventory", new InventoryItem { Name = "Forklift", Quantity = 2, Location = "Warehouse B" });
    Console.WriteLine($"[Auth Verify] User GET /api/inventory -> {(int)userGetResp.StatusCode} {userGetResp.StatusCode}");
    Console.WriteLine($"[Auth Verify] User POST /api/inventory -> {(int)userPostResp.StatusCode} {userPostResp.StatusCode}");

    // 5. Login as Manager and POST /api/inventory (201 Created)
    var loginMgrResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
    {
        Username = mgrName,
        Password = "ManagerPass123!"
    });
    var mgrAuth = await loginMgrResp.Content.ReadFromJsonAsync<AuthResponse>();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrAuth!.Token);
    var mgrPostResp = await client.PostAsJsonAsync("/api/inventory", new InventoryItem { Name = "Reach Truck", Quantity = 4, Location = "Warehouse B" });
    var createdItem = await mgrPostResp.Content.ReadFromJsonAsync<InventoryItem>();
    Console.WriteLine($"[Auth Verify] Manager POST /api/inventory -> {(int)mgrPostResp.StatusCode} {mgrPostResp.StatusCode} (ItemId={createdItem?.ItemId})");

    var mgrDelResp = await client.DeleteAsync($"/api/inventory/{createdItem!.ItemId}");
    Console.WriteLine($"[Auth Verify] Manager DELETE /api/inventory/{createdItem.ItemId} -> {(int)mgrDelResp.StatusCode} {mgrDelResp.StatusCode}");

    await app.StopAsync();
    return;
}

app.Run();

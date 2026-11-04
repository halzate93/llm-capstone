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
var verifyCache = args.Contains("--verify-cache");
if (verifyAuth || verifyCache)
{
    builder.WebHost.UseTestServer();
}

// Add services to the container.
builder.Services.AddDbContext<LogiTrackContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=logitrack.db"));

// Activity 4: Enable In-Memory Caching
builder.Services.AddMemoryCache();

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

if (verifyAuth || verifyCache)
{
    await app.StartAsync();
    using var client = app.GetTestClient();

    var mgrEmail = $"mgr_{Guid.NewGuid():N}@logitrack.io";
    var mgrName = $"mgr_{Guid.NewGuid():N}"[..14];
    await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
    {
        Username = mgrName,
        Email = mgrEmail,
        Password = "ManagerPass123!",
        Role = "Manager"
    });
    var loginMgrResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
    {
        Username = mgrName,
        Password = "ManagerPass123!"
    });
    var mgrAuth = await loginMgrResp.Content.ReadFromJsonAsync<AuthResponse>();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrAuth!.Token);

    if (verifyCache)
    {
        // 1. First request -> Cache MISS
        var missResp = await client.GetAsync("/api/inventory");
        var missStatus = missResp.Headers.GetValues("X-Cache-Status").FirstOrDefault();
        var missTime = missResp.Headers.GetValues("X-Response-Time-Ms").FirstOrDefault();
        Console.WriteLine($"[Cache Verify] 1st GET /api/inventory -> Cache={missStatus}, Time={missTime} ms");

        // 2. Second request -> Cache HIT
        var hitResp = await client.GetAsync("/api/inventory");
        var hitStatus = hitResp.Headers.GetValues("X-Cache-Status").FirstOrDefault();
        var hitTime = hitResp.Headers.GetValues("X-Response-Time-Ms").FirstOrDefault();
        Console.WriteLine($"[Cache Verify] 2nd GET /api/inventory -> Cache={hitStatus}, Time={hitTime} ms");

        // 3. Mutate inventory -> Invalidates cache
        var postResp = await client.PostAsJsonAsync("/api/inventory", new InventoryItem
        {
            Name = "Automated Guided Vehicle",
            Quantity = 2,
            Location = "Warehouse C"
        });
        var created = await postResp.Content.ReadFromJsonAsync<InventoryItem>();

        // 4. Third request after mutation -> Cache MISS with updated data
        var afterWriteResp = await client.GetAsync("/api/inventory");
        var afterWriteStatus = afterWriteResp.Headers.GetValues("X-Cache-Status").FirstOrDefault();
        var afterWriteTime = afterWriteResp.Headers.GetValues("X-Response-Time-Ms").FirstOrDefault();
        Console.WriteLine($"[Cache Verify] GET /api/inventory after POST -> Cache={afterWriteStatus}, Time={afterWriteTime} ms");

        await client.DeleteAsync($"/api/inventory/{created!.ItemId}");
    }

    await app.StopAsync();
    return;
}

app.Run();

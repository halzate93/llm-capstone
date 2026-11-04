# LogiTrack Order Management System — 5-Activity Development Plan

## Commit Strategy
The repository is structured around **5 atomic commits**—one at the completion of each Capstone Activity—with clear summary commit messages describing the work completed in that stage.

---

## Activity 1: Structuring Business Logic and Database Integration
**Goal:** Scaffold the `LogiTrack` ASP.NET Core Web API project, implement domain models (`InventoryItem` and `Order`), configure EF Core with SQLite (`LogiTrackContext`), create the initial database schema migration, and verify database seeding and console summary output.

1. Create `LogiTrack` project (`dotnet new webapi -n LogiTrack --framework net8.0 --use-controllers`).
2. Implement `Models/InventoryItem.cs` (`ItemId`, `Name`, `Quantity`, `Location`, `OrderId`, `Order`, `DisplayInfo()`).
3. Implement `Models/Order.cs` (`OrderId`, `CustomerName`, `DatePlaced`, `Items`, `AddItem()`, `RemoveItem()`, `GetOrderSummary()`).
4. Add `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Tools`.
5. Create `LogiTrackContext.cs` configuring `InventoryItems`, `Orders`, SQLite (`Data Source=logitrack.db`), and the one-to-many relationship.
6. Create and apply the `InitialCreate` migration to generate `logitrack.db`.
7. Add startup seeding & test verification in `Program.cs` (creates `"Pallet Jack"` in `"Warehouse A"`, tests `DisplayInfo()`, `Order.AddItem()`, `Order.RemoveItem()`, and `GetOrderSummary()`).
8. **Commit 1:**
   `feat(activity-1): structure business logic models and EF Core SQLite integration`

---

## Activity 2: Building RESTful API Endpoints
**Goal:** Expose CRUD operations for inventory and orders via `InventoryController` and `OrderController`, refactor to `async/await` with proper HTTP status codes and error handling, and verify via HTTP/Swagger tests.

1. Register `LogiTrackContext` in `Program.cs` DI container and enable Controller mapping + Swagger UI.
2. Implement `Controllers/InventoryController.cs`:
   - `GET /api/inventory` (`GetInventoryItems`)
   - `POST /api/inventory` (`CreateInventoryItem`)
   - `DELETE /api/inventory/{id}` (`DeleteInventoryItem` with `404 NotFound` handling)
3. Implement `Controllers/OrderController.cs`:
   - `GET /api/orders` (`GetOrders` including `Items`)
   - `GET /api/orders/{id}` (`GetOrderById` including `Items`, returning `404 NotFound` when missing)
   - `POST /api/orders` (`CreateOrder` supporting multiple `InventoryItem` entries)
   - `DELETE /api/orders/{id}` (`DeleteOrder`)
4. **Commit 2:**
   `feat(activity-2): implement RESTful API controllers for inventory and orders`

---

## Activity 3: Securing the API with ASP.NET Identity & JWT
**Goal:** Integrate ASP.NET Core Identity, upgrade `LogiTrackContext` to `IdentityDbContext<ApplicationUser>`, implement `/api/auth/register` and `/api/auth/login` issuing JWT tokens, and enforce `[Authorize]` and `[Authorize(Roles = "Manager")]`.

1. Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` and `Microsoft.AspNetCore.Authentication.JwtBearer`.
2. Create `Models/ApplicationUser.cs` inheriting from `IdentityUser` and DTOs (`RegisterDto`, `LoginDto`) with validation attributes.
3. Update `LogiTrackContext` to inherit from `IdentityDbContext<ApplicationUser>`.
4. Configure Identity (`AddIdentity<ApplicationUser, IdentityRole>()`) and JWT Bearer authentication in `Program.cs` (plus Swagger Bearer auth support and `Manager` role seeding).
5. Create and apply the `AddIdentity` migration to update `logitrack.db`.
6. Implement `Controllers/AuthController.cs` (`POST /api/auth/register`, `POST /api/auth/login`).
7. Apply `[Authorize]` to `InventoryController` and `OrderController`, and `[Authorize(Roles = "Manager")]` to inventory mutation and order deletion endpoints.
8. **Commit 3:**
   `feat(activity-3): secure API with ASP.NET Core Identity, JWT auth, and Manager RBAC`

---

## Activity 4: Optimizing Performance with Caching and Query Tuning
**Goal:** Add `IMemoryCache` with 30-second expiration and cache invalidation, optimize EF Core queries with `.AsNoTracking()` and `.Include()`, and instrument endpoints with `Stopwatch` metrics.

1. Register `builder.Services.AddMemoryCache()` in `Program.cs`.
2. Inject `IMemoryCache` and `ILogger` into `InventoryController` and `OrderController`.
3. Cache `GET /api/inventory` for 30 seconds (`MemoryCacheEntryOptions` with `AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)`) and evict cache on `POST`/`DELETE`.
4. Optimize read queries in `InventoryController` and `OrderController` with `.AsNoTracking()` and `.Include(o => o.Items)`.
5. Add `Stopwatch` timing instrumentation and `X-Response-Time-Ms` / `X-Cache-Status` (`HIT` / `MISS`) headers to demonstrate measurable performance improvements.
6. **Commit 4:**
   `perf(activity-4): add IMemoryCache with 30s TTL and optimize EF Core queries`

---

## Activity 5: Integrating State Management, Full-System Validation & Reflection
**Goal:** Implement cross-session state persistence and cache rehydration, run comprehensive end-to-end workflow validation across server restarts, perform final code polish, and author the Peer Review Reflection document covering all 18 rubric points.

1. Enhance `Order` and `OrderController` with session/user state tracking (`SessionId` / user correlation) and cache rehydration helper so critical state is seamlessly recovered from SQLite after cache eviction or server restart.
2. Add global exception handling / ProblemDetails and clean up any redundant code.
3. Create an end-to-end verification script (`scripts/verify-workflow.ps1`) and `.http` request collection (`LogiTrack/LogiTrack.http`) testing:
   - User registration & JWT login (User & Manager roles)
   - Inventory creation, caching (`MISS` $\rightarrow$ `HIT`), and invalidation
   - Order creation with items, summary generation, and persistence across server restart
   - Role restriction (`401` / `403`) and invalid input handling (`400` / `404`)
4. Write `docs/Capstone-Reflection.md` answering all Step 1–3 peer-review questions mapped directly to the 18-point grading rubric.
5. **Commit 5:**
   `feat(activity-5): implement persistent state management, E2E verification, and capstone reflection`

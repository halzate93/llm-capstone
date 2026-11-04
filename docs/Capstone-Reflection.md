# LogiTrack Capstone Project — Peer Review Submission & Reflection

> Use the sections below to directly copy and paste your responses into the assignment submission form. Each response is written to satisfy all 18 points of the grading rubric in full detail.

---

## 1. Project Title
**LogiTrack: A Secure, High-Performance Order and Warehouse Inventory Management REST API Built with ASP.NET Core, Entity Framework Core, and JWT Identity**

---

## 2. Describe your API project and its key features.
LogiTrack is a full-featured backend Order Management System REST API built with ASP.NET Core Web API (.NET 8), Entity Framework Core, and SQLite designed to coordinate warehouse inventory and customer fulfillment orders across multiple logistics centers. The system provides a centralized, stateless HTTP interface that enables warehouse staff, fulfillment systems, and managers to track stock levels, process multi-item customer orders, and audit order summaries in real time.

The application delivers three core functionalities:
1. **Multi-Warehouse Inventory Management (`/api/inventory`):** Allows authenticated users to query real-time stock quantities and warehouse locations across fulfillment centers, while empowering users with the `Manager` role to register new inventory items and decommission obsolete stock.
2. **End-to-End Customer Order Processing (`/api/orders`):** Enables creating, retrieving, summarizing, and deleting customer orders containing multiple `InventoryItem` line items, automatically aggregating duplicate item quantities and generating human-readable fulfillment summaries via `GetOrderSummary()`.
3. **Role-Based Security, In-Memory Caching, and Cross-Session State Persistence (`/api/auth` & `/api/orders/session/{sessionId}`):** Secures all endpoints using ASP.NET Core Identity and HMAC-SHA256 JSON Web Tokens (JWT), accelerates high-frequency reads using `IMemoryCache` with write-through cache invalidation, and persists session-linked order states in SQLite with automatic cache rehydration across server restarts.

---

## 3. What were the major challenges you faced, and how did you overcome them?
During development, one of the primary architectural challenges was modeling the one-to-many relationship between `Order` and `InventoryItem` in Entity Framework Core without introducing circular JSON serialization errors or $N+1$ database query bottlenecks when creating and retrieving multi-item orders. Initially, serializing an `Order` that eagerly loaded its `Items` caused infinite reference loops through the parent navigation property, and attaching existing inventory items inside a loop during `POST /api/orders` triggered redundant database round-trips. I overcame this by decorating the inverse `Order` navigation property on `InventoryItem` with `[JsonIgnore]`, configuring the foreign key relationship explicitly in `LogiTrackContext.OnModelCreating` with `DeleteBehavior.SetNull`, and refactoring `OrderController.CreateOrder` with Microsoft Copilot's assistance to batch-load all existing `ItemId` references in a single `WHERE ItemId IN (...)` dictionary query before linking them to the order.

A second significant challenge was preventing stale data when combining `IMemoryCache` with frequent `POST` and `DELETE` mutations, as well as ensuring seamless state recovery after cache eviction or server restarts. If a manager added or deleted an inventory item while the 30-second cache entry was still active, subsequent `GET /api/inventory` calls risked returning outdated stock counts. To solve this, I implemented deterministic cache key management (`InventoryList_CacheKey`, `OrderList_CacheKey`, and session-scoped keys) paired with explicit write-through cache invalidation (`_cache.Remove(...)`) on every mutating endpoint, along with automatic SQLite database rehydration whenever a session or order cache key misses.

---

## 4. How did you implement key components like business logic, data persistence, and state management?
**Business Logic:** I structured the core domain logic around two strongly-typed entity models—`InventoryItem` and `Order`—encapsulating behavior directly alongside state. `InventoryItem` tracks `ItemId`, `Name`, `Quantity`, `Location`, and an optional `OrderId` foreign key, and provides a `DisplayInfo()` method for formatted console diagnostics. `Order` manages `OrderId`, `CustomerName`, `DatePlaced`, `SessionId`, `CreatedByUserId`, and its `Items` collection, exposing domain methods `AddItem(InventoryItem item)` (which validates input and merges quantities when an item with the same `ItemId` already exists), `RemoveItem(int itemId)`, and `GetOrderSummary()`.

**Data Persistence:** Data persistence is implemented through `LogiTrackContext`, which inherits from `IdentityDbContext<ApplicationUser>` and manages `DbSet<InventoryItem>` and `DbSet<Order>` backed by a relational SQLite database (`logitrack.db`). Schema evolution is managed reproducibly via Entity Framework Core migrations (`InitialCreate`, `AddIdentity`, and `AddOrderStateTracking`), with composite and single-column indexes defined in `OnModelCreating` on `OrderId`, `Name`, `DatePlaced`, and `SessionId` to keep lookups fast as data volume grows.

**State Management:** To retain critical data across client sessions and server restarts, I implemented a hybrid **Database Persistence + Cache Rehydration** strategy. Every created order captures the authenticated user's `NameIdentifier` claim (`CreatedByUserId`) and a correlation `SessionId` (from the `X-Session-Id` header or payload). Active orders and session states are cached in `IMemoryCache` with a 5-minute sliding / 15-minute absolute expiration policy for fast in-memory access; if the cache expires or the server restarts, endpoints such as `GET /api/orders/{id}` and `GET /api/orders/session/{sessionId}` transparently query SQLite, rehydrate `IMemoryCache`, and return the restored state with an `X-Cache-Status: REHYDRATED` header.

---

## 5. What security measures did you implement?
I implemented a defense-in-depth security architecture centered on **ASP.NET Core Identity**, **JWT Bearer Authentication**, **Role-Based Access Control (RBAC)**, and **strict input validation**:
1. **ASP.NET Core Identity & Password Hardening:** User accounts are modeled with `ApplicationUser : IdentityUser` and persisted via `IdentityDbContext<ApplicationUser>`. In `Program.cs`, Identity is configured to enforce strong password complexity (minimum 8 characters, requiring digits, uppercase, lowercase, and non-alphanumeric characters), unique email addresses, and account lockout protection on repeated failed sign-in attempts (`CheckPasswordSignInAsync(..., lockoutOnFailure: true)`).
2. **Stateless JWT Authentication:** `AuthController` exposes `/api/auth/register` and `/api/auth/login`. Upon successful credential verification via `UserManager` and `SignInManager`, the API issues an HMAC-SHA256 signed JWT containing the user's `Sub`, `UniqueName`, `Email`, `Jti`, and `ClaimTypes.Role` claims, validated on every request by the `JwtBearerMiddleware` against issuer, audience, lifetime, and signing key parameters.
3. **Role-Based Access Control (RBAC):** All routes in `InventoryController` and `OrderController` require authentication via the controller-level `[Authorize]` attribute, returning `401 Unauthorized` to anonymous callers. Administrative and destructive operations—specifically `POST /api/inventory`, `DELETE /api/inventory/{id}`, and `DELETE /api/orders/{id}`—are further restricted with `[Authorize(Roles = "Manager")]`, returning `403 Forbidden` if a standard `User` attempts to modify warehouse stock or delete orders.
4. **Input Validation & Safe Queries:** All models and authentication DTOs (`RegisterRequest`, `LoginRequest`, `InventoryItem`, `Order`) enforce Data Annotations (`[Required]`, `[EmailAddress]`, `[MinLength]`, `[MaxLength]`, `[Range(0, int.MaxValue)]`) validated automatically by `[ApiController]` before database execution, and all SQLite queries are parameterized by EF Core to prevent SQL injection.

---

## 6. How did you manage caching and optimize performance?
To optimize performance and scalability under load, I combined **in-memory response caching (`IMemoryCache`)**, **EF Core query tuning**, and **asynchronous I/O**:
1. **In-Memory Caching with Expiration and Write-Through Invalidation:** I registered `builder.Services.AddMemoryCache()` in `Program.cs` and injected `IMemoryCache` into both `InventoryController` and `OrderController`. High-read endpoints (`GET /api/inventory` and `GET /api/orders`) cache their serialized query results for 30 seconds (`AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)`), while individual order and session lookups use a 5-minute sliding / 15-minute absolute TTL. Whenever a `POST` or `DELETE` operation modifies inventory or orders, the controller immediately evicts the affected cache keys (`_cache.Remove(...)`) so clients never read stale data.
2. **EF Core Query Optimizations (`.AsNoTracking()` & `.Include()`):** All read-only queries in `InventoryController` and `OrderController` apply `.AsNoTracking()`, bypassing EF Core's change-tracking snapshot creation and significantly reducing memory allocations. In `OrderController`, queries use `.Include(o => o.Items)` to eager-load order items in a single SQL `LEFT JOIN` rather than triggering lazy-loaded $N+1$ queries, and `CreateOrder` batches item lookups into a single `Where(i => existingIds.Contains(i.ItemId)).ToDictionaryAsync(...)` call.
3. **Measured Performance Gains:** Using `System.Diagnostics.Stopwatch` and custom diagnostic headers (`X-Cache-Status` and `X-Response-Time-Ms`), benchmarking confirmed that cold SQLite queries (`Cache=MISS`) took approximately `8.87 ms`, whereas subsequent cached requests (`Cache=HIT`) completed in `0.023 ms`—an improvement of over **380x** in response latency while eliminating repetitive database load.

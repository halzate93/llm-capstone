# Microsoft Backend Developer Capstone Project

# This assignment is the culmination of your backend capstone project, where you’ve built a secure and optimized order management system using ASP.NET Core and Entity Framework. In this peer review, you’ll evaluate another learner’s submission by reviewing their responses to key questions about their implementation and development process.

# Throughout this assignment, you will:

* # Describe and analyze your own backend application

* # Answer targeted questions about your architectural, security, and performance decisions

* # Evaluate a peer’s submission based on structured grading criteria

# This peer review simulates real-world backend development review processes and provides an opportunity to reflect on your design choices while learning from others.

### Grading Criteria Overview

There are a total of **18** points for this project:

* **(2 pts)** Clearly identified and described at least three key features of the application  
* **(2 pts)** Analyzed significant development challenges and explained how they were addressed  
* **(4 pts)** Explained how business logic, caching, and state management were structured and implemented  
* **(6 pts)** Described the security approach, including authentication, role-based access, and input validation  
* **(4 pts)** Demonstrated performance optimization through query improvements, caching strategies, or other techniques

### Step by Step Assignment Instructions

**Step 1: Describe Your Project**

Provide a brief written summary of your project:

* What does your API system do?  
* What are three key functionalities of the app?  
* What technologies and tools did you use?

**Step 2: Analyze Your Development Process**

Answer the following questions:

* What challenges did you face during development?  
* How did you solve those challenges?  
* How did you use Microsoft Copilot to debug or improve your code?  
* What optimizations did you make for performance and scalability?

**Step 3: Review Your Project’s Key Features**

Reflect on how you implemented the following:

* Business Logic: How did you structure your data models and relationships?  
* API Design: What controllers and routes did you build?  
* Security: How did you manage authentication and role-based access?  
* Caching: Where did you use in-memory caching and why?  
* State Management: How did your app retain data across sessions?

**Step 4: Final Testing and Submission**

Before submitting your reflection, ensure that:

* The full system is tested and running as intended  
* You’ve addressed performance and security  
* Your answers are clear and thoughtful

After submission, you will be assigned a peer’s submission to evaluate.

# Activity: Capstone Part 1 \- Structuring Business Logic and Database Integration

**Introduction**

You're developing the Order Management System for LogiTrack, a logistics software platform. This system will manage inventory items and customer orders across multiple fulfillment centers.

In this activity, you will:

* Create two business logic classes: InventoryItem and Order.  
* Model your data and integrate it into a relational database using EF Core.  
* Build basic logic to create, view, and update inventory and orders.  
* Use Microsoft Copilot to help debug and optimize your work.

This is the first of five activities in your capstone project. The classes and logic you develop here will be extended in upcoming parts to include API endpoints, secure access, and performance optimization.

**Activity Instructions**

**Step 1**: Set Up Your Project

* Open Visual Studio Code  
* Use the CLI to create a new console or ASP.NET Core Web API project: **dotnet new webapi \-n LogiTrack cd LogiTrack**  
* Add a Models folder and create two new C\# class files:   
  * InventoryItem.cs  
  * Order.cs

**Step 2**: Define the InventoryItem Class 

Create a class named **InventoryItem** with the following properties:

* ItemId (int)  
* Name (string)  
* Quantity (int)  
* Location (string)

Include a method named **DisplayInfo()** that prints: Item: Pallet Jack \| Quantity: 12 \| Location: Warehouse A

Test this method with a sample object in Program.cs or a test controller.

**Step 3**: Define the Order Class 

Create a class named **Order** with the following properties:

* OrderId (int)  
* CustomerName (string)  
* DatePlaced (DateTime)  
* List (list of items in the order)

Add methods to:

* AddItem(InventoryItem item)  
* RemoveItem(int itemId)  
* GetOrderSummary() – returns a string like: Order \#1001 for Samir \| Items: 2 \| Placed: 4/5/2025

Write a test block to create an order, add/remove items, and print the summary.

**Step 4**: Connect to a Database with EF Core

* Add EF Core packages via NuGet or CLI: 

dotnet add package Microsoft.EntityFrameworkCore.Sqlite  
dotnet add package Microsoft.EntityFrameworkCore.Tools

* Create a new DbContext class in a file named LogiTrackContext.cs:

using Microsoft.EntityFrameworkCore;  
public class LogiTrackContext : DbContext  
{  
    public DbSet\<InventoryItem\> InventoryItems { get; set; }  
    public DbSet\<Order\> Orders { get; set; }  
    protected override void OnConfiguring(DbContextOptionsBuilder options)  
        \=\> options.UseSqlite("Data Source=logitrack.db");  
}

* Add proper \[Key\], \[Required\], or \[ForeignKey\] attributes if needed.  
* Use migrations to create your database:

dotnet ef migrations add InitialCreate  
dotnet ef database update

**Step 5**: Seed and Test the Database

Now that your database schema is created, add some test data to verify that everything works as expected.

* Open **Program.cs** (or your test controller) and add the following block of code:

using (var context \= new LogiTrackContext())  
{  
    // Add test inventory item if none exist  
    if (\!context.InventoryItems.Any())  
    {  
        context.InventoryItems.Add(new InventoryItem  
        {  
            Name \= "Pallet Jack",  
            Quantity \= 12,  
            Location \= "Warehouse A"  
        });

        context.SaveChanges();  
    }

    // Retrieve and print inventory to confirm  
    var items \= context.InventoryItems.ToList();  
    foreach (var item in items)  
    {  
        item.DisplayInfo(); // Should print: Item: Pallet Jack \| Quantity: 12 \| Location: Warehouse A  
    }  
}

* Run the application to confirm that the data is saved and printed as expected.

This step ensures that your **DbContext** is working, your table is populated, and your **DisplayInfo()** method works correctly.

**Step 6**: Use Microsoft Copilot to Review and Optimize Try the following prompts with Microsoft Copilot:

* "Generate EF Core code for one-to-many between Order and InventoryItem."  
* "Suggest a way to print Order summaries efficiently."  
* "Identify any performance improvements in my AddItem method."

After reviewing Copilot suggestions:

* Refactor any logic as needed  
* Ensure everything compiles and runs with no errors

Submission Checklist

* Two working classes: InventoryItem and Order  
* Test data with basic output shown in console or controller  
* LogiTrackContext with EF Core integration  
* Database successfully created and tested  
* Copilot-assisted review applied  
* Code saved for integration in Part 2

# Activity: Capstone Part 2 \- Building RESTful API endpoints

## **Introduction**

You’ve structured the logic and database for LogiTrack’s order management system. Now it’s time to build the web API that allows systems to interact with it. This will make your data accessible through secure HTTP routes for viewing, creating, and updating orders and inventory.

In this activity, you will:

* Create API controllers for inventory and orders  
* Implement GET, POST, and DELETE routes  
* Test your endpoints using Swagger or Postman  
* Use Microsoft Copilot to debug and optimize routing logic

This is the second of five activities in your capstone project. The endpoints you build will be secured and optimized in future parts.

## **Activity Instructions**

**Step 1: Create the Controllers**

* In the **Controllers** folder, create two new files:  
  * **InventoryController.cs**  
  * **OrderController.cs**

**Step 2: Build InventoryController Endpoints**

In **InventoryController.cs**, implement the following:

**\[HttpGet\] /api/inventory**

* Return a list of all inventory items

**\[HttpPost\] /api/inventory**

* Add a new item to the inventory

**\[HttpDelete\] /api/inventory/{id}**

* Remove an item by ID

Use dependency injection to get an instance of **LogiTrackContext**.

**Step 3: Build OrderController Endpoints**

In **OrderController.cs**, implement the following:

**\[HttpGet\] /api/orders**

* Return a list of all orders

**\[HttpGet\] /api/orders/{id}**

* Return an order with its items

**\[HttpPost\] /api/orders**

* Create a new order

**\[HttpDelete\] /api/orders/{id}**

* Delete an order by ID

Ensure the **Order** object submitted through the POST route can include multiple inventory items.

**Step 4: Test Your API Endpoints**

* Run your project  
* Use Swagger (auto-generated by ASP.NET Core) or Postman to test your endpoints  
* Confirm that:  
  * You can create, read, and delete inventory items and orders  
  * Data persists in the database between requests

**Step 5: Use Microsoft Copilot to Review and Improve**

Try these prompts in Copilot:

* “Generate a GET endpoint to return an order by ID including item details.”  
* “Suggest how to handle error responses if an ID is not found.”  
* “Refactor this controller to use async methods.”

After reviewing suggestions:

* Apply improvements to routing, error handling, or controller design.  
* Re-test to ensure functionality.

### **Submission Checklist**

* Two API controllers: Inventory and Order  
* Working GET, POST, DELETE routes for both controllers  
* Routes tested in Swagger or Postman  
* Microsoft Copilot suggestions applied  
* Code saved for use in Part 3

# Activity: Capstone Part 3 \- Securing your API with [ASP.NET](http://ASP.NET) Identity

**Introduction**

Your API now performs CRUD operations for orders and inventory. But without authentication and access control, it’s vulnerable to misuse. In this activity, you’ll secure the API using ASP.NET Identity and role-based access.

In this activity, you will:

* Add ASP.NET Identity to your project  
* Create registration and login functionality  
* Secure API endpoints with authentication and roles  
* Use Copilot to validate your security configuration

This is the third of five activities in your capstone project. The security you implement here will provide the foundation for performance and state management in Parts 4 and 5\.

## **Activity Instructions**

**Step 1: Install ASP.NET Identity**

Install the required NuGet packages:

dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore

dotnet add package Microsoft.EntityFrameworkCore.Sqlite

Create an **ApplicationUser** class that inherits from **IdentityUser**.

**Step 2: Set Up Identity in Your Project**

* Modify your **LogiTrackContext** to inherit from **IdentityDbContext\<ApplicationUser\>**.  
* Configure services in **Program.cs** to support Identity:

builder.Services.AddIdentity\<ApplicationUser, IdentityRole\>()  
    .AddEntityFrameworkStores\<LogiTrackContext\>();

Update your database and apply a migration:

dotnet ef migrations add AddIdentity  
dotnet ef database update

**Step 3: Create Registration and Login Endpoints**

* Add an **AuthController** with the following endpoints:  
  * **\[HttpPost\] /api/auth/register** – registers a user  
  * **\[HttpPost\] /api/auth/login** – logs in and returns a JWT

Use **UserManager** and **SignInManager** services to handle identity operations.

**Step 4: Protect API Routes with Authorization**

* Use the **\[Authorize\]** attribute to protect endpoints in **OrderController** and **InventoryController**.  
* Assign a role to admin users (e.g., “Manager”) and limit specific routes with:

**\[Authorize(Roles \= "Manager")\]**

**Step 5: Use Microsoft Copilot for Security Review**

Use these prompts with Copilot:

* “Add JWT authentication to an ASP.NET Core API.”  
* “How do I restrict a route to users with a specific role?”  
* “Suggest improvements to secure user registration.”

Test your secured API by:

* Registering a new user  
* Logging in and retrieving a token  
* Accessing routes with and without authentication

### **Submission Checklist**

* ASP.NET Identity installed and configured  
* **ApplicationUser** model created  
* Registration and login routes added  
* JWT token-based authentication implemented  
* Routes secured with **\[Authorize\]** and roles  
* Copilot suggestions reviewed and applied  
* Database updated with Identity schema

# Activity: Capstone Part 4 \- Optimizing Performance with Caching and Copilot

**Introduction**

Now that your API is functional and secure, it's time to make it fast and scalable. In this activity, you'll implement caching to reduce load and accelerate responses. You’ll also refine your queries and controller logic with the help of Microsoft Copilot.

In this activity, you will:

* Apply in-memory caching to frequently accessed data  
* Improve database performance using query optimizations  
* Use Copilot to analyze and enhance performance

This is the fourth of five activities in your capstone project. These optimizations will ensure your system remains stable under load and performs well in real-world use.

## **Activity Instructions**

**Step 1: Enable In-Memory Caching**

* In **Program.cs**, add:

**builder.Services.AddMemoryCache();**

In your controller (e.g., **InventoryController**), inject **IMemoryCache**:

private readonly IMemoryCache \_cache;  
public InventoryController(LogiTrackContext context, IMemoryCache cache)  
{  
    \_context \= context;  
    \_cache \= cache;  
}

**Step 2: Cache the Inventory List**

Modify the **\[HttpGet\]** method in **InventoryController** to use caching:

* Store the result of the inventory query in memory for 30 seconds  
* Retrieve it from cache on subsequent requests

Example Copilot prompt:

* “Add in-memory caching to a GET request in ASP.NET Core.”

**Step 3: Analyze and Optimize Queries**

* Use **.Include()** to eager-load related data and reduce N+1 problems  
* Use **.AsNoTracking()** when queries don’t require tracking

In **OrderController**, identify and refactor any queries that may be slow or repetitive.

Use the following Copilot prompt:

* “Optimize Entity Framework queries in ASP.NET Core.”

**Step 4: Measure Improvements**

* Use timing methods **(Stopwatch)** or performance profiling tools to measure improvement before and after caching  
* Ensure that cached routes still return valid data and refresh appropriately

**Step 5: Use Copilot to Review and Improve**

Try these prompts:

* “Suggest performance improvements in this controller.”  
* “How can I avoid repeated DB calls for the same result?”  
* “Improve the speed of this API endpoint.”

Apply Copilot’s suggestions to your codebase and re-test.

### **Submission Checklist**

* In-memory caching implemented on at least one route  
* Query optimizations applied in controller logic  
* Slow or repeated queries reduced or eliminated  
* Cache expiration policy in place  
* Copilot prompts used to refine logic  
* Improvements verified through testing or time measurement  
* Code saved for use in Part 5

# Activity: Capstone Part 5 \- Integrating State Manage and Finishing the Project

**Introduction**

Your API is now functional, secure, and optimized. To complete your capstone, you’ll ensure data consistency and test how your system behaves across sessions. You’ll use Copilot for a final project review and prepare your work for peer submission.

In this activity, you will:

* Implement persistent state management for critical data  
* Conduct full-system tests to validate functionality  
* Apply final performance and usability enhancements with Copilot

This is the final activity in your capstone project. At the end, you’ll have a complete, portfolio-ready API system to submit for peer review.

## **Activity Instructions**

**Step 1: Implement State Persistence**

Choose one of the following strategies to persist key data:

* **Caching strategy:** Use **IMemoryCache** with longer expiration and rehydration logic  
* **Database strategy:** Confirm database persistence by verifying that state survives server restarts

For cart-like or temporary order data, consider storing session IDs or user tokens as part of the order model.

**Step 2: Validate Application Workflow**

Test the entire workflow, including:

* Inventory and order creation  
* Authentication and access control  
* API response times and caching  
* Error handling for invalid input  
* Secure access to role-restricted routes

Use Swagger or Postman to simulate real usage.

**Step 3: Use Microsoft Copilot for Final Improvements**

Try prompts like:

* “Review this codebase for redundant logic.”  
* “Suggest final performance improvements.”  
* “What else should I include in a production-ready API?”

Apply Copilot suggestions and verify they improve clarity, performance, or security.

**Step 4: Prepare Final Submission**

* Save all files and confirm successful project build  
* Prepare a short summary of your system architecture and key decisions (used in Peer Review)

### **Submission Checklist**

* Persistent state implementation verified  
* Full workflow tested: authentication, caching, API behavior  
* Code optimized using Copilot recommendations  
* Redundant or unnecessary logic removed  
* Final version ready for peer submission


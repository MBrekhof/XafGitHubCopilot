# XafGitHubCopilot

Integrating the GitHub Copilot SDK into a DevExpress XAF application to create an in-app AI assistant that queries live business data, creates records conversationally, and works on both Blazor Server and WinForms.

## Features

- **Dynamic Schema Discovery** — The AI assistant automatically discovers all entities, properties, relationships, and enum values at runtime via XAF `ITypesInfo` reflection. No hardcoded entity definitions; add or modify business objects and the AI immediately knows about them.
- **Generic Tool Calling** — Three AI-powered tools (`list_entities`, `query_entity`, `create_entity`) work with any entity in the data model. The AI can query, filter, and create records for any table without entity-specific code.
- **Dual Platform** — Full support for both Blazor Server and WinForms using DevExpress AI chat controls (`DxAIChat` and `AIChatControl`), backed by the same shared module.
- **Streaming Responses** — Real-time streaming of AI responses via the GitHub Copilot SDK session event model.
- **Runtime Model Switching** — Switch between AI models (GPT-4o, GPT-5, Claude Sonnet 4, Gemini 2.5 Pro, etc.) at runtime via a toolbar action.
- **Markdown Rendering** — AI responses rendered as formatted HTML with table, code block, and list support via Markdig + HtmlSanitizer.

## Architecture

```
XafGitHubCopilot.Module/          Platform-agnostic core (business objects, services, controllers)
  BusinessObjects/                EF Core entities (Northwind-style domain)
  Services/                       GitHub Copilot SDK integration layer
    SchemaDiscoveryService        Reflects over ITypesInfo to discover entities at runtime
    CopilotChatService            Manages CopilotClient lifecycle, sessions, and streaming
    CopilotChatClient             IChatClient adapter for DevExpress AI controls
    CopilotToolsProvider          Generic AIFunction tools (list, query, create)
    CopilotChatDefaults           Shared UI config, prompt suggestions, Markdown rendering
    CopilotOptions                Configuration model bound from appsettings.json
  Controllers/                    XAF controllers (navigation, model switching)

XafGitHubCopilot.Blazor.Server/   Blazor Server UI
  Editors/CopilotChatViewItem/    CopilotChat.razor (DxAIChat component)

XafGitHubCopilot.Win/             WinForms UI
  Editors/                        AIChatControl integration
```

### How It Works

1. **Startup** — `ServiceCollectionExtensions.AddCopilotSdk()` registers all services as singletons. `SchemaDiscoveryService` reflects over `ITypesInfo` to discover the data model. The system prompt and AI tools are generated dynamically from this metadata.

2. **Schema Discovery** — `SchemaDiscoveryService` iterates all persistent types in the XAF type system, extracts scalar properties (with types and enum values), navigation properties (to-one and to-many relationships), and produces a `SchemaInfo` object cached for the application lifetime.

3. **AI Tools** — `CopilotToolsProvider` exposes three `AIFunction` tools to the Copilot SDK:
   - `list_entities` — Returns all entity names with properties, relationships, and enum values
   - `query_entity` — Queries any entity by name with optional `PropertyName=value` filters (semicolon-separated). Supports partial string matching, enum filtering, and relationship navigation.
   - `create_entity` — Creates a record for any entity with `PropertyName=value` property pairs. Resolves relationship references by searching for matching display names.

4. **Chat Flow** — User messages flow through `DxAIChat` (Blazor) or `AIChatControl` (WinForms) → `CopilotChatClient` (IChatClient adapter) → `CopilotChatService` → GitHub Copilot SDK session → AI model. Tool calls are executed automatically by the SDK, with results fed back into the conversation.

For a detailed step-by-step walkthrough of how a user question becomes a data-driven answer — including what the AI model sees, how it decides which tool to call, and how the query executes against the database — see **[Behind the Scenes](BEHIND_THE_SCENES.md)**.

## Data Model

A Northwind-style order management domain with 13 business entities:

| Entity | Key Properties | Relationships |
|--------|---------------|---------------|
| **Customer** | CompanyName, ContactName, Phone, Email, City, Country | has many Orders |
| **Order** | OrderDate, Status (New/Processing/Shipped/Delivered/Cancelled), Freight, ShipCity | belongs to Customer, Employee, Shipper, Invoice; has many OrderItems |
| **OrderItem** | UnitPrice, Quantity, Discount | belongs to Order, Product |
| **Product** | Name, UnitPrice, UnitsInStock, Discontinued | belongs to Category, Supplier |
| **Category** | Name, Description | has many Products |
| **Supplier** | CompanyName, ContactName, Phone, Email | has many Products |
| **Employee** | FirstName, LastName, Title, HireDate | belongs to Department; has many Orders, Territories, DirectReports |
| **Department** | Name, Code, Location, Budget, IsActive | has many Employees |
| **EmployeeTerritory** | (join table) | belongs to Employee, Territory |
| **Territory** | Name | belongs to Region |
| **Region** | Name | has many Territories |
| **Shipper** | CompanyName, Phone | has many Orders |
| **Invoice** | InvoiceNumber, InvoiceDate, DueDate, Status (Draft/Sent/Paid/Overdue/Cancelled) | has many Orders |

Seed data is generated automatically on first run: 20 customers, 5 employees across 5 departments, 3 shippers, 30 products across 8 categories, 50 orders, and 20 invoices.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [DevExpress Universal Subscription](https://www.devexpress.com/) (v25.2+) with a valid NuGet feed configured
- A GitHub account with Copilot access (Individual, Business, or Enterprise)
- GitHub CLI (`gh`) logged in, **or** a GitHub Personal Access Token

## Getting Started

### 1. Clone and build

```bash
git clone https://github.com/MBrekhof/XafGitHubCopilot.git
cd XafGitHubCopilot
dotnet build XafGitHubCopilot.slnx
```

### 2. Configure authentication

The GitHub Copilot SDK authenticates via one of two methods:

**Option A — GitHub CLI (default)**
Log in with `gh auth login`. The SDK picks up credentials automatically when `UseLoggedInUser` is `true` (the default).

**Option B — Personal Access Token**
Add a `"Copilot"` section to `appsettings.json`:

```json
{
  "Copilot": {
    "GithubToken": "ghp_your_token_here"
  }
}
```

### 3. Run

```bash
# Blazor Server (web)
dotnet run --project XafGitHubCopilot/XafGitHubCopilot.Blazor.Server

# WinForms (desktop, Windows only)
dotnet run --project XafGitHubCopilot/XafGitHubCopilot.Win
```

Log in with user **Admin** (empty password) or **User** (empty password).

Navigate to the **Copilot Chat** item in the sidebar to start chatting with the AI assistant.

## Configuration

All Copilot SDK settings are in the `"Copilot"` section of `appsettings.json`:

| Setting | Default | Description |
|---------|---------|-------------|
| `Model` | `"gpt-4o"` | AI model to use. Can be switched at runtime via the toolbar. |
| `GithubToken` | `null` | GitHub PAT. If set, overrides CLI authentication. |
| `CliPath` | `null` | Custom path to the GitHub CLI binary. |
| `UseLoggedInUser` | `true` | Use the currently logged-in GitHub CLI user for authentication. |
| `Streaming` | `true` | Enable streaming responses. |

### Available AI Models

Selectable at runtime via the model switcher toolbar action:

- GPT-4o, GPT-4o Mini, GPT-4.1, GPT-4.1 Mini, GPT-4.1 Nano
- GPT-5, o3-mini, o4-mini
- Claude Sonnet 4
- Gemini 2.5 Pro

## Example Prompts

| Use Case | Example Prompt |
|----------|---------------|
| Order Lookup | "Show me all orders for Around the Horn that are still processing" |
| Invoice Aging | "Give me an aging summary of overdue invoices grouped by customer" |
| Low Stock Alert | "Which products have fewer than 20 units in stock?" |
| Sales Leaderboard | "Rank employees by number of orders and show their territories" |
| Create Record | "Create a new order for Alfreds Futterkiste: 10 units of Chai, ship via Speedy Express" |
| Schema Discovery | "What entities are available in the database?" |

## Dynamic Schema Discovery in Action

The AI assistant does not use hardcoded entity definitions. Instead, `SchemaDiscoveryService` reflects over the XAF `ITypesInfo` type system at startup to discover every persistent entity, its properties, relationships, and enum values. This means you can add, rename, or remove business objects and the AI immediately knows about the changes — no service code modifications required.

### Example: Adding the Department Entity

The `Department` entity was added to demonstrate this. Here is everything that was needed:

**1. Create the business object** (`BusinessObjects/Department.cs`):

```csharp
[DefaultClassOptions]
[NavigationItem("HR")]
[DefaultProperty(nameof(Name))]
public class Department : BaseObject
{
    [StringLength(128)]
    public virtual string Name { get; set; }

    [StringLength(64)]
    public virtual string Code { get; set; }

    [StringLength(256)]
    public virtual string Location { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public virtual decimal Budget { get; set; }

    public virtual bool IsActive { get; set; } = true;

    public virtual IList<Employee> Employees { get; set; } = new ObservableCollection<Employee>();
}
```

**2. Add a `DbSet` to the `DbContext`:**

```csharp
public DbSet<Department> Departments { get; set; }
```

**3. Configure relationships in `OnModelCreating`** (standard EF Core):

```csharp
modelBuilder.Entity<Department>()
    .HasMany(d => d.Employees)
    .WithOne(e => e.Department)
    .HasForeignKey(e => e.DepartmentId)
    .OnDelete(DeleteBehavior.SetNull);
```

**4. Run the app and ask the AI:**

| Prompt | What happens |
|--------|-------------|
| "What entities are available?" | The AI calls `list_entities` and Department appears in the list with its properties, relationships, and data types — automatically. |
| "What departments do we have?" | The AI calls `query_entity` with `entityName=Department` and returns all department records. |
| "Which employees are in the Sales department?" | The AI calls `query_entity` with `entityName=Employee` and `filter=Department=Sales`, navigating the relationship. |
| "Create a new department called R&D with code RND, budget 800000, located in Building D" | The AI calls `create_entity` and creates the record. |

No changes were made to `SchemaDiscoveryService`, `CopilotToolsProvider`, `CopilotChatService`, or any other service file. The AI discovered Department entirely through runtime reflection.

## Integrating into an Existing XAF Application

You can add the GitHub Copilot chat assistant to your own XAF application. Below is a step-by-step guide covering what to copy, configure, and wire up.

### Prerequisites

- An existing DevExpress XAF application (EF Core, v25.2+) with Blazor Server and/or WinForms
- A GitHub account with Copilot access (Individual, Business, or Enterprise)
- .NET 10.0 SDK

### Step 1: Add NuGet Packages

Add the following packages to your **Module** project (`.csproj`):

```xml
<PackageReference Include="GitHub.Copilot.SDK" Version="0.1.24" />
<PackageReference Include="Microsoft.Extensions.AI" Version="*" />
<PackageReference Include="Markdig" Version="0.45.0" />
<PackageReference Include="HtmlSanitizer" Version="9.0.892" />
```

Add the AI chat control package to your **UI project(s)**:

```xml
<!-- Blazor Server project -->
<PackageReference Include="DevExpress.AIIntegration.Blazor.Chat" Version="25.2.*" />

<!-- WinForms project -->
<PackageReference Include="DevExpress.AIIntegration.WinForms.Chat" Version="25.2.*" />
```

### Step 2: Copy the Service Files

Copy the entire `Services/` folder from `XafGitHubCopilot.Module` into your module project. These are the seven files you need:

| File | Purpose |
|------|---------|
| `SchemaDiscoveryService.cs` | Discovers all entities, properties, relationships, and enums via `ITypesInfo` at runtime. Generates the AI system prompt dynamically. |
| `CopilotChatService.cs` | Manages the GitHub Copilot SDK client lifecycle, session creation, and streaming responses. |
| `CopilotChatClient.cs` | `IChatClient` adapter that bridges DevExpress AI chat controls to the Copilot SDK. |
| `CopilotToolsProvider.cs` | Provides three generic `AIFunction` tools (`list_entities`, `query_entity`, `create_entity`) that work with any entity. |
| `CopilotChatDefaults.cs` | Shared UI defaults: header text, empty-state text, prompt suggestions, and Markdown-to-HTML rendering. |
| `CopilotOptions.cs` | Configuration model bound from the `"Copilot"` section in `appsettings.json`. |
| `ServiceCollectionExtensions.cs` | `AddCopilotSdk()` extension method that registers all services with DI. |

**Important:** Update the namespace in `SchemaDiscoveryService.cs` line 109 to match your project:

```csharp
// Change this to your own BusinessObjects namespace
var businessObjectNamespace = "YourApp.Module.BusinessObjects";
```

Also review the `ExcludedTypeNames` set in `SchemaDiscoveryService.cs` and add any custom framework types from your application that should not be exposed to the AI (e.g., audit log entities, internal configuration objects).

### Step 3: Copy the Non-Persistent CopilotChat Object

Copy `BusinessObjects/CopilotChat.cs` — this is the XAF non-persistent `DomainComponent` that provides the navigation item for the chat view.

### Step 4: Copy the Controllers

Copy these controllers from `Controllers/`:

| File | Purpose |
|------|---------|
| `CopilotChatController.cs` | Handles navigation to the Copilot Chat view and initial setup. |
| `ModelSwitcherController.cs` | Adds a toolbar action to switch AI models at runtime. |

### Step 5: Copy the UI Editors

**For Blazor Server**, copy:
- `Editors/CopilotChatViewItem/CopilotChat.razor` — The Blazor component wrapping `DxAIChat`
- `Editors/CopilotChatViewItem/CopilotChatViewItem.cs` — The XAF ViewItem that hosts the Razor component

**For WinForms**, copy:
- `Editors/CopilotChatViewItemWin.cs` — The XAF ViewItem wrapping the DevExpress `AIChatControl`

### Step 6: Register Services at Startup

**Blazor Server** — in `Startup.cs` or `Program.cs`:

```csharp
services.AddCopilotSdk(builder.Configuration);
```

**WinForms** — in your `Startup.cs`, after the application is built:

```csharp
// In ConfigureServices or equivalent:
services.AddCopilotSdk(configuration);

// After application.Build(), wire the system prompt into the WinForms service:
var schemaService = application.ServiceProvider.GetRequiredService<SchemaDiscoveryService>();
var copilotService = application.ServiceProvider.GetRequiredService<CopilotChatService>();
copilotService.SystemMessage = schemaService.GenerateSystemPrompt();
```

### Step 7: Configure appsettings.json

Add the `"Copilot"` section:

```json
{
  "Copilot": {
    "Model": "gpt-4o",
    "UseLoggedInUser": true,
    "Streaming": true
  }
}
```

Or use a GitHub Personal Access Token instead of CLI auth:

```json
{
  "Copilot": {
    "Model": "gpt-4o",
    "GithubToken": "ghp_your_token_here"
  }
}
```

### Step 8: Run and Verify

1. Build your application
2. Run it and log in
3. Navigate to the **Copilot Chat** item in the sidebar
4. Ask: *"What entities are available in the database?"*
5. The AI should list all your business objects with their properties and relationships

### What Gets Discovered Automatically

`SchemaDiscoveryService` picks up the following from your business objects without any additional configuration:

- All persistent entity types in your `BusinessObjects` namespace
- Scalar properties with their CLR types (string, int, decimal, DateTime, bool, etc.)
- Enum properties with all possible values
- To-one navigation properties (e.g., `Order.Customer`)
- To-many collection properties (e.g., `Customer.Orders`)
- The `[DefaultProperty]` attribute is used by the tools to display human-readable names when resolving relationships

### Customizing for Your Domain

- **System prompt tone**: Edit `SchemaDiscoveryService.GenerateSystemPrompt()` to change the opening line from "order management application" to whatever fits your domain.
- **Excluded types**: Add entries to `ExcludedTypeNames` for any entities you don't want the AI to see (audit logs, internal tables, etc.).
- **Additional tools**: Add new methods to `CopilotToolsProvider` with `[Description]` attributes and register them in `CreateTools()`. For example, you could add an `update_entity` or `delete_entity` tool.
- **Prompt suggestions**: Edit `CopilotChatDefaults.PromptSuggestions` to provide domain-specific example prompts for your users.

## Roadmap

The next major improvement is **scalable AI schema discovery** — attribute-based filtering and two-tier discovery to handle large data models (100+ entities) efficiently. Instead of sending the entire schema in every system prompt, the AI will discover entity details on demand.

Key features planned:
- `[AIVisible]` attribute to control which entities and properties the AI can see
- `[AIDescription]` attribute to provide human-readable context to the AI
- Lightweight system prompt (entity names only) with a new `describe_entity` tool for on-demand detail loading

See **[TODO.md](TODO.md)** for the full implementation plan.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | DevExpress XAF 25.2.* |
| UI (Web) | Blazor Server, DevExpress `DxAIChat` |
| UI (Desktop) | WinForms, DevExpress `AIChatControl` |
| AI | GitHub Copilot SDK 0.1.23, Microsoft.Extensions.AI |
| Database | EF Core 8.0.18 + SQLite |
| Rendering | Markdig (Markdown), HtmlSanitizer (XSS protection) |
| Runtime | .NET 10.0 |

## Articles

The full implementation details are covered in this two-part series:

- [The Day I Integrated GitHub Copilot SDK Inside My XAF App — Part 1](https://www.jocheojeda.com/2026/02/16/the-day-i-integrated-github-copilot-sdk-inside-my-xaf-app-part-1/)
- [The Day I Integrated GitHub Copilot SDK Inside My XAF App — Part 2](https://www.jocheojeda.com/2026/02/16/the-day-i-integrated-github-copilot-sdk-inside-my-xaf-app-part-2/)

## License

This project is provided as a reference implementation for educational purposes.

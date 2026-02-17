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

## Data Model

A Northwind-style order management domain with 12 business entities:

| Entity | Key Properties | Relationships |
|--------|---------------|---------------|
| **Customer** | CompanyName, ContactName, Phone, Email, City, Country | has many Orders |
| **Order** | OrderDate, Status (New/Processing/Shipped/Delivered/Cancelled), Freight, ShipCity | belongs to Customer, Employee, Shipper, Invoice; has many OrderItems |
| **OrderItem** | UnitPrice, Quantity, Discount | belongs to Order, Product |
| **Product** | Name, UnitPrice, UnitsInStock, Discontinued | belongs to Category, Supplier |
| **Category** | Name, Description | has many Products |
| **Supplier** | CompanyName, ContactName, Phone, Email | has many Products |
| **Employee** | FirstName, LastName, Title, HireDate | has many Orders, Territories, DirectReports |
| **EmployeeTerritory** | (join table) | belongs to Employee, Territory |
| **Territory** | Name | belongs to Region |
| **Region** | Name | has many Territories |
| **Shipper** | CompanyName, Phone | has many Orders |
| **Invoice** | InvoiceNumber, InvoiceDate, DueDate, Status (Draft/Sent/Paid/Overdue/Cancelled) | has many Orders |

Seed data is generated automatically on first run: 20 customers, 5 employees, 3 shippers, 30 products across 8 categories, 50 orders, and 20 invoices.

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

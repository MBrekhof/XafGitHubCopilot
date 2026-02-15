# GitHub Copilot SDK Integration Plan

## Overview

Integrate the **GitHub Copilot SDK** (`GitHub.Copilot.SDK` NuGet package) into the XAF Module project to add AI-powered conversational features on top of the existing Northwind-style EF Core domain model.

---

## Existing Domain Model

| Entity | Key Properties | Relationships |
|---|---|---|
| **Customer** | CompanyName, ContactName, Phone, Email, Address, City, Country | → Orders |
| **Order** | OrderDate, RequiredDate, ShippedDate, Freight, ShipAddress, ShipCity, ShipCountry, Status | → Customer, Employee, Shipper, Invoice, OrderItems |
| **OrderItem** | UnitPrice, Quantity, Discount | → Order, Product |
| **Product** | Name, UnitPrice, UnitsInStock, Discontinued | → Category, Supplier, OrderItems |
| **Category** | Name, Description | → Products |
| **Supplier** | CompanyName, ContactName, Phone, Email, Address, City, Country | → Products |
| **Employee** | FirstName, LastName, Title, HireDate, Email, Phone, ReportsTo | → DirectReports, Territories, Orders |
| **EmployeeTerritory** | | → Employee, Territory |
| **Territory** | Name, Description | → Region, EmployeeTerritories |
| **Region** | Name, Description | → Territories |
| **Shipper** | CompanyName, Phone | → Orders |
| **Invoice** | InvoiceNumber, InvoiceDate, DueDate, Status, TotalAmount (computed) | → Orders |

### Enums

- **OrderStatus**: New, Processing, Shipped, Delivered, Cancelled
- **InvoiceStatus**: Draft, Sent, Paid, Overdue, Cancelled

---

## What's Already Done

1. **NuGet package** `GitHub.Copilot.SDK 0.1.23` added to `XafGitHubCopilot.Module.csproj`.
2. **`CopilotOptions`** — configuration POCO bound to `appsettings.json` section `"Copilot"`.
3. **`CopilotChatService`** — singleton service that manages the Copilot CLI lifecycle (auto-start, streaming, send/receive).
4. **`CopilotChatClient`** — `IChatClient` adapter so DevExpress `DxAIChat` routes messages through the Copilot SDK.
5. **`ServiceCollectionExtensions.AddCopilotSdk()`** — DI registration helper (registers options, singleton, and `IChatClient`).
6. **Blazor Server `Startup.cs`** updated to call `services.AddCopilotSdk(Configuration)` and `services.AddDevExpressAI()`.
7. **`CopilotChat`** — non-persistent `DomainComponent` business object for the chat Detail View.
8. **`IModelCopilotChatViewItem`** — XAF model interface for the custom ViewItem.
9. **`CopilotChatViewItemBlazor`** — Blazor ViewItem hosting `DxAIChat`.
10. **`CopilotChat.razor`** — Blazor component with **5 prompt suggestions**, a **system prompt** with full domain context, **Markdown rendering** (Markdig + HtmlSanitizer), and streaming.
11. **`ShowCopilotChatController`** — Window controller with navigation item + toolbar action.
12. **`SelectCopilotModelController`** — Runtime model switcher (gpt-4o, gpt-5, claude-sonnet-4, etc.).
13. **`Markdig`** and **`HtmlSanitizer`** NuGet packages added to the Blazor Server project.
14. **Northwind seed data** in `Updater.cs` — 20 customers, 5 employees, 30 products, 50 orders, 20 invoices.

---

## 5 Demo Use Cases

### Use Case 1 — Natural-Language Order Lookup

| | |
|---|---|
| **Scenario** | User types: *"Show me all orders for Contoso that are still processing"* |
| **SDK Features** | Custom **Tools** (function calling) |
| **Entities** | `Order`, `Customer` |
| **Implementation** | Register a `query_orders` tool via `AIFunctionFactory.Create`. The tool receives structured parameters (customerName, status) parsed by Copilot from the free-form prompt, runs an EF Core LINQ query, and returns the matching orders as JSON. Copilot formats the result for the user. |

#### Tool Signature

```csharp
AIFunctionFactory.Create(
    async ([Description("Customer company name")] string customerName,
           [Description("Order status filter")] string status) =>
    {
        // IObjectSpace LINQ query against Order/Customer
    },
    "query_orders",
    "Search orders by customer name and/or status"
)
```

---

### Use Case 2 — Smart Invoice Summary & Aging Report

| | |
|---|---|
| **Scenario** | User asks: *"Give me an aging summary of overdue invoices grouped by customer"* |
| **SDK Features** | Custom Tools + narrative generation |
| **Entities** | `Invoice`, `Order`, `Customer` |
| **Implementation** | Register an `invoice_aging` tool that queries `Invoice` (Status == Overdue), joins through Orders to Customer, groups by CompanyName, and sums TotalAmount. Copilot receives the raw data and narrates it with insights and recommended actions. |

#### Tool Signature

```csharp
AIFunctionFactory.Create(
    async ([Description("Invoice status to filter")] string status) =>
    {
        // Group overdue invoices by customer, return summary
    },
    "invoice_aging",
    "Get invoice aging summary grouped by customer"
)
```

---

### Use Case 3 — Product Restock Advisor

| | |
|---|---|
| **Scenario** | User asks: *"Which products are running low and who supplies them?"* |
| **SDK Features** | **Streaming** + content generation |
| **Entities** | `Product`, `Supplier`, `Category` |
| **Implementation** | Register a `low_stock_products` tool that queries non-discontinued products where UnitsInStock < threshold, joins Supplier and Category. Copilot streams back a restock recommendation and drafts a supplier email with contact info. |

#### Tool Signature

```csharp
AIFunctionFactory.Create(
    async ([Description("Stock threshold")] int threshold) =>
    {
        // Query low-stock products with supplier details
    },
    "low_stock_products",
    "Find products with stock below a threshold, including supplier info"
)
```

---

### Use Case 4 — Employee Performance / Territory Insights

| | |
|---|---|
| **Scenario** | User asks: *"How are my sales reps performing this quarter? Who covers the most territories?"* |
| **SDK Features** | **Multiple tool calls** in one turn + cross-referencing |
| **Entities** | `Employee`, `Order`, `EmployeeTerritory` |
| **Implementation** | Two tools: `employee_order_stats` (aggregates order count and total freight per employee for a date range) and `employee_territories` (returns territory count per employee). Copilot calls both, correlates the data, and produces a ranked leaderboard. |

#### Tool Signatures

```csharp
// Tool 1
AIFunctionFactory.Create(
    async ([Description("Start date (ISO)")] string fromDate,
           [Description("End date (ISO)")] string toDate) =>
    {
        // Aggregate orders per employee in date range
    },
    "employee_order_stats",
    "Get order count and total freight per employee for a date range"
)

// Tool 2
AIFunctionFactory.Create(
    async () =>
    {
        // Count territories per employee
    },
    "employee_territories",
    "List employees with their territory counts"
)
```

---

### Use Case 5 — Conversational Order Creation

| | |
|---|---|
| **Scenario** | User says: *"Create an order for customer Acme Corp, 10 units of Widget Pro, ship via FastShip"* |
| **SDK Features** | **OnUserInputRequest** (ask_user) + write-back tool |
| **Entities** | `Order`, `OrderItem`, `Customer`, `Product`, `Shipper` |
| **Implementation** | Register a `create_order` tool that accepts customer name, product name, quantity, and shipper name. If ambiguous matches are found (e.g., two products starting with "Widget"), Copilot uses `OnUserInputRequest` to ask the user for clarification. Once confirmed, the tool creates the Order + OrderItem via IObjectSpace, links all relationships, and saves. Copilot confirms with a summary. |

#### Tool Signature

```csharp
AIFunctionFactory.Create(
    async ([Description("Customer company name")] string customerName,
           [Description("Product name")] string productName,
           [Description("Quantity")] int quantity,
           [Description("Shipper company name")] string shipperName) =>
    {
        // Resolve entities, create Order + OrderItem, save via IObjectSpace
    },
    "create_order",
    "Create a new order with line items for a customer"
)
```

#### OnUserInputRequest Handler

```csharp
OnUserInputRequest = async (request, invocation) =>
{
    // Present choices to the user when multiple matches are found
    // e.g., "Did you mean Widget Pro ($25) or Widget Plus ($30)?"
    return new UserInputResponse
    {
        Answer = selectedChoice,
        WasFreeform = false
    };
}
```

---

## Implementation Roadmap

### Phase 1 — Foundation (already done)

- [x] Add `GitHub.Copilot.SDK` NuGet package
- [x] Create `CopilotOptions` configuration class
- [x] Create `CopilotChatService` singleton
- [x] Create `CopilotChatClient` IChatClient adapter
- [x] Create `ServiceCollectionExtensions.AddCopilotSdk()`
- [x] Register services in Blazor Server `Startup.cs`
- [x] Create `CopilotChat` non-persistent business object
- [x] Create `IModelCopilotChatViewItem` model interface
- [x] Create `CopilotChatViewItemBlazor` ViewItem
- [x] Create `ShowCopilotChatController` (navigation + action)
- [x] Create `SelectCopilotModelController` (model switcher)
- [x] Seed Northwind data in `Updater.cs`

### Phase 2 — Prompt Suggestions & System Prompt (done)

- [x] Add 5 prompt suggestions mapped to the demo use cases
- [x] Add system prompt with full domain model context
- [x] Add Markdown rendering (Markdig + HtmlSanitizer)
- [x] Add `Markdig` and `HtmlSanitizer` NuGet packages

### Phase 3 — Read-Only Tools (Use Cases 1, 2, 3, 4)

- [ ] Create a `CopilotToolsProvider` class in the Module that registers all tools
- [ ] Implement `query_orders` tool (Use Case 1)
- [ ] Implement `invoice_aging` tool (Use Case 2)
- [ ] Implement `low_stock_products` tool (Use Case 3)
- [ ] Implement `employee_order_stats` tool (Use Case 4)
- [ ] Implement `employee_territories` tool (Use Case 4)
- [ ] Create an XAF controller with a UI action to open a Copilot chat panel

### Phase 4 — Write-Back & Conversation (Use Case 5)

- [ ] Implement `create_order` tool with IObjectSpace write-back
- [ ] Wire up `OnUserInputRequest` for disambiguation
- [ ] Add validation and error handling for entity resolution

### Phase 5 — Polish & Demo

- [ ] Add system message customization for the business domain context
- [x] Seed the database with sample Northwind data (Updater.cs)
- [ ] Build a demo script walking through all 5 use cases
- [ ] Test streaming UX in the Blazor chat panel

---

## Configuration (appsettings.json)

```json
{
  "Copilot": {
    "Model": "gpt-5",
    "Streaming": true,
    "UseLoggedInUser": true
  }
}
```

> Set `GithubToken` in user secrets for PAT-based auth, or rely on `UseLoggedInUser: true` with `gh auth login`.

---

## Prerequisites

- .NET 8.0+
- GitHub Copilot CLI installed and on PATH (`npm install -g @githubnext/github-copilot-cli` or equivalent)
- Authenticated via `gh auth login`

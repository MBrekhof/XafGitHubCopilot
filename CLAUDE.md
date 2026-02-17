# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DevExpress XAF application integrating the GitHub Copilot SDK to provide an in-app AI assistant that queries live business data, creates records conversationally, and works on both Blazor Server and WinForms. Uses a Northwind-style domain (orders, customers, products, employees, invoices) seeded automatically on first run.

## Build & Run Commands

```bash
# Restore and build the entire solution
dotnet build XafGitHubCopilot/XafGitHubCopilot.slnx

# Run the Blazor Server app (primary development target)
dotnet run --project XafGitHubCopilot/XafGitHubCopilot.Blazor.Server

# Run the WinForms app (Windows only)
dotnet run --project XafGitHubCopilot/XafGitHubCopilot.Win

# Build a specific project
dotnet build XafGitHubCopilot/XafGitHubCopilot.Module/XafGitHubCopilot.Module.csproj
```

There is no formal test suite configured. The `ConsoleTest` project is a minimal console app for ad-hoc testing.

## Architecture

### Solution Structure (3-tier XAF pattern)

- **`XafGitHubCopilot.Module/`** — Platform-agnostic core: business objects (EF Core entities), XAF controllers, and all Copilot SDK integration services. Both UI projects reference this.
- **`XafGitHubCopilot.Blazor.Server/`** — Blazor Server UI. Entry point: `Program.cs` → `Startup.cs`. Uses DevExpress `DxAIChat` Blazor component for the chat UI (`Editors/CopilotChatViewItem/CopilotChat.razor`).
- **`XafGitHubCopilot.Win/`** — WinForms UI (net10.0-windows). Uses DevExpress `AIChatControl` for the chat UI.

### GitHub Copilot SDK Integration (Module/Services/)

The integration chain flows:

1. **`ServiceCollectionExtensions.AddCopilotSdk()`** — Registers all services. Called from both Blazor `Startup.cs` and WinForms `Startup.cs`.
2. **`CopilotChatService`** — Singleton managing the `CopilotClient` lifecycle. Handles session creation, streaming via SDK events (`AssistantMessageDeltaEvent`, `SessionIdleEvent`), and a 2-minute timeout. Lazy-starts on first request.
3. **`CopilotChatClient`** — `IChatClient` adapter bridging DevExpress AI chat controls to the Copilot SDK. This is what DxAIChat/AIChatControl resolves via DI.
4. **`CopilotToolsProvider`** — Creates `AIFunction` tools for function calling. Tools use `ScopedObjectSpace` (DI scope + `INonSecuredObjectSpaceFactory`) for database access. Six tools: `query_orders`, `invoice_aging`, `low_stock_products`, `employee_order_stats`, `employee_territories`, `create_order`.
5. **`CopilotChatDefaults`** — Shared UI config (system prompt, prompt suggestions, Markdown→HTML rendering via Markdig + HtmlSanitizer).
6. **`CopilotOptions`** — Bound from `appsettings.json` section `"Copilot"`. Keys: `Model` (default "gpt-4o"), `GithubToken`, `CliPath`, `UseLoggedInUser` (default true), `Streaming` (default true).

### Key Patterns

- **Non-Persistent Business Objects**: `CopilotChat` is a `DomainComponent` (not stored in DB) — it exists only to host the chat ViewItem in XAF's navigation.
- **ScopedObjectSpace pattern**: Tool methods in `CopilotToolsProvider` create a DI scope + non-secured ObjectSpace per call, disposed after use. This is required because tools run outside the normal XAF request lifecycle.
- **Model switching at runtime**: `SelectCopilotModelController` lets users switch AI models (gpt-4o, gpt-5, claude-sonnet-4, etc.) via a `SingleChoiceAction` that sets `CopilotChatService.CurrentModel`.
- **XAF Model Differences**: `Model.DesignedDiffs.xafml` (embedded in Module) and `Model.xafml` (copied to output in UI projects) configure XAF views, navigation, and layout.

### Database

- EF Core 8.0.18 with SQLite (`XafGitHubCopilot.db`) for development
- DbContext: `XafGitHubCopilotEFCoreDbContext`
- Auto-migration via XAF's `ModuleUpdater` pattern (`DatabaseUpdate/Updater.cs`)
- 14 entities: Order, OrderItem, Customer, Product, Category, Supplier, Employee, EmployeeTerritory, Territory, Region, Shipper, Invoice, ApplicationUser, ApplicationUserLoginInfo
- Seed data: 20 customers, 5 employees, 30 products, 50 orders, 20 invoices, test users "User"/"Admin" (empty passwords in debug)

## Tech Stack

- .NET 10.0 (net10.0 / net10.0-windows)
- DevExpress XAF 25.2.*, DevExpress AI Integration 25.2.*
- GitHub Copilot SDK 0.1.23, Microsoft.Extensions.AI
- EF Core 8.0.18 + SQLite
- Markdig + HtmlSanitizer for Markdown rendering

## Configuration

Add a `"Copilot"` section to `appsettings.json` to override defaults:
```json
{
  "Copilot": {
    "Model": "gpt-4o",
    "GithubToken": null,
    "UseLoggedInUser": true,
    "Streaming": true
  }
}
```

Authentication requires either a GitHub PAT (`GithubToken`) or an active GitHub CLI login (`UseLoggedInUser: true`).

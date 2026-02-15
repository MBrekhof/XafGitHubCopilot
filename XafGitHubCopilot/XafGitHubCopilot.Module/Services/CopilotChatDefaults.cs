using System.Collections.Generic;
using Markdig;
using Ganss.Xss;

namespace XafGitHubCopilot.Module.Services
{
    /// <summary>
    /// Single source of truth for Copilot chat UI configuration shared
    /// across Blazor and WinForms platform projects.
    /// </summary>
    public static class CopilotChatDefaults
    {
        // ── Header / Empty State ──────────────────────────────────────────

        public const string HeaderText = "Copilot Assistant";

        public const string EmptyStateText =
            "Ask me anything about your data — orders, invoices, products, employees & more.\nPowered by GitHub Copilot SDK.";

        // ── Prompt Suggestions ────────────────────────────────────────────

        /// <summary>
        /// Lightweight DTO used by both platforms to build their
        /// native suggestion controls.
        /// </summary>
        public record PromptSuggestionItem(string Title, string Text, string Prompt);

        public static IReadOnlyList<PromptSuggestionItem> PromptSuggestions { get; } = new List<PromptSuggestionItem>
        {
            // Use Case 1 — Natural-Language Order Lookup
            new("Order Lookup",
                "Search orders by customer name or status",
                "Show me all orders for Around the Horn that are still processing"),

            // Use Case 2 — Invoice Aging Report
            new("Invoice Aging",
                "Overdue invoices grouped by customer",
                "Give me an aging summary of overdue invoices grouped by customer, including totals and recommendations"),

            // Use Case 3 — Product Restock Advisor
            new("Restock Advisor",
                "Low-stock products with supplier contacts",
                "Which products have fewer than 20 units in stock and are not discontinued? Include the supplier name and contact info"),

            // Use Case 4 — Employee Performance
            new("Sales Leaderboard",
                "Employee order stats and territory coverage",
                "How are the sales reps performing? Rank them by number of orders and show how many territories each one covers"),

            // Use Case 5 — Create an Order
            new("Create Order",
                "Conversational order entry via AI",
                "Create a new order for customer Alfreds Futterkiste: 10 units of Chai and 5 units of Chang, ship via Speedy Express")
        };

        // ── System Prompt ─────────────────────────────────────────────────

        public const string SystemPrompt = @"
You are a helpful business assistant for a Northwind-style order management application.
The database contains these entities:

- **Customer** (CompanyName, ContactName, Phone, Email, City, Country) -> has many Orders
- **Order** (OrderDate, RequiredDate, ShippedDate, Freight, ShipAddress, ShipCity, ShipCountry, Status) -> belongs to Customer, Employee, Shipper, Invoice; has many OrderItems
  - OrderStatus values: New, Processing, Shipped, Delivered, Cancelled
- **OrderItem** (UnitPrice, Quantity, Discount) -> belongs to Order and Product
- **Product** (Name, UnitPrice, UnitsInStock, Discontinued) -> belongs to Category, Supplier
- **Category** (Name, Description) -> has many Products
- **Supplier** (CompanyName, ContactName, Phone, Email, City, Country) -> has many Products
- **Employee** (FirstName, LastName, Title, HireDate, Email, Phone) -> has many Orders, Territories, DirectReports; may report to another Employee
- **EmployeeTerritory** -> links Employee to Territory
- **Territory** (Name) -> belongs to Region
- **Region** (Name)
- **Shipper** (CompanyName, Phone) -> has many Orders
- **Invoice** (InvoiceNumber, InvoiceDate, DueDate, Status, computed TotalAmount) -> has many Orders
  - InvoiceStatus values: Draft, Sent, Paid, Overdue, Cancelled

Seeded data includes:
- 20 customers (Alfreds Futterkiste, Around the Horn, Ernst Handel, etc.)
- 5 employees (Nancy Davolio, Andrew Fuller, Janet Leverling, Margaret Peacock, Steven Buchanan)
- 3 shippers (Speedy Express, United Package, Federal Shipping)
- 30 products across 8 categories from 10 suppliers
- 50 orders with 2-4 line items each
- 20 invoices

When answering:
- Use Markdown formatting for readability (tables, bold, lists).
- When asked about data, write realistic output that matches the schema and seed data above.
- When asked to create records, describe the steps and confirm before proceeding.
- Be concise but thorough.
";

        // ── Markdown → HTML ───────────────────────────────────────────────

        private static readonly HtmlSanitizer Sanitizer = new();

        /// <summary>
        /// Converts a Markdown string to sanitized HTML.
        /// Thread-safe — the sanitizer instance is reentrant.
        /// </summary>
        public static string ConvertMarkdownToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var html = Markdown.ToHtml(markdown);
            return Sanitizer.Sanitize(html);
        }
    }
}

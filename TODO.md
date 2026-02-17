# TODO

## Scalable AI Schema Discovery: Attribute-Based Filtering & Two-Tier Discovery

**Problem:** With 100+ entities averaging 25 properties each, the current approach sends the entire schema (~2500+ property definitions) in the system prompt with every single message. This causes unnecessary token cost, increased latency, and may hit context window limits on some models.

**Solution:** Combine two complementary strategies — attribute-based filtering to control *what* the AI can see, and two-tier discovery to control *when* details are loaded.

---

### Phase 1: `[AIVisible]` and `[AIDescription]` Attributes

Create generic attributes (not Copilot-specific, so they work with any AI integration) that developers place on entities and properties to control AI discoverability.

- [ ] **Create `AIVisibleAttribute`** — applies to classes and properties
  - `[AIVisible]` on a class → include this entity in AI discovery (opt-in at entity level)
  - `[AIVisible]` on a property → include this property (when entity-level filtering is active)
  - `[AIVisible(false)]` on a property → explicitly exclude a property even if the entity is visible
  - If no entity in the model has `[AIVisible]`, fall back to current behavior (discover all entities) for backward compatibility
  - File: `Module/Attributes/AIVisibleAttribute.cs`

- [ ] **Create `AIDescriptionAttribute`** — applies to classes and properties
  - Provides a human-readable description that the AI sees instead of just the property/entity name
  - `[AIDescription("Company departments with budget tracking and employee assignments")]` on a class
  - `[AIDescription("Annual operating budget in USD")]` on a property
  - When present, the description is included in both the system prompt and tool output
  - File: `Module/Attributes/AIDescriptionAttribute.cs`

- [ ] **Update `SchemaDiscoveryService.Discover()`** to respect the new attributes
  - If any entity has `[AIVisible]`, switch to opt-in mode: only discover entities with `[AIVisible]`
  - Filter out properties with `[AIVisible(false)]`
  - Read `[AIDescription]` values and include them in `EntityInfo` / `EntityPropertyInfo`
  - Remove the hardcoded `ExcludedTypeNames` set — replace with `[AIVisible(false)]` or simply omit `[AIVisible]` on framework types

- [ ] **Update `SchemaInfo` model classes** to carry description metadata
  - Add `Description` property to `EntityInfo`
  - Add `Description` property to `EntityPropertyInfo`

- [ ] **Decorate existing business objects** with the new attributes as a working example
  - Add `[AIVisible]` and `[AIDescription]` to a few entities (e.g., Department, Employee, Order)
  - Add `[AIVisible(false)]` to a few internal properties to demonstrate exclusion

---

### Phase 2: Two-Tier Discovery (Lightweight System Prompt + On-Demand Details)

Restructure the system prompt to be lightweight, and move detailed schema information into a tool that the AI calls only when needed.

- [ ] **Slim down `GenerateSystemPrompt()`**
  - Only include entity names and their `[AIDescription]` text (one line per entity)
  - Do NOT include property lists, relationships, or enum values in the system prompt
  - Example output:
    ```
    Available entities: Department (company departments with budget tracking),
    Employee (staff members with department and territory assignments),
    Order (customer orders with status tracking), ...
    ```

- [ ] **Add `describe_entity` tool to `CopilotToolsProvider`**
  - Takes an entity name as parameter
  - Returns full property details, types, enum values, and relationships for that single entity
  - The AI calls this when it needs to understand an entity's structure before querying or creating
  - Register in `CreateTools()` alongside the existing three tools

- [ ] **Update tool `[Description]` attributes** to guide the AI toward the new flow
  - `query_entity` description: mention calling `describe_entity` first if unsure about property names
  - `create_entity` description: mention calling `describe_entity` first to see required fields

---

### Phase 3: Testing & Validation

- [ ] **Verify backward compatibility** — when no `[AIVisible]` attributes are present, behavior should be identical to current implementation
- [ ] **Test with attribute-decorated entities** — confirm only visible entities/properties appear in prompts and tool output
- [ ] **Measure token reduction** — compare system prompt size before and after (target: 80%+ reduction for large models)
- [ ] **Test the AI conversation flow** — verify the AI correctly calls `describe_entity` before querying unfamiliar entities

---

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Attribute naming | `AIVisible` / `AIDescription` (not `CopilotVisible`) | Generic — works with any AI backend, not tied to GitHub Copilot |
| Entity filtering mode | Opt-in when any `[AIVisible]` exists, discover-all otherwise | Backward compatible; existing projects work without changes |
| Property filtering | Opt-out (`[AIVisible(false)]` to exclude) | Most properties should be visible; excluding is the exception |
| System prompt content | Entity names + descriptions only | Minimizes per-message token cost; details loaded on demand |
| Detail loading | `describe_entity` tool | AI decides when it needs details; no unnecessary data transfer |

### Files to Create

| File | Purpose |
|------|---------|
| `Module/Attributes/AIVisibleAttribute.cs` | Controls entity/property discoverability |
| `Module/Attributes/AIDescriptionAttribute.cs` | Provides AI-readable descriptions |

### Files to Modify

| File | Changes |
|------|---------|
| `Module/Services/SchemaDiscoveryService.cs` | Respect attributes in `Discover()`, slim down `GenerateSystemPrompt()`, add `Description` to model classes |
| `Module/Services/CopilotToolsProvider.cs` | Add `describe_entity` tool, update existing tool descriptions |
| `Module/BusinessObjects/*.cs` | Decorate entities with `[AIVisible]` and `[AIDescription]` as examples |

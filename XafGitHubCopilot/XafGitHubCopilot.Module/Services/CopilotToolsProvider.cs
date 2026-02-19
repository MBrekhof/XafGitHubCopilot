using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace XafGitHubCopilot.Module.Services
{
    /// <summary>
    /// Creates generic <see cref="AIFunction"/> tools that work with any entity
    /// discovered by <see cref="SchemaDiscoveryService"/>.
    /// Pattern: <c>[Description]</c> on method + params, <c>AIFunctionFactory.Create(method, name)</c>.
    /// </summary>
    public sealed class CopilotToolsProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SchemaDiscoveryService _schemaService;
        private readonly ILogger<CopilotToolsProvider> _logger;
        private List<AIFunction> _tools;

        public CopilotToolsProvider(IServiceProvider serviceProvider, SchemaDiscoveryService schemaService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _schemaService = schemaService ?? throw new ArgumentNullException(nameof(schemaService));
            _logger = serviceProvider.GetRequiredService<ILogger<CopilotToolsProvider>>();
        }

        public IReadOnlyList<AIFunction> Tools => _tools ??= CreateTools();

        private List<AIFunction> CreateTools() =>
        [
            AIFunctionFactory.Create(ListEntities, "list_entities"),
            AIFunctionFactory.Create(DescribeEntity, "describe_entity"),
            AIFunctionFactory.Create(QueryEntity, "query_entity"),
            AIFunctionFactory.Create(CreateEntity, "create_entity"),
        ];

        // -- Helpers ---------------------------------------------------------------

        /// <summary>
        /// Creates a DI scope + non-secured object space for the given entity type.
        /// Callers MUST dispose the returned <see cref="ScopedObjectSpace"/>
        /// which disposes both the object space and the scope.
        /// </summary>
        private ScopedObjectSpace GetObjectSpace(Type entityType)
        {
            var scope = _serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<INonSecuredObjectSpaceFactory>();
            var os = factory.CreateNonSecuredObjectSpace(entityType);
            return new ScopedObjectSpace(os, scope);
        }

        /// <summary>Wraps an IObjectSpace + IServiceScope for joint disposal.</summary>
        private sealed class ScopedObjectSpace : IDisposable
        {
            public IObjectSpace Os { get; }
            private readonly IServiceScope _scope;

            public ScopedObjectSpace(IObjectSpace os, IServiceScope scope)
            {
                Os = os;
                _scope = scope;
            }

            public void Dispose()
            {
                Os.Dispose();
                _scope.Dispose();
            }
        }

        /// <summary>
        /// Returns a comma-separated list of all known entity names.
        /// </summary>
        private string GetEntityNameList() =>
            string.Join(", ", _schemaService.Schema.Entities.Select(e => e.Name));

        /// <summary>
        /// Formats a single entity object as a line of "Property: Value" pairs
        /// using XAF <see cref="ITypeInfo"/> metadata.
        /// </summary>
        private string FormatObject(object obj, EntityInfo entityInfo, ITypeInfo typeInfo)
        {
            var parts = new List<string>();
            foreach (var prop in entityInfo.Properties)
            {
                var member = typeInfo.FindMember(prop.Name);
                if (member == null) continue;
                var val = member.GetValue(obj);
                parts.Add($"{prop.Name}: {FormatValue(val)}");
            }
            // Include to-one relationship references (show a summary, not the whole object)
            foreach (var rel in entityInfo.Relationships.Where(r => !r.IsCollection))
            {
                var member = typeInfo.FindMember(rel.PropertyName);
                if (member == null) continue;
                var refObj = member.GetValue(obj);
                if (refObj != null)
                    parts.Add($"{rel.PropertyName}: {GetObjectDisplayText(refObj)}");
            }
            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Attempts to produce a human-readable label for an entity object
        /// by looking for common "name" properties.
        /// </summary>
        private static string GetObjectDisplayText(object obj)
        {
            if (obj == null) return "null";
            var type = obj.GetType();
            // Try common name properties
            foreach (var propName in new[] { "Name", "CompanyName", "Title", "FullName", "FirstName", "Description", "InvoiceNumber" })
            {
                var prop = type.GetProperty(propName);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val != null) return val.ToString();
                }
            }
            return obj.ToString();
        }

        private static string FormatValue(object val)
        {
            if (val == null) return "N/A";
            if (val is DateTime dt) return dt.ToString("yyyy-MM-dd");
            if (val is decimal d) return d.ToString("F2");
            if (val is double dbl) return dbl.ToString("F2");
            if (val is float f) return f.ToString("F2");
            return val.ToString();
        }

        /// <summary>
        /// Parses "Key=Value;Key2=Value2" into a list of key-value pairs.
        /// </summary>
        private static List<(string Key, string Value)> ParsePairs(string input)
        {
            var pairs = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(input)) return pairs;
            foreach (var segment in input.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = segment.IndexOf('=');
                if (eqIndex <= 0) continue;
                var key = segment.Substring(0, eqIndex).Trim();
                var value = segment.Substring(eqIndex + 1).Trim();
                if (!string.IsNullOrEmpty(key))
                    pairs.Add((key, value));
            }
            return pairs;
        }

        /// <summary>
        /// Converts a string value to the target CLR type, handling enums, dates,
        /// numbers, booleans, and nullable wrappers.
        /// </summary>
        private static object ConvertValue(string value, Type targetType)
        {
            if (value == null) return null;

            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying != null)
            {
                if (string.IsNullOrWhiteSpace(value)) return null;
                return ConvertValue(value, underlying);
            }

            if (targetType == typeof(string)) return value;
            if (targetType.IsEnum) return Enum.Parse(targetType, value, ignoreCase: true);
            if (targetType == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal)) return decimal.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(float)) return float.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool)) return bool.Parse(value);
            if (targetType == typeof(Guid)) return Guid.Parse(value);

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        // -- Tool implementations --------------------------------------------------

        [Description("List all available entities (tables) in the database with their properties and relationships.")]
        private string ListEntities()
        {
            _logger.LogInformation("[Tool:list_entities] Called");
            try
            {
                var schema = _schemaService.Schema;
                var sb = new StringBuilder();
                sb.AppendLine("Available entities:");

                foreach (var entity in schema.Entities)
                {
                    var props = string.Join(", ", entity.Properties.Select(p => p.Name));
                    sb.Append($"- {entity.Name} ({props})");

                    var rels = entity.Relationships;
                    if (rels.Count > 0)
                    {
                        var relDescriptions = rels.Select(r =>
                            r.IsCollection ? $"has many {r.TargetEntity}" : $"belongs to {r.TargetEntity}");
                        sb.Append($" -> {string.Join(", ", relDescriptions)}");
                    }
                    sb.AppendLine();

                    // Enum values for properties
                    foreach (var p in entity.Properties.Where(p => p.EnumValues.Count > 0))
                        sb.AppendLine($"  - {p.Name} values: {string.Join(", ", p.EnumValues)}");
                }

                var result = sb.ToString();
                _logger.LogInformation("[Tool:list_entities] Returning {Len} chars", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Tool:list_entities] Error");
                return $"Error listing entities: {ex.Message}";
            }
        }

        [Description("Get full schema details for a single entity — properties, types, relationships, and enum values. Call this before querying or creating records of an unfamiliar entity.")]
        private string DescribeEntity(
            [Description("Entity name to describe (e.g. 'Customer', 'Order'). Use list_entities to see available names.")] string entityName)
        {
            _logger.LogInformation("[Tool:describe_entity] Called with entity={Entity}", entityName);
            try
            {
                if (string.IsNullOrWhiteSpace(entityName))
                    return $"Entity name is required. Available entities: {GetEntityNameList()}";

                var entityInfo = _schemaService.Schema.FindEntity(entityName);
                if (entityInfo == null)
                    return $"Entity '{entityName}' not found. Available entities: {GetEntityNameList()}";

                var sb = new StringBuilder();
                sb.AppendLine($"**{entityInfo.Name}**");
                if (!string.IsNullOrEmpty(entityInfo.Description))
                    sb.AppendLine(entityInfo.Description);
                sb.AppendLine();

                // Properties
                sb.AppendLine("Properties:");
                foreach (var prop in entityInfo.Properties)
                {
                    var required = prop.IsRequired ? " (required)" : "";
                    var desc = !string.IsNullOrEmpty(prop.Description) ? $" — {prop.Description}" : "";
                    sb.AppendLine($"  - {prop.Name}: {prop.TypeName}{required}{desc}");

                    if (prop.EnumValues.Count > 0)
                        sb.AppendLine($"    Values: {string.Join(", ", prop.EnumValues)}");
                }

                // Relationships
                if (entityInfo.Relationships.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Relationships:");
                    foreach (var rel in entityInfo.Relationships)
                    {
                        var kind = rel.IsCollection ? "has many" : "belongs to";
                        sb.AppendLine($"  - {rel.PropertyName}: {kind} {rel.TargetEntity}");
                    }
                }

                var result = sb.ToString();
                _logger.LogInformation("[Tool:describe_entity] Returning {Len} chars for {Entity}", result.Length, entityName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Tool:describe_entity] Error");
                return $"Error describing {entityName}: {ex.Message}";
            }
        }

        [Description("Query records of any entity (table) in the database. Call describe_entity first if you are unsure about property names or types.")]
        private string QueryEntity(
            [Description("Entity name to query (e.g. 'Customer', 'Order', 'Product'). Use list_entities to see available names.")] string entityName,
            [Description("Optional filter as semicolon-separated 'PropertyName=value' pairs. Example: 'Status=New;Country=USA'. Omit for no filter.")] string filter = "",
            [Description("Maximum number of records to return. Default is 25.")] int top = 25)
        {
            _logger.LogInformation("[Tool:query_entity] Called with entity={Entity}, filter={Filter}, top={Top}", entityName, filter, top);
            try
            {
                if (string.IsNullOrWhiteSpace(entityName))
                    return $"Entity name is required. Available entities: {GetEntityNameList()}";

                var entityInfo = _schemaService.Schema.FindEntity(entityName);
                if (entityInfo == null)
                    return $"Entity '{entityName}' not found. Available entities: {GetEntityNameList()}";

                var entityType = entityInfo.ClrType;
                if (top <= 0) top = 25;

                using var sos = GetObjectSpace(entityType);
                var os = sos.Os;
                var typeInfo = XafTypesInfo.Instance.FindTypeInfo(entityType);

                // Retrieve all objects of this type
                var allObjects = os.GetObjects(entityType);
                IEnumerable<object> results = allObjects.Cast<object>();

                // Apply in-memory filters
                var filterPairs = ParsePairs(filter);
                foreach (var (key, value) in filterPairs)
                {
                    // Try scalar property first
                    var propInfo = entityInfo.Properties
                        .FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (propInfo != null)
                    {
                        var member = typeInfo.FindMember(propInfo.Name);
                        if (member != null)
                        {
                            // For string properties, use Contains (case-insensitive)
                            if (propInfo.ClrType == typeof(string))
                            {
                                results = results.Where(o =>
                                {
                                    var v = member.GetValue(o) as string;
                                    return v != null && v.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
                                });
                            }
                            else
                            {
                                try
                                {
                                    var converted = ConvertValue(value, propInfo.ClrType);
                                    results = results.Where(o => Equals(member.GetValue(o), converted));
                                }
                                catch
                                {
                                    return $"Cannot convert filter value '{value}' to type '{propInfo.TypeName}' for property '{key}'.";
                                }
                            }
                        }
                        continue;
                    }

                    // Try relationship (to-one navigation) — match by display text
                    var relInfo = entityInfo.Relationships
                        .FirstOrDefault(r => !r.IsCollection && r.PropertyName.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (relInfo != null)
                    {
                        var member = typeInfo.FindMember(relInfo.PropertyName);
                        if (member != null)
                        {
                            results = results.Where(o =>
                            {
                                var refObj = member.GetValue(o);
                                if (refObj == null) return false;
                                var display = GetObjectDisplayText(refObj);
                                return display.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
                            });
                        }
                        continue;
                    }

                    var availableProps = string.Join(", ", entityInfo.Properties.Select(p => p.Name)
                        .Concat(entityInfo.Relationships.Where(r => !r.IsCollection).Select(r => r.PropertyName)));
                    return $"Property '{key}' not found on {entityInfo.Name}. Available: {availableProps}";
                }

                var list = results.Take(top).ToList();
                if (list.Count == 0) return $"No {entityInfo.Name} records found matching the given criteria.";

                var sb = new StringBuilder();
                sb.AppendLine($"Found {list.Count} {entityInfo.Name} record(s):");
                foreach (var obj in list)
                    sb.AppendLine(FormatObject(obj, entityInfo, typeInfo));

                var result = sb.ToString();
                _logger.LogInformation("[Tool:query_entity] Returning {Len} chars, {Count} records", result.Length, list.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Tool:query_entity] Error");
                return $"Error querying {entityName}: {ex.Message}";
            }
        }

        [Description("Create a new record of any entity in the database. Call describe_entity first to see required fields, property types, and relationships.")]
        private string CreateEntity(
            [Description("Entity name to create (e.g. 'Customer', 'Order', 'Product'). Use list_entities to see available names.")] string entityName,
            [Description("Semicolon-separated 'PropertyName=value' pairs. For reference properties (relationships), provide a search term to match by name. Example: 'CompanyName=Acme Corp;Country=USA' or 'Customer=Acme;Status=New'.")] string properties)
        {
            _logger.LogInformation("[Tool:create_entity] Called with entity={Entity}, properties={Props}", entityName, properties);
            try
            {
                if (string.IsNullOrWhiteSpace(entityName))
                    return $"Entity name is required. Available entities: {GetEntityNameList()}";

                var entityInfo = _schemaService.Schema.FindEntity(entityName);
                if (entityInfo == null)
                    return $"Entity '{entityName}' not found. Available entities: {GetEntityNameList()}";

                if (string.IsNullOrWhiteSpace(properties))
                {
                    var availableProps = string.Join(", ", entityInfo.Properties.Select(p => p.Name));
                    var availableRels = string.Join(", ", entityInfo.Relationships.Where(r => !r.IsCollection).Select(r => r.PropertyName));
                    return $"Properties are required. {entityInfo.Name} properties: {availableProps}" +
                           (string.IsNullOrEmpty(availableRels) ? "" : $". Relationships: {availableRels}");
                }

                var entityType = entityInfo.ClrType;
                using var sos = GetObjectSpace(entityType);
                var os = sos.Os;
                var typeInfo = XafTypesInfo.Instance.FindTypeInfo(entityType);

                var obj = os.CreateObject(entityType);
                var pairs = ParsePairs(properties);
                var setProperties = new List<string>();

                foreach (var (key, value) in pairs)
                {
                    // Check if it's a scalar property
                    var propInfo = entityInfo.Properties
                        .FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (propInfo != null)
                    {
                        var member = typeInfo.FindMember(propInfo.Name);
                        if (member != null)
                        {
                            try
                            {
                                var converted = ConvertValue(value, propInfo.ClrType);
                                member.SetValue(obj, converted);
                                setProperties.Add($"{propInfo.Name}: {FormatValue(converted)}");
                            }
                            catch (Exception ex)
                            {
                                return $"Error setting {propInfo.Name}: cannot convert '{value}' to {propInfo.TypeName}. {ex.Message}";
                            }
                        }
                        continue;
                    }

                    // Check if it's a to-one relationship
                    var relInfo = entityInfo.Relationships
                        .FirstOrDefault(r => !r.IsCollection && r.PropertyName.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (relInfo != null)
                    {
                        // Look up the referenced entity by searching for a natural key match
                        var refObjects = os.GetObjects(relInfo.TargetClrType);
                        object matched = null;
                        foreach (var refObj in refObjects)
                        {
                            var display = GetObjectDisplayText(refObj);
                            if (display.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matched = refObj;
                                break;
                            }
                        }

                        if (matched == null)
                        {
                            // List some available values to help the user
                            var available = refObjects.Cast<object>()
                                .Take(10)
                                .Select(GetObjectDisplayText);
                            return $"{relInfo.PropertyName} '{value}' not found. Available {relInfo.TargetEntity} records: {string.Join(", ", available)}";
                        }

                        var member = typeInfo.FindMember(relInfo.PropertyName);
                        if (member != null)
                        {
                            member.SetValue(obj, matched);
                            setProperties.Add($"{relInfo.PropertyName}: {GetObjectDisplayText(matched)}");
                        }
                        continue;
                    }

                    // Property not found
                    var allProps = string.Join(", ", entityInfo.Properties.Select(p => p.Name)
                        .Concat(entityInfo.Relationships.Where(r => !r.IsCollection).Select(r => r.PropertyName)));
                    return $"Property '{key}' not found on {entityInfo.Name}. Available: {allProps}";
                }

                os.CommitChanges();

                var summary = string.Join(" | ", setProperties);
                var result = $"{entityInfo.Name} created successfully! {summary}";
                _logger.LogInformation("[Tool:create_entity] {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Tool:create_entity] Error");
                return $"Error creating {entityName}: {ex.Message}";
            }
        }
    }
}

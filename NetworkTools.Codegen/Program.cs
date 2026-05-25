// ─────────────────────────────────────────────────────────────────────────────
// NetworkTools.Codegen
//
// Scans C# source files for enum declarations and parameter field declarations,
// then emits a TypeScript file containing:
//   - Mirrored enum definitions
//   - PARAM_KEYS     — dot-separated key constants grouped by tool
//   - PARAM_META     — type/default/range/modes metadata for each parameter
//   - ENUM_OPTIONS   — UI option lists derived from [EnumOption] attributes
//   - PARAM_BINDINGS — pre-built TwoWayBinding instances for bindable types
//   - PARAM_BINDING  — flat key->binding lookup map
//
// Parsing uses Roslyn syntax trees instead of regex, making the extraction
// resilient to formatting changes and self-documenting via typed AST nodes.
// ─────────────────────────────────────────────────────────────────────────────

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var enums = new Dictionary<string, EnumDef>();
var parameters = new List<ParamDef>();

if (args.Length < 2) {
    Console.Error.WriteLine("Usage: NetworkTools.Codegen <sourceDir> <outputFile> [--configuration Debug|Release]");
    return 1;
}

var sourceDir = Path.GetFullPath(args[0]);
var outputFile = Path.GetFullPath(args[1]);

var configIdx = Array.IndexOf(args, "--configuration");
var isDebug = configIdx >= 0 && configIdx + 1 < args.Length
    && args[configIdx + 1].Equals("Debug", StringComparison.OrdinalIgnoreCase);

if (!Directory.Exists(sourceDir)) {
    Console.Error.WriteLine($"Source directory not found: {sourceDir}");
    return 1;
}

// Parse every .cs file into a Roslyn syntax tree up front so we can walk them
// in two passes without re-reading from disk.
var csFiles = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);
var roots = new List<CompilationUnitSyntax>();
foreach (var file in csFiles) {
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
    roots.Add(tree.GetCompilationUnitRoot());
}

// Two-pass strategy: enums must be collected first because parameter parsing
// needs to resolve enum member references (e.g. "ConnectMode.Loop" -> 3)
// when extracting default values and mode bitmasks.
foreach (var root in roots) ParseEnums(root);
foreach (var root in roots) ParseParameters(root);

if (parameters.Count == 0)
    Console.Error.WriteLine("WARNING: No parameter declarations found. Emitting empty generated file.");

var ts = EmitTypeScript();

Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

// Only write when content actually changed to avoid unnecessary rebuilds downstream.
var existing = File.Exists(outputFile) ? File.ReadAllText(outputFile) : null;
if (ts != existing) {
    File.WriteAllText(outputFile, ts);
    Console.WriteLine($"Generated {outputFile}: {parameters.Count} params, {ReferencedEnumNames().Count} enums.");
} else {
    Console.WriteLine($"No changes to {outputFile}.");
}

return 0;

// ── Enum parsing ───────────────────────────────────────────────────────────────

/// <summary>
/// Walks all <c>enum</c> declarations in a syntax tree, extracting members
/// that have explicit integer values and any <c>[EnumOption]</c> attributes.
/// Members without an explicit <c>= value</c> are skipped (mirrors the
/// regex codegen's <c>(\w+)\s*=\s*(-?\d+)</c> capture group).
/// </summary>
void ParseEnums(CompilationUnitSyntax root) {
    foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>()) {
        var name = enumDecl.Identifier.Text;
        var members = new List<EnumMember>();

        foreach (var member in enumDecl.Members) {
            // Extract the integer value from the EqualsValue clause.
            // Handles both positive literals (e.g. `= 3`) and negated literals (e.g. `= -1`).
            // Members with computed expressions (shifts, casts) are skipped.
            var memberValue = member.EqualsValue?.Value switch {
                LiteralExpressionSyntax { Token.Value: int v } => (int?)v,
                PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax { Token.Value: int v } } neg
                    when neg.IsKind(SyntaxKind.UnaryMinusExpression) => -v,
                _ => null
            };
            if (memberValue is not int value) continue;

            // A single enum member can have multiple [EnumOption] attributes when it
            // appears in different groups (e.g. ShapeTransformTemplate.Preserve has both
            // a "Slope" and a "Curve" option).
            var options = member.AttributeLists
                .SelectMany(al => al.Attributes)
                .Where(a => a.Name.ToString() == "EnumOption")
                .Select(ParseEnumOptionAttr)
                .ToList();

            members.Add(new EnumMember(member.Identifier.Text, value, options));
        }

        if (members.Count > 0)
            enums[name] = new EnumDef(name, members);
    }
}

/// <summary>
/// Parses a single <c>[EnumOption("label", "icon", Group = ..., Visible = ..., Disabled = ...)]</c>
/// attribute into an <see cref="EnumOptionDef"/>.
/// </summary>
/// <remarks>
/// The attribute uses two syntax forms for its arguments:
/// <list type="bullet">
///   <item>Positional constructor args (label, icon) — accessed via index, no <c>NameColon</c> or <c>NameEquals</c>.</item>
///   <item>Named property setters (Group, Visible, Disabled) — identified by <c>arg.NameEquals</c>.</item>
/// </list>
/// This is distinct from constructor named args (which use <c>arg.NameColon</c>).
/// </remarks>
EnumOptionDef ParseEnumOptionAttr(AttributeSyntax attr) {
    var attrArgs = attr.ArgumentList?.Arguments;
    if (attrArgs == null) return new EnumOptionDef("", "");

    string label = "", icon = "";
    string? group = null;
    bool visible = true, disabled = false;
    int positional = 0;

    foreach (var arg in attrArgs) {
        // NameEquals is set for property-setter-style args: `Group = "Slope"`
        var propName = arg.NameEquals?.Name.Identifier.Text;
        if (propName != null) {
            switch (propName) {
                case "Group": group = GetStringLiteral(arg.Expression); break;
                case "Visible": visible = arg.Expression.ToString() == "true"; break;
                case "Disabled": disabled = arg.Expression.ToString() == "true"; break;
            }
        } else {
            // Positional args: first is the localization key, second is the icon URI
            if (positional == 0) label = GetStringLiteral(arg.Expression);
            else if (positional == 1) icon = GetStringLiteral(arg.Expression);
            positional++;
        }
    }

    return new EnumOptionDef(label, icon, group, visible, disabled);
}

/// <summary>
/// Extracts the raw string from a string literal expression node.
/// Uses <c>Token.Value</c> to get the unescaped value without surrounding quotes.
/// Falls back to <c>ToString().Trim('"')</c> for non-literal expressions.
/// </summary>
string GetStringLiteral(ExpressionSyntax expr) =>
    expr is LiteralExpressionSyntax { Token.Value: string s } ? s : expr.ToString().Trim('"');

// ── Parameter parsing ──────────────────────────────────────────────────────────

/// <summary>
/// Walks all <c>public</c> field declarations in a syntax tree, looking for
/// fields typed as one of the known parameter types (FloatParameter, IntParameter,
/// BoolParameter, etc.). For each match, extracts the constructor arguments from
/// the field initializer and delegates to the appropriate Parse*Param handler.
/// </summary>
void ParseParameters(CompilationUnitSyntax root) {
    foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>()) {
        if (!field.Modifiers.Any(SyntaxKind.PublicKeyword)) continue;

        var typeName = field.Declaration.Type.ToString();
        if (!IsParameterType(typeName)) continue;

        foreach (var variable in field.Declaration.Variables) {
            var fieldName = variable.Identifier.Text;

            // Handle both target-typed new (C# 9+):  `= new("key", ...)`
            // and explicit new:                       `= new FloatParameter("key", ...)`
            var argList = variable.Initializer?.Value switch {
                ImplicitObjectCreationExpressionSyntax impl => impl.ArgumentList,
                ObjectCreationExpressionSyntax obj => obj.ArgumentList,
                _ => null
            };
            if (argList == null) continue;

            var ctorArgs = ExtractArgs(argList.Arguments);

            if (typeName == "FloatParameter")
                ParseFloatParam(fieldName, ctorArgs);
            else if (typeName == "IntParameter")
                ParseIntParam(fieldName, ctorArgs);
            else if (typeName == "BoolParameter")
                ParseBoolParam(fieldName, ctorArgs);
            else if (typeName == "Float3Parameter")
                ParseFloat3Param(fieldName, ctorArgs);
            else if (typeName == "QuaternionParameter")
                ParseQuaternionParam(fieldName, ctorArgs);
            else if (typeName == "NetPrefabParameter")
                ParseNetPrefabParam(fieldName, ctorArgs);
            else if (typeName.StartsWith("EnumParameter<")) {
                // Extract the generic type argument: "EnumParameter<ConnectMode>" -> "ConnectMode"
                var enumType = typeName["EnumParameter<".Length..^1];
                var key = StripQuotes(ctorArgs.GetValueOrDefault("key") ?? ctorArgs.GetValueOrDefault("0") ?? "");
                var defaultStr = ctorArgs.GetValueOrDefault("default") ?? ctorArgs.GetValueOrDefault("1") ?? "0";
                var defaultValue = ResolveEnumValue(defaultStr, enumType);
                var modes = ResolveModes(ctorArgs.GetValueOrDefault("modes") ?? ctorArgs.GetValueOrDefault("2") ?? "0");
                parameters.Add(new EnumParamDef(key, fieldName, enumType, defaultValue, modes) { Label = ParseLabel(ctorArgs) });
            }
        }
    }
}

/// <summary>
/// Checks whether <paramref name="typeName"/> is one of the recognized parameter types.
/// </summary>
bool IsParameterType(string typeName) =>
    typeName is "FloatParameter" or "IntParameter" or "BoolParameter"
        or "Float3Parameter" or "QuaternionParameter" or "NetPrefabParameter"
    || typeName.StartsWith("EnumParameter<");

/// <summary>
/// Converts a Roslyn <see cref="ArgumentSyntax"/> list into a string dictionary,
/// keyed by named-argument name or positional index.
/// This bridges the Roslyn AST into the same format the Parse*Param handlers expect,
/// keeping the downstream resolution logic (modes, enum values, floats) unchanged.
/// </summary>
/// <remarks>
/// Named constructor args use colon syntax (<c>modes: 3</c>) and are identified
/// via <c>arg.NameColon</c>. Positional args are stored under their zero-based
/// index as a string key ("0", "1", ...).
/// <para/>
/// Note: Roslyn's <c>NameColon.Name.Identifier.Text</c> automatically strips the
/// <c>@</c> verbatim prefix from escaped keywords (e.g. <c>@default:</c> becomes
/// key <c>"default"</c>).
/// </remarks>
Dictionary<string, string> ExtractArgs(SeparatedSyntaxList<ArgumentSyntax> syntaxArgs) {
    var result = new Dictionary<string, string>();
    int positional = 0;
    foreach (var arg in syntaxArgs) {
        var name = arg.NameColon?.Name.Identifier.Text;
        var value = arg.Expression.ToString().Trim();
        if (name != null)
            result[name] = value;
        else
            result[(positional++).ToString()] = value;
    }
    return result;
}

// ── Parameter type handlers ────────────────────────────────────────────────────
//
// Each handler extracts constructor arguments by trying the named key first,
// then falling back to positional index. This matches the C# constructor
// signatures where early args (key, default, min, max) are typically passed
// positionally, while later optional args (modes, label, fractionDigits) use
// named syntax:
//
//   new("connect.loopRadius", 50f, 1f, 500f, modes: (int)ConnectMode.Loop, label: "...")
//        ^--- positional 0-3 ---^              ^--- named ---^

/// <summary>
/// Extracts a <see cref="FloatParamDef"/> from constructor args.
/// Positional order: key(0), default(1), min(2), max(3), modes(4).
/// </summary>
void ParseFloatParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var def = ParseFloatLiteral(args.GetValueOrDefault("default") ?? args.GetValueOrDefault("1") ?? "0");
    var min = ParseFloatLiteral(args.GetValueOrDefault("min") ?? args.GetValueOrDefault("2") ?? "0");
    var max = ParseFloatLiteral(args.GetValueOrDefault("max") ?? args.GetValueOrDefault("3") ?? "0");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("4") ?? "0");
    var label = ParseLabel(args);
    var fractionDigits = int.TryParse(args.GetValueOrDefault("fractionDigits"), out var fd) ? fd : 1;
    var numberType = ParseNumberType(args);
    var displayScale = ParseDisplayScale(args);
    parameters.Add(new FloatParamDef(key, fieldName, def, min, max, modes) { Label = label, FractionDigits = fractionDigits, NumberType = numberType, DisplayScale = displayScale });
}

/// <summary>
/// Extracts an <see cref="IntParamDef"/> from constructor args.
/// Positional order: key(0), default(1), min(2), max(3), modes(4).
/// </summary>
void ParseIntParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var def = int.Parse(args.GetValueOrDefault("default") ?? args.GetValueOrDefault("1") ?? "0");
    var min = int.Parse(args.GetValueOrDefault("min") ?? args.GetValueOrDefault("2") ?? "0");
    var max = int.Parse(args.GetValueOrDefault("max") ?? args.GetValueOrDefault("3") ?? "0");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("4") ?? "0");
    var numberType = ParseNumberType(args);
    var displayScale = ParseDisplayScale(args);
    parameters.Add(new IntParamDef(key, fieldName, def, min, max, modes) { Label = ParseLabel(args), NumberType = numberType, DisplayScale = displayScale });
}

/// <summary>
/// Extracts a <see cref="BoolParamDef"/> from constructor args.
/// Positional order: key(0), default(1), modes(2).
/// </summary>
void ParseBoolParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var def = bool.Parse(args.GetValueOrDefault("default") ?? args.GetValueOrDefault("1") ?? "false");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("2") ?? "0");
    parameters.Add(new BoolParamDef(key, fieldName, def, modes) { Label = ParseLabel(args) });
}

/// <summary>
/// Extracts a <see cref="Float3ParamDef"/> from constructor args.
/// Positional order: key(0), default(1), modes(2). Default is unused in codegen.
/// </summary>
void ParseFloat3Param(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("2") ?? "0");
    parameters.Add(new Float3ParamDef(key, fieldName, modes) { Label = ParseLabel(args) });
}

/// <summary>
/// Extracts a <see cref="QuaternionParamDef"/> from constructor args.
/// Positional order: key(0), default(1), modes(2). Default is unused in codegen.
/// </summary>
void ParseQuaternionParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("2") ?? "0");
    parameters.Add(new QuaternionParamDef(key, fieldName, modes) { Label = ParseLabel(args) });
}

/// <summary>
/// Extracts a <see cref="NetPrefabParamDef"/> from constructor args.
/// Positional order: key(0), modes(1). No default value.
/// </summary>
void ParseNetPrefabParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("1") ?? "0");
    var nullable = args.GetValueOrDefault("nullable") == "true";
    parameters.Add(new NetPrefabParamDef(key, fieldName, modes, nullable) { Label = ParseLabel(args) });
}

/// <summary>
/// Extracts the <c>label</c> named argument if present, stripping surrounding quotes.
/// </summary>
string? ParseLabel(Dictionary<string, string> args) {
    var raw = args.GetValueOrDefault("label");
    return raw != null ? StripQuotes(raw) : null;
}

/// <summary>
/// Extracts the <c>numberType</c> named argument if present, resolving the
/// <c>NumberType.XXX</c> enum member to a lowercase string for TypeScript output.
/// Returns <c>null</c> when absent or when the value is <c>NumberType.None</c>.
/// </summary>
string? ParseNumberType(Dictionary<string, string> args) {
    var raw = args.GetValueOrDefault("numberType");
    if (raw == null) return null;
    var member = raw.Contains('.') ? raw.Split('.').Last() : raw;
    return member == "None" ? null : CamelCase(member);
}

string CamelCase(string s) => s.Length == 0 ? s : char.ToLower(s[0]) + s[1..];

/// <summary>
/// Extracts the <c>displayScale</c> named argument if present, parsing the float literal.
/// Returns <c>null</c> when absent or when the value is <c>1</c> (the default).
/// </summary>
float? ParseDisplayScale(Dictionary<string, string> args) {
    var raw = args.GetValueOrDefault("displayScale");
    if (raw == null) return null;
    var val = ParseFloatLiteral(raw);
    return val == 1f ? null : val;
}

// ── Value resolution ───────────────────────────────────────────────────────────
//
// These helpers operate on the raw expression strings produced by ExtractArgs
// (e.g. "0.5f", "ConnectMode.Loop", "(int)GenerateMode.Grid | (int)GenerateMode.Oval").
// A small amount of regex is still used here because the expressions are already
// flattened to strings — walking Roslyn nodes for these would add complexity
// without improving clarity.

/// <summary>
/// Parses a C# float literal string, stripping the optional <c>f</c>/<c>F</c> suffix.
/// </summary>
float ParseFloatLiteral(string s) {
    s = s.Trim().TrimEnd('f', 'F');
    return float.Parse(s, CultureInfo.InvariantCulture);
}

/// <summary>
/// Strips surrounding whitespace and double-quote characters from a string.
/// </summary>
string StripQuotes(string s) => s.Trim().Trim('"');

/// <summary>
/// Resolves an enum member access expression (e.g. <c>"ConnectMode.SimpleCurve"</c>)
/// to its integer value by looking it up in the previously-collected <see cref="enums"/> dictionary.
/// Falls back to <see cref="int.TryParse"/> for raw integer literals, or <c>0</c> if unresolvable.
/// </summary>
int ResolveEnumValue(string expr, string enumType) {
    expr = expr.Trim();
    // Match "EnumType.MemberName" and look up the integer value
    var match = Regex.Match(expr, @"(\w+)\.(\w+)");
    if (match.Success && enums.TryGetValue(match.Groups[1].Value, out var def)) {
        var member = def.Members.FirstOrDefault(m => m.Name == match.Groups[2].Value);
        if (member != null) return member.Value;
    }
    if (int.TryParse(expr, out var intVal)) return intVal;
    return 0;
}

/// <summary>
/// Resolves a mode bitmask expression that may contain bitwise OR of cast enum values.
/// Handles expressions like <c>"(int)GenerateMode.Grid | (int)GenerateMode.Circle"</c>
/// by splitting on <c>|</c>, resolving each part, and OR-ing them together.
/// The result is a bitmask controlling which tool modes a parameter is visible in.
/// </summary>
int ResolveModes(string expr) {
    expr = expr.Trim();
    if (expr == "0") return 0;
    if (int.TryParse(expr, out var literal)) return literal;

    // Split on | and resolve each "(int)EnumType.Member" segment
    int result = 0;
    foreach (var part in expr.Split('|')) {
        var trimmed = part.Trim();
        var castMatch = Regex.Match(trimmed, @"\(int\)\s*(\w+)\.(\w+)");
        if (castMatch.Success && enums.TryGetValue(castMatch.Groups[1].Value, out var def)) {
            var member = def.Members.FirstOrDefault(m => m.Name == castMatch.Groups[2].Value);
            if (member != null) { result |= member.Value; continue; }
        }
        if (int.TryParse(trimmed, out var intVal))
            result |= intVal;
    }
    return result;
}

// ── TypeScript emission ────────────────────────────────────────────────────────

/// <summary>
/// Returns the sorted, distinct list of enum type names that are actually referenced
/// by <see cref="EnumParamDef"/> parameters — only these need to be emitted in the output.
/// </summary>
List<string> ReferencedEnumNames() =>
    parameters.OfType<EnumParamDef>()
        .Select(p => p.EnumType)
        .Distinct()
        .OrderBy(e => e)
        .ToList();

/// <summary>
/// Builds the complete TypeScript output string containing enum mirrors, parameter
/// metadata, enum option arrays, and TwoWayBinding declarations.
/// </summary>
string EmitTypeScript() {
    var sb = new StringBuilder();
    sb.AppendLine("// AUTO-GENERATED by NetworkTools.Codegen. Do not edit.");
    sb.AppendLine();
    sb.AppendLine("import { TwoWayBinding } from \"utils/bidirectionalBinding\";");
    sb.AppendLine();

    // ── Enum definitions ──
    // Mirror only the enums actually used by EnumParameter<T> fields.
    var referenced = ReferencedEnumNames();
    foreach (var enumName in referenced) {
        if (!enums.TryGetValue(enumName, out var def)) continue;
        sb.Append($"export enum {def.Name} {{ ");
        sb.Append(string.Join(", ", def.Members.Select(m => $"{m.Name} = {m.Value}")));
        sb.AppendLine(" }");
    }
    if (referenced.Count > 0) sb.AppendLine();

    // Parameters grouped by the tool prefix (first segment of the dot-separated key).
    // e.g. "connect.loopRadius" belongs to the "connect" group.
    var groups = parameters
        .GroupBy(p => p.Key.Split('.')[0])
        .OrderBy(g => g.Key)
        .ToList();

    // Float3, Quaternion, and NetPrefab types lack Colossal ValueWriter support,
    // so they are excluded from TwoWayBinding generation.
    var bindable = parameters.Where(p => p is not Float3ParamDef and not QuaternionParamDef and not NetPrefabParamDef).ToList();

    // ── PARAM_KEYS ──
    // Nested object of short key names -> full dot-separated key strings.
    sb.AppendLine("export const PARAM_KEYS = {");
    foreach (var group in groups) {
        sb.AppendLine($"    {group.Key}: {{");
        foreach (var param in group) {
            var shortKey = GetShortKey(param.Key, group.Key);
            sb.AppendLine($"        {shortKey}: \"{param.Key}\",");
        }
        sb.AppendLine("    },");
    }
    sb.AppendLine("} as const;");
    sb.AppendLine();

    // ── PARAM_META ──
    // Flat map of every parameter's full key to its type descriptor (type, default,
    // min/max, modes bitmask, optional label).
    sb.AppendLine("export const PARAM_META = {");
    foreach (var param in parameters) {
        sb.Append($"    \"{param.Key}\": {{ ");
        sb.Append(param switch {
            FloatParamDef f =>
                $"type: \"float\", default: {Fmt(f.Default)}, min: {Fmt(f.Min)}, max: {Fmt(f.Max)}, fractionDigits: {f.FractionDigits}, displayScale: {Fmt(f.DisplayScale ?? 1f)}, numberType: \"{f.NumberType ?? "none"}\", modes: {f.Modes}",
            IntParamDef i =>
                $"type: \"int\", default: {i.Default}, min: {i.Min}, max: {i.Max}, displayScale: {Fmt(i.DisplayScale ?? 1f)}, numberType: \"{i.NumberType ?? "none"}\", modes: {i.Modes}",
            BoolParamDef b =>
                $"type: \"bool\", default: {(b.Default ? "true" : "false")}, modes: {b.Modes}",
            EnumParamDef e =>
                $"type: \"enum\", enumType: \"{e.EnumType}\", default: {e.DefaultValue}, modes: {e.Modes}",
            Float3ParamDef f3 =>
                $"type: \"float3\", modes: {f3.Modes}",
            QuaternionParamDef q =>
                $"type: \"quaternion\", modes: {q.Modes}",
            NetPrefabParamDef np =>
                $"type: \"netPrefab\", modes: {np.Modes}{(np.Nullable ? ", nullable: true" : "")}",
            _ => ""
        });
        if (param.Label != null) sb.Append($", label: \"{param.Label}\"");
        sb.AppendLine(" },");
    }
    sb.AppendLine("} as const;");
    sb.AppendLine();

    // ── ENUM_OPTIONS ──
    // UI option arrays for enums that have [EnumOption] attributes.
    // Enums with groups (e.g. ShapeTransformTemplate with "Slope"/"Curve") are
    // keyed as "EnumName.GroupName"; ungrouped enums use just the enum name.
    var enumsWithOptions = referenced
        .Where(n => enums.TryGetValue(n, out var d) && d.HasOptions)
        .Select(n => enums[n])
        .ToList();

    if (enumsWithOptions.Count > 0) {
        sb.AppendLine("export interface EnumOption { readonly value: number; readonly label: string; readonly icon: string; readonly visible?: boolean; readonly disabled?: boolean }");
        sb.AppendLine();
        sb.AppendLine("export const ENUM_OPTIONS = {");
        foreach (var def in enumsWithOptions) {
            if (def.HasGroups) {
                // Grouped enums: each group gets its own array, keyed as "EnumName.GroupName".
                // A single member can appear in multiple groups via multiple [EnumOption] attributes.
                foreach (var group in def.Groups) {
                    sb.AppendLine($"    \"{def.Name}.{group}\": [");
                    foreach (var member in def.Members) {
                        foreach (var opt in member.Options.Where(o => o.Group == group)) {
                            sb.Append($"        {{ value: {def.Name}.{member.Name}, label: \"{opt.Label}\", icon: \"{opt.Icon}\"");
                            if (!opt.Visible) sb.Append(", visible: false");
                            if (opt.Disabled && !isDebug) sb.Append(", disabled: true");
                            sb.AppendLine(" },");
                        }
                    }
                    sb.AppendLine("    ] as readonly EnumOption[],");
                }
            } else {
                // Ungrouped enums: single array keyed by enum name.
                sb.AppendLine($"    {def.Name}: [");
                foreach (var member in def.Members) {
                    foreach (var opt in member.Options) {
                        sb.Append($"        {{ value: {def.Name}.{member.Name}, label: \"{opt.Label}\", icon: \"{opt.Icon}\"");
                        if (!opt.Visible) sb.Append(", visible: false");
                        if (opt.Disabled && !isDebug) sb.Append(", disabled: true");
                        sb.AppendLine(" },");
                    }
                }
                sb.AppendLine("    ] as readonly EnumOption[],");
            }
        }
        sb.AppendLine("};");
        sb.AppendLine();
    }

    // ── PARAM_BINDINGS ──
    // Grouped TwoWayBinding instances for bindable parameter types.
    // These are used by the UI to establish bidirectional communication with the mod.
    var bindableGroups = bindable
        .GroupBy(p => p.Key.Split('.')[0])
        .OrderBy(g => g.Key)
        .ToList();

    sb.AppendLine("export const PARAM_BINDINGS = {");
    foreach (var group in bindableGroups) {
        sb.AppendLine($"    {group.Key}: {{");
        foreach (var param in group) {
            var shortKey = GetShortKey(param.Key, group.Key);
            var (tsType, defaultLiteral) = param switch {
                FloatParamDef f => ("number", Fmt(f.Default)),
                IntParamDef i => ("number", i.Default.ToString()),
                BoolParamDef b => ("boolean", b.Default ? "true" : "false"),
                EnumParamDef e => ("number", e.DefaultValue.ToString()),
                _ => ("unknown", "undefined")
            };
            sb.AppendLine($"        {shortKey}: new TwoWayBinding<{tsType}>(\"{param.Key}\", {defaultLiteral}),");
        }
        sb.AppendLine("    },");
    }
    sb.AppendLine("};");
    sb.AppendLine();

    // ── PARAM_BINDING ──
    // Flat lookup from full key string to its TwoWayBinding instance.
    // Provides O(1) access when the key is known at runtime but the group is not.
    sb.AppendLine("export const PARAM_BINDING: Record<string, TwoWayBinding<any>> = {");
    foreach (var group in bindableGroups) {
        foreach (var param in group) {
            var shortKey = GetShortKey(param.Key, group.Key);
            sb.AppendLine($"    \"{param.Key}\": PARAM_BINDINGS.{group.Key}.{shortKey},");
        }
    }
    sb.AppendLine("};");

    return sb.ToString();
}

/// <summary>
/// Converts a full dot-separated parameter key to a short camelCase property name
/// within its tool group.
/// </summary>
/// <example>
/// <c>GetShortKey("connect.loopRadius", "connect")</c> returns <c>"loopRadius"</c>.
/// <c>GetShortKey("generate.grid.xSpacing", "generate")</c> returns <c>"gridXSpacing"</c>.
/// </example>
string GetShortKey(string fullKey, string toolPrefix) {
    // Strip the tool prefix and its trailing dot
    var suffix = fullKey[(toolPrefix.Length + 1)..];
    if (!suffix.Contains('.')) return suffix;
    // Remaining dots become camelCase boundaries: "grid.xSpacing" -> "gridXSpacing"
    var parts = suffix.Split('.');
    return parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpper(p[0]) + p[1..]));
}

/// <summary>
/// Formats a float for TypeScript output. Whole numbers are emitted without a decimal
/// point (e.g. <c>1</c> not <c>1.0</c>), while fractional values use the "G" format
/// with invariant culture to avoid locale-dependent separators.
/// </summary>
string Fmt(float v) => v == (int)v
    ? ((int)v).ToString()
    : v.ToString("G", CultureInfo.InvariantCulture);

// ── Data models ────────────────────────────────────────────────────────────────

/// <summary>
/// A single <c>[EnumOption]</c> attribute's parsed content.
/// Represents one UI option entry for an enum value.
/// </summary>
/// <param name="Label">Localization key for the option's display text.</param>
/// <param name="Icon">URI to the option's icon (e.g. <c>"coui://nt/Modes/ConnectLoop.svg"</c>).</param>
/// <param name="Group">Optional group name for enums that split options across categories (e.g. "Slope", "Curve").</param>
/// <param name="Visible">Whether the option appears in the UI. Hidden options are still valid values.</param>
/// <param name="Disabled">Whether the option is shown but greyed out / unselectable.</param>
record EnumOptionDef(string Label, string Icon, string? Group = null, bool Visible = true, bool Disabled = false);

/// <summary>
/// A single member of an enum declaration (e.g. <c>Grid = 1</c>) with its
/// associated <see cref="EnumOptionDef"/> attributes.
/// </summary>
record EnumMember(string Name, int Value, List<EnumOptionDef> Options);

/// <summary>
/// A complete enum declaration with all its members.
/// Provides helpers to determine whether the enum has UI options and/or groups.
/// </summary>
record EnumDef(string Name, List<EnumMember> Members) {
    /// <summary>Whether any member has at least one <c>[EnumOption]</c> attribute.</summary>
    public bool HasOptions => Members.Any(m => m.Options.Count > 0);
    /// <summary>Whether any option specifies a <c>Group</c>, requiring split option arrays in the output.</summary>
    public bool HasGroups => Members.Any(m => m.Options.Any(o => o.Group != null));
    /// <summary>The distinct, sorted group names across all members' options.</summary>
    public IEnumerable<string> Groups => Members
        .SelectMany(m => m.Options.Where(o => o.Group != null).Select(o => o.Group!))
        .Distinct()
        .OrderBy(g => g);
}

/// <summary>
/// Base record for all parameter definitions. The <see cref="Key"/> is the full
/// dot-separated identifier used for C#/TypeScript binding (e.g. <c>"connect.loopRadius"</c>).
/// <see cref="Modes"/> is a bitmask of tool modes in which this parameter is active.
/// </summary>
abstract record ParamDef(string Key, string FieldName, int Modes) {
    /// <summary>Optional localization key for the parameter's UI label.</summary>
    public string? Label { get; init; }
}

/// <param name="Default">Initial value.</param>
/// <param name="Min">Minimum slider/input value.</param>
/// <param name="Max">Maximum slider/input value.</param>
record FloatParamDef(string Key, string FieldName, float Default, float Min, float Max, int Modes)
    : ParamDef(Key, FieldName, Modes) {
    /// <summary>Number of decimal places shown in the UI (default 1).</summary>
    public int FractionDigits { get; init; } = 1;
    /// <summary>Optional semantic number type (e.g. "distance", "percentage") for UI formatting hints.</summary>
    public string? NumberType { get; init; }
    /// <summary>Optional display multiplier for the UI (e.g. 100 to show 0–1 as 0–100). Null when 1 (default).</summary>
    public float? DisplayScale { get; init; }
}
record IntParamDef(string Key, string FieldName, int Default, int Min, int Max, int Modes)
    : ParamDef(Key, FieldName, Modes) {
    /// <summary>Optional semantic number type (e.g. "rows", "columns") for UI formatting hints.</summary>
    public string? NumberType { get; init; }
    /// <summary>Optional display multiplier for the UI. Null when 1 (default).</summary>
    public float? DisplayScale { get; init; }
}
record BoolParamDef(string Key, string FieldName, bool Default, int Modes)
    : ParamDef(Key, FieldName, Modes);
/// <param name="EnumType">The C# enum type name (e.g. <c>"ConnectMode"</c>).</param>
/// <param name="DefaultValue">The resolved integer value of the default enum member.</param>
record EnumParamDef(string Key, string FieldName, string EnumType, int DefaultValue, int Modes)
    : ParamDef(Key, FieldName, Modes);
record Float3ParamDef(string Key, string FieldName, int Modes)
    : ParamDef(Key, FieldName, Modes);
record QuaternionParamDef(string Key, string FieldName, int Modes)
    : ParamDef(Key, FieldName, Modes);
record NetPrefabParamDef(string Key, string FieldName, int Modes, bool Nullable = false)
    : ParamDef(Key, FieldName, Modes);

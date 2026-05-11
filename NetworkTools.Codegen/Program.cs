using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

var enums = new Dictionary<string, EnumDef>();
var parameters = new List<ParamDef>();

if (args.Length < 2) {
    Console.Error.WriteLine("Usage: NetworkTools.Codegen <sourceDir> <outputFile>");
    return 1;
}

var sourceDir = Path.GetFullPath(args[0]);
var outputFile = Path.GetFullPath(args[1]);

if (!Directory.Exists(sourceDir)) {
    Console.Error.WriteLine($"Source directory not found: {sourceDir}");
    return 1;
}

var csFiles = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);

foreach (var file in csFiles) {
    var content = File.ReadAllText(file);
    ParseEnums(content);
}

foreach (var file in csFiles) {
    var content = File.ReadAllText(file);
    ParseParameters(content);
}

if (parameters.Count == 0)
    Console.Error.WriteLine("WARNING: No parameter declarations found. Emitting empty generated file.");

var ts = EmitTypeScript();

Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

var existing = File.Exists(outputFile) ? File.ReadAllText(outputFile) : null;
if (ts != existing) {
    File.WriteAllText(outputFile, ts);
    Console.WriteLine($"Generated {outputFile}: {parameters.Count} params, {ReferencedEnumNames().Count} enums.");
} else {
    Console.WriteLine($"No changes to {outputFile}.");
}

return 0;

// ── Enum parsing ───────────────────────────────────────────────────────────────

void ParseEnums(string source) {
    var regex = new Regex(@"public\s+enum\s+(\w+)\s*\{([^}]+)\}", RegexOptions.Singleline);
    foreach (Match m in regex.Matches(source)) {
        var name = m.Groups[1].Value;
        var body = m.Groups[2].Value;

        var members = new List<EnumMember>();
        var memberRegex = new Regex(@"(\w+)\s*=\s*(-?\d+)");
        foreach (Match mm in memberRegex.Matches(body))
            members.Add(new EnumMember(mm.Groups[1].Value, int.Parse(mm.Groups[2].Value)));

        if (members.Count > 0)
            enums[name] = new EnumDef(name, members);
    }
}

// ── Parameter parsing ──────────────────────────────────────────────────────────

void ParseParameters(string source) {
    source = Regex.Replace(source, @"//.*?$", "", RegexOptions.Multiline);
    source = Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline);
    source = Regex.Replace(source, @"\s+", " ");

    var regex = new Regex(
        @"public\s+(FloatParameter|IntParameter|BoolParameter|Float3Parameter|QuaternionParameter|NetPrefabParameter|EnumParameter<(\w+)>)\s+(\w+)\s*=\s*new\s*\(");

    var matches = regex.Matches(source).Cast<Match>().OrderBy(m => m.Index);
    foreach (var m in matches) {
        var typeName = m.Groups[1].Value;
        var fieldName = m.Groups[3].Value;
        var argsStr = ExtractBalancedParens(source, m.Index + m.Length);
        var ctorArgs = ParseConstructorArgs(argsStr);

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
            var enumType = m.Groups[2].Value;
            var key = StripQuotes(ctorArgs.GetValueOrDefault("key") ?? ctorArgs.GetValueOrDefault("0") ?? "");
            var defaultStr = ctorArgs.GetValueOrDefault("default") ?? ctorArgs.GetValueOrDefault("1") ?? "0";
            var defaultValue = ResolveEnumValue(defaultStr, enumType);
            var modes = ResolveModes(ctorArgs.GetValueOrDefault("modes") ?? ctorArgs.GetValueOrDefault("2") ?? "0");
            parameters.Add(new EnumParamDef(key, fieldName, enumType, defaultValue, modes));
        }
    }
}

void ParseFloatParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var def = ParseFloatLiteral(args.GetValueOrDefault("default") ?? args.GetValueOrDefault("1") ?? "0");
    var min = ParseFloatLiteral(args.GetValueOrDefault("min") ?? args.GetValueOrDefault("2") ?? "0");
    var max = ParseFloatLiteral(args.GetValueOrDefault("max") ?? args.GetValueOrDefault("3") ?? "0");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("4") ?? "0");
    parameters.Add(new FloatParamDef(key, fieldName, def, min, max, modes));
}

void ParseIntParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var def = int.Parse(args.GetValueOrDefault("default") ?? args.GetValueOrDefault("1") ?? "0");
    var min = int.Parse(args.GetValueOrDefault("min") ?? args.GetValueOrDefault("2") ?? "0");
    var max = int.Parse(args.GetValueOrDefault("max") ?? args.GetValueOrDefault("3") ?? "0");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("4") ?? "0");
    parameters.Add(new IntParamDef(key, fieldName, def, min, max, modes));
}

void ParseBoolParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var def = bool.Parse(args.GetValueOrDefault("default") ?? args.GetValueOrDefault("1") ?? "false");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("2") ?? "0");
    parameters.Add(new BoolParamDef(key, fieldName, def, modes));
}

void ParseFloat3Param(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("2") ?? "0");
    parameters.Add(new Float3ParamDef(key, fieldName, modes));
}

void ParseQuaternionParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("2") ?? "0");
    parameters.Add(new QuaternionParamDef(key, fieldName, modes));
}

void ParseNetPrefabParam(string fieldName, Dictionary<string, string> args) {
    var key = StripQuotes(args.GetValueOrDefault("key") ?? args.GetValueOrDefault("0") ?? "");
    var modes = ResolveModes(args.GetValueOrDefault("modes") ?? args.GetValueOrDefault("1") ?? "0");
    parameters.Add(new NetPrefabParamDef(key, fieldName, modes));
}

// ── Constructor argument parsing ───────────────────────────────────────────────

string ExtractBalancedParens(string source, int startAfterOpen) {
    int depth = 1;
    int i = startAfterOpen;
    while (i < source.Length && depth > 0) {
        if (source[i] == '(') depth++;
        else if (source[i] == ')') depth--;
        if (depth > 0) i++;
    }
    return source[startAfterOpen..i];
}

Dictionary<string, string> ParseConstructorArgs(string argsStr) {
    var result = new Dictionary<string, string>();
    var parts = SplitArgs(argsStr);
    for (int i = 0; i < parts.Count; i++) {
        var part = parts[i].Trim();
        if (string.IsNullOrEmpty(part)) continue;

        var colonIdx = FindNamedArgColon(part);
        if (colonIdx > 0) {
            var name = part[..colonIdx].Trim().TrimStart('@');
            var value = part[(colonIdx + 1)..].Trim();
            result[name] = value;
        } else {
            result[i.ToString()] = part;
        }
    }
    return result;
}

int FindNamedArgColon(string part) {
    if (part.StartsWith("\"")) return -1;
    for (int i = 0; i < part.Length; i++) {
        if (part[i] == ':') return i;
        if (part[i] == '"' || part[i] == '(') return -1;
    }
    return -1;
}

List<string> SplitArgs(string argsStr) {
    var result = new List<string>();
    int depth = 0;
    int start = 0;
    bool inString = false;
    for (int i = 0; i < argsStr.Length; i++) {
        var c = argsStr[i];
        if (c == '"') inString = !inString;
        if (inString) continue;
        switch (c) {
            case '(': depth++; break;
            case ')': depth--; break;
            case ',' when depth == 0:
                result.Add(argsStr[start..i]);
                start = i + 1;
                break;
        }
    }
    result.Add(argsStr[start..]);
    return result;
}

// ── Value resolution ───────────────────────────────────────────────────────────

float ParseFloatLiteral(string s) {
    s = s.Trim().TrimEnd('f', 'F');
    return float.Parse(s, CultureInfo.InvariantCulture);
}

string StripQuotes(string s) => s.Trim().Trim('"');

int ResolveEnumValue(string expr, string enumType) {
    expr = expr.Trim();
    var match = Regex.Match(expr, @"(\w+)\.(\w+)");
    if (match.Success && enums.TryGetValue(match.Groups[1].Value, out var def)) {
        var member = def.Members.FirstOrDefault(m => m.Name == match.Groups[2].Value);
        if (member != null) return member.Value;
    }
    if (int.TryParse(expr, out var intVal)) return intVal;
    return 0;
}

int ResolveModes(string expr) {
    expr = expr.Trim();
    if (expr == "0") return 0;
    if (int.TryParse(expr, out var literal)) return literal;

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

List<string> ReferencedEnumNames() =>
    parameters.OfType<EnumParamDef>()
        .Select(p => p.EnumType)
        .Distinct()
        .OrderBy(e => e)
        .ToList();

string EmitTypeScript() {
    var sb = new StringBuilder();
    sb.AppendLine("// AUTO-GENERATED by NetworkTools.Codegen. Do not edit.");
    sb.AppendLine();
    sb.AppendLine("import { TwoWayBinding } from \"utils/bidirectionalBinding\";");
    sb.AppendLine();

    var referenced = ReferencedEnumNames();
    foreach (var enumName in referenced) {
        if (!enums.TryGetValue(enumName, out var def)) continue;
        sb.Append($"export enum {def.Name} {{ ");
        sb.Append(string.Join(", ", def.Members.Select(m => $"{m.Name} = {m.Value}")));
        sb.AppendLine(" }");
    }
    if (referenced.Count > 0) sb.AppendLine();

    var groups = parameters
        .GroupBy(p => p.Key.Split('.')[0])
        .OrderBy(g => g.Key)
        .ToList();

    // Bindable parameter types (skip types without Colossal ValueWriter support)
    var bindable = parameters.Where(p => p is not Float3ParamDef and not QuaternionParamDef and not NetPrefabParamDef).ToList();

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

    sb.AppendLine("export const PARAM_META = {");
    foreach (var param in parameters) {
        sb.Append($"    \"{param.Key}\": {{ ");
        sb.Append(param switch {
            FloatParamDef f =>
                $"type: \"float\", default: {Fmt(f.Default)}, min: {Fmt(f.Min)}, max: {Fmt(f.Max)}, modes: {f.Modes}",
            IntParamDef i =>
                $"type: \"int\", default: {i.Default}, min: {i.Min}, max: {i.Max}, modes: {i.Modes}",
            BoolParamDef b =>
                $"type: \"bool\", default: {(b.Default ? "true" : "false")}, modes: {b.Modes}",
            EnumParamDef e =>
                $"type: \"enum\", enumType: \"{e.EnumType}\", default: {e.DefaultValue}, modes: {e.Modes}",
            Float3ParamDef f3 =>
                $"type: \"float3\", modes: {f3.Modes}",
            QuaternionParamDef q =>
                $"type: \"quaternion\", modes: {q.Modes}",
            NetPrefabParamDef np =>
                $"type: \"netPrefab\", modes: {np.Modes}",
            _ => ""
        });
        sb.AppendLine(" },");
    }
    sb.AppendLine("} as const;");
    sb.AppendLine();

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

    return sb.ToString();
}

string GetShortKey(string fullKey, string toolPrefix) {
    var suffix = fullKey[(toolPrefix.Length + 1)..];
    if (!suffix.Contains('.')) return suffix;
    var parts = suffix.Split('.');
    return parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpper(p[0]) + p[1..]));
}

string Fmt(float v) => v == (int)v
    ? ((int)v).ToString()
    : v.ToString("G", CultureInfo.InvariantCulture);

// ── Data models ────────────────────────────────────────────────────────────────

record EnumMember(string Name, int Value);
record EnumDef(string Name, List<EnumMember> Members);

abstract record ParamDef(string Key, string FieldName, int Modes);
record FloatParamDef(string Key, string FieldName, float Default, float Min, float Max, int Modes)
    : ParamDef(Key, FieldName, Modes);
record IntParamDef(string Key, string FieldName, int Default, int Min, int Max, int Modes)
    : ParamDef(Key, FieldName, Modes);
record BoolParamDef(string Key, string FieldName, bool Default, int Modes)
    : ParamDef(Key, FieldName, Modes);
record EnumParamDef(string Key, string FieldName, string EnumType, int DefaultValue, int Modes)
    : ParamDef(Key, FieldName, Modes);
record Float3ParamDef(string Key, string FieldName, int Modes)
    : ParamDef(Key, FieldName, Modes);
record QuaternionParamDef(string Key, string FieldName, int Modes)
    : ParamDef(Key, FieldName, Modes);
record NetPrefabParamDef(string Key, string FieldName, int Modes)
    : ParamDef(Key, FieldName, Modes);

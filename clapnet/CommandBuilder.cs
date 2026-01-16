using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Command = System.CommandLine.Command;

namespace clapnet
{
    enum CaseType
    {
        CamelCase,
        SnakeCase,
        KebabCase,
    }
    /// <summary>
    /// Class that makes it easy to create CLI program by just passing methods to a builder
    /// </summary>
    /// <example>
    ///  <code>return CommandBuilder.New()
    /// .With(() => {Console.WriteLine("Test");})
    /// .Run(args);</code>
    /// </example>
    public class CommandBuilder
    {
        private RootCommand _rootCommand = new RootCommand();
        private CaseType _caseType = CaseType.SnakeCase;

        private readonly Dictionary<Type, Func<ParseResult, object>> _settingsBuilders =
            new Dictionary<Type, Func<ParseResult, object>>();

        /// <summary>
        /// Creates a new builder instance
        /// </summary>
        /// <returns>Builder instance</returns>
        public static CommandBuilder New()
        {
            var builder = new CommandBuilder()
            {
                _rootCommand = new RootCommand()
            };
            return builder;
        }

        /// <summary>
        /// Use snake case for options.
        /// </summary>
        public CommandBuilder UseSnakeCase()
        {
            _caseType = CaseType.SnakeCase;
            return this;
        }

        /// <summary>
        /// Use lower camel case for options.
        /// </summary>
        public CommandBuilder UseCamelCase()
        {
            _caseType = CaseType.CamelCase;
            return this;
        }

        /// <summary>
        /// Use kebab case for options.
        /// </summary>
        public CommandBuilder UseKebabCase()
        {
            _caseType = CaseType.KebabCase;
            return this;
        }

        private string ParseField(string fieldName)
        {
            var result = fieldName;
            switch (_caseType)
            {
                case CaseType.CamelCase:
                    result = ToLowerCamelCase(fieldName);
                    break;
                case CaseType.SnakeCase:
                    result = ToSnakeCase(fieldName);
                    break;
                case CaseType.KebabCase:
                    result = ToKebabCase(fieldName);
                    break;
            }
            return result;
        }

        private Func<ParseResult, object>? BuildArgumentForType(FieldInfo field, string argumentName, ref Command command)
        {
            var fieldName = $"--{argumentName}";
            string description = DocumentationReader.GetDescription(field) ?? "";

            if (field.FieldType == typeof(string))
            {
                var option = new Option<string>(fieldName) { Description = description };

                command.Add(option);
                return result => result.GetValue(option) ?? string.Empty;
            }
            else if (field.FieldType == typeof(int))
            {
                var option = new Option<int>(fieldName) { Description = description };

                command.Add(option);
                return result => result.GetValue(option);
            }
            else if (field.FieldType == typeof(bool))
            {
                var option = new Option<bool>(fieldName) { Description = description };

                command.Add(option);
                return result => result.GetValue(option);
            }
            else if (field.FieldType == typeof(float))
            {
                var option = new Option<float>(fieldName) { Description = description };

                command.Add(option);
                return result => result.GetValue(option);
            }

            return null;
        }

        private bool TryIntoOption(ParameterInfo parameterInfo, out Func<ParseResult, object>? func, ref Command command)
        {
            func = null;
            var type = parameterInfo.ParameterType;
            if (type.IsClass)
            {
                BuildOptionsForType(type, ref command);
            }

            if (_settingsBuilders.TryGetValue(type, out var f))
            {
                func = f;
                return true;
            }

            return false;
        }

        private bool TryIntoArgument(ParameterInfo parameterInfo, out Func<ParseResult, object>? func,
            out Argument? argument)
        {
            func = null;
            argument = null;
            string description = DocumentationReader.GetParameterDescription(parameterInfo) ?? "";

            if (parameterInfo.ParameterType == typeof(bool))
            {
                var def = false;
                if (parameterInfo.DefaultValue != null)
                {
                    def = (bool)parameterInfo.DefaultValue;
                }

                var boolArgument = new Argument<bool>(parameterInfo.Name) { Description = description };
                if (parameterInfo.HasDefaultValue)
                {
                    boolArgument.DefaultValueFactory = _ => def;
                }

                argument = boolArgument;
                func = parse => parse.GetValue(boolArgument);
            }
            else if (parameterInfo.ParameterType == typeof(string))
            {
                var option = new Argument<string>(parameterInfo.Name) { Description = description };
                if (parameterInfo.HasDefaultValue)
                {
                    option.DefaultValueFactory = _ =>
                    {
                        if (parameterInfo.DefaultValue == null)
                        {
                            return string.Empty;
                        }

                        return (string)parameterInfo.DefaultValue;
                    };
                }

                argument = option;
                func = parse => parse.GetValue(option) ?? string.Empty;
            }
            else if (parameterInfo.ParameterType == typeof(int))
            {
                var option = new Argument<int>(parameterInfo.Name) { Description = description };
                if (parameterInfo.HasDefaultValue)
                {
                    option.DefaultValueFactory = _ => (int)parameterInfo.DefaultValue;
                }

                argument = option;
                func = parse => parse.GetValue(option);
            }
            else if (parameterInfo.ParameterType == typeof(float))
            {
                var option = new Argument<float>(parameterInfo.Name) { Description = description };
                if (parameterInfo.HasDefaultValue)
                {
                    option.DefaultValueFactory = _ => (float)parameterInfo.DefaultValue;
                }

                argument = option;
                func = parse => parse.GetValue(option);
            }
            else if (parameterInfo.ParameterType == typeof(double))
            {
                var option = new Argument<double>(parameterInfo.Name) { Description = description };
                if (parameterInfo.HasDefaultValue)
                {
                    option.DefaultValueFactory = _ => (double)parameterInfo.DefaultValue;
                }

                argument = option;
                func = parse => parse.GetValue(option);
            }
            else
            {
                return false;
            }

            return true;
        }

        private void BuildOptionsForType(Type type, ref Command command)
        {
            if (!type.IsClass)
            {
                return;
            }

            var arguments = new Dictionary<string, Func<ParseResult, object>>();
            foreach (var field in
                     type.GetFields())
            {
                var fieldName = ParseField(field.Name);
                var optionFn = BuildArgumentForType(field, fieldName, ref command);
                if (optionFn != null)
                {
                    arguments.Add(fieldName, optionFn);
                }
            }
            // if it already exists, don't do anything.
            if (_settingsBuilders.ContainsKey(type))
            {
                return;
            }

            _settingsBuilders.Add(type, result =>
            {
                var instance = Activator.CreateInstance(type);
                FieldInfo[] ps = instance.GetType().GetFields();
                foreach (var field in ps)
                {
                    var o = field.GetValue(instance);
                    var p = instance.GetType().GetField(field.Name);
                    if (o != null)
                    {
                        var name = ParseField(field.Name);
                        if (arguments.TryGetValue(name, out Func<ParseResult, object> f))
                        {
                            var newValue = f(result);
                            if (newValue != null)
                            {
                                p.SetValue(instance, newValue);
                            }
                        }
                    }
                }

                return instance;
            });
        }

        private Command BuildCommand(Delegate func, string nameOverride = "")
        {
            var method = func.Method;
            var name = method.Name;
            if (!string.IsNullOrWhiteSpace(nameOverride))
            {
                name = nameOverride;
            }

            if (name.Contains("|"))
            {
                name = name.Split('|')[0];
            }

            if (name.Contains("__"))
            {
                var index = name.LastIndexOf("__", StringComparison.Ordinal);
                name = name.Substring(index + 2);
            }

            name = name.ToLowerInvariant();
            Command cmd = new Command(name);

            string? desc = DocumentationReader.GetDescription(method);
            if (!string.IsNullOrEmpty(desc))
            {
                cmd.Description = desc;
            }

            var parameters = method.GetParameters();
            var isInt = method.ReturnType == typeof(int);
            var arguments = new List<Func<ParseResult, object>>();
            foreach (var parameterInfo in parameters)
            {
                if (TryIntoArgument(parameterInfo, out var f2, out var argument))
                {
                    cmd.Arguments.Add(argument!);
                    arguments.Add(f2!);
                }
                else if (TryIntoOption(parameterInfo, out var f, ref cmd))
                {
                    arguments.Add(f!);
                }
                else
                {
                    Console.Error.WriteLine($"Unknown parameter: {parameterInfo.Name}");
                }
            }

            cmd.SetAction(result =>
            {
                var parametersValues = new List<object>();
                foreach (var parameterInfo in arguments)
                {
                    var rr = parameterInfo(result);
                    if (rr != null)
                    {
                        parametersValues.Add(rr);
                    }
                    else
                    {
                        Console.WriteLine("Result is null");
                    }
                }

                var methodResult = func.DynamicInvoke(parametersValues.ToArray());

                if (isInt && methodResult is int i)
                {
                    return i;
                }

                return 0;
            });
            return cmd;
        }

        /// <summary>
        /// Adds subcommand to the CLI program
        /// </summary>
        /// <param name="func">Function to be called</param>
        /// <param name="description">Optional description for the command</param>
        /// <param name="name">Specify command name</param>
        /// <returns>Builder</returns>
        public CommandBuilder With(Delegate func, string description = "", string name = "")
        {
            var cmd = BuildCommand(func, name);
            if (!string.IsNullOrWhiteSpace(description))
            {
                cmd.Description = description;
            }
            else
            {
                string? desc = DocumentationReader.GetDescription(func.Method);
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    cmd.Description = desc;
                }
            }

            _rootCommand.Add(cmd);
            return this;
        }

        /// <summary>
        /// Set up the root command for the CLI program
        /// </summary>
        /// <param name="func">Function to be called</param>
        /// <param name="description">Optional description for the command</param>
        /// <returns></returns>
        public CommandBuilder WithRootCommand(Delegate func, string description = "")
        {
            var method = func.Method;
            if (!string.IsNullOrEmpty(description))
            {
                _rootCommand.Description = description;
            }
            else
            {
                string? desc = DocumentationReader.GetDescription(method);
                if (!string.IsNullOrEmpty(desc))
                {
                    _rootCommand.Description = desc;
                }
            }

            var parameters = method.GetParameters();
            var isInt = method.ReturnType == typeof(int);
            var arguments = new List<Func<ParseResult, object>>();
            foreach (var parameterInfo in parameters)
            {
                if (TryIntoArgument(parameterInfo, out var f2, out var argument))
                {
                    _rootCommand.Arguments.Add(argument!);
                    arguments.Add(f2!);
                }
                else
                {
                    var rootCommand = _rootCommand as Command;
                    if (TryIntoOption(parameterInfo,
                            out var f,
                            ref rootCommand))
                    {
                        arguments.Add(f!);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Unknown parameter: {parameterInfo.Name}");
                    }
                }
            }

            _rootCommand.SetAction(result =>
            {
                var parametersValues = new List<object>();
                foreach (var parameterInfo in arguments)
                {
                    var rr = parameterInfo(result);
                    if (rr != null)
                    {
                        parametersValues.Add(rr);
                    }
                    else
                    {
                        Console.WriteLine("Result is null");
                    }
                }

                var methodResult = func.DynamicInvoke(parametersValues.ToArray());

                if (isInt && methodResult is int i)
                {
                    return i;
                }

                return 0;
            });
            return this;
        }

        /// <summary>
        /// Runs the CLI
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public int Run(string[] args)
        {
            ParseResult parseResult = _rootCommand.Parse(args);
            return parseResult.Invoke();
        }

        static string ToSnakeCase(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1. Handle the abbreviation case (e.g. IOStream -> IO_Stream)
            // 2. Handle the standard case (e.g. MyVariable -> My_Variable)
            var result = Regex.Replace(text, "([a-z0-9])([A-Z])", "$1_$2");

            return result.ToLower();
        }

        static string ToKebabCase(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1. Handle the abbreviation case (e.g. IOStream -> IO-Stream)
            // 2. Handle the standard case (e.g. MyVariable -> My-Variable)
            var result = Regex.Replace(text, "([a-z0-9])([A-Z])", "$1-$2");

            return result.ToLower();
        }

        static string ToLowerCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var output = Regex.Replace(text, "_([a-z])", m => m.Groups[1].Value.ToUpper());
            if (output.Length > 0)
            {
                return char.ToLowerInvariant(output[0]) + output.Substring(1);
            }

            return output;
        }
    }

    internal static class DocumentationReader
    {
        private static readonly Dictionary<Assembly, XDocument?> _cache = new Dictionary<Assembly, XDocument?>();
        private static readonly Dictionary<string, string[]> _sourceFileCache = new Dictionary<string, string[]>();

        public static string? GetDescription(MemberInfo member)
        {
            var doc = GetXmlDoc(member);
            if (doc != null)
            {
                string memberId = GetMemberId(member);
                if (!string.IsNullOrEmpty(memberId))
                {
                    var element = doc.Descendants("member")
                        .FirstOrDefault(e => e.Attribute("name")?.Value == memberId);
                    if (element != null)
                    {
                        return element.Element("summary")?.Value.Trim();
                    }
                }
            }

            return GetSourceDescription(member);
        }

        public static string? GetParameterDescription(ParameterInfo parameter)
        {
            var member = parameter.Member;
            var doc = GetXmlDoc(member);

            if (doc != null)
            {
                string memberId = GetMemberId(member);
                if (!string.IsNullOrEmpty(memberId))
                {
                    var memberElement = doc.Descendants("member")
                        .FirstOrDefault(e => e.Attribute("name")?.Value == memberId);

                    var paramElement = memberElement?.Elements("param")
                        .FirstOrDefault(e => e.Attribute("name")?.Value == parameter.Name);

                    if (paramElement != null)
                    {
                        return paramElement.Value.Trim();
                    }
                }
            }

            return GetSourceParameterDescription(parameter);
        }

        private static XDocument? GetXmlDoc(MemberInfo member)
        {
            var assembly = member.DeclaringType?.Assembly;
            if (assembly == null) return null;

            if (!_cache.TryGetValue(assembly, out var doc))
            {
                doc = LoadXmlDocumentation(assembly);
                _cache[assembly] = doc;
            }
            return doc;
        }

        private static string? GetSourceDescription(MemberInfo member)
        {
            var result = FindMemberInSource(member);
            if (result.HasValue)
            {
                var xml = ExtractCommentXml(result.Value.lines, result.Value.index);
                return xml?.Element("summary")?.Value.Trim();
            }
            return null;
        }

        private static string? GetSourceParameterDescription(ParameterInfo parameter)
        {
            var result = FindMemberInSource(parameter.Member);
            if (result.HasValue)
            {
                var xml = ExtractCommentXml(result.Value.lines, result.Value.index);
                if (xml != null)
                {
                    var paramElement = xml.Elements("param")
                       .FirstOrDefault(e => e.Attribute("name")?.Value == parameter.Name);
                    return paramElement?.Value.Trim();
                }
            }
            return null;
        }

        private static (string[] lines, int index)? FindMemberInSource(MemberInfo member)
        {
            var type = member.DeclaringType;
            if (type == null) return null;

            // Simple heuristic: find .cs files in current directory
            try
            {
                var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.cs", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var lines = GetFileLines(file);

                    // Check if type is defined in this file
                    bool isScriptOrProgram = type.Name == "Program" || type.Name.StartsWith("<");
                    bool hasType = isScriptOrProgram || lines.Any(l => Regex.IsMatch(l, $@"\b(class|struct)\s+{type.Name}\b"));

                    if (hasType)
                    {
                        // Clean up member name if it's a local function
                        string memberName = member.Name;
                        var match = Regex.Match(memberName, @">g__(.+)\|");
                        if (match.Success)
                        {
                            memberName = match.Groups[1].Value;
                        }

                        // Construct regex based on member type
                        string pattern;
                        if (member is FieldInfo)
                        {
                            // Field: Type Name = ...; or Type Name;
                            pattern = $@"\s+{memberName}\s*(=|;)";
                        }
                        else if (member is MethodInfo)
                        {
                            // Method: ReturnType Name(...)
                            pattern = $@"\s+{memberName}\s*\(";
                        }
                        else
                        {
                            continue;
                        }

                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (Regex.IsMatch(lines[i], pattern))
                            {
                                return (lines, i);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore IO errors
            }

            return null;
        }

        private static XElement? ExtractCommentXml(string[] lines, int lineIndex)
        {
            var comments = new List<string>();
            for (int i = lineIndex - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("///"))
                {
                    comments.Insert(0, line.Substring(3));
                }
                else
                {
                    break;
                }
            }

            if (comments.Count > 0)
            {
                // Wrap in root element to allow multiple elements (summary, param, etc.)
                var fullXml = "<root>" + string.Join(Environment.NewLine, comments) + "</root>";
                try
                {
                    return XDocument.Parse(fullXml).Root;
                }
                catch
                {
                    // Malformed XML in comments
                }
            }
            return null;
        }

        private static string[] GetFileLines(string path)
        {
            if (_sourceFileCache.TryGetValue(path, out var lines)) return lines;
            lines = File.ReadAllLines(path);
            _sourceFileCache[path] = lines;
            return lines;
        }

        private static XDocument? LoadXmlDocumentation(Assembly assembly)
        {
            try
            {
                var location = assembly.Location;
                if (string.IsNullOrEmpty(location)) return null;

                var xmlPath = Path.ChangeExtension(location, ".xml");
                if (File.Exists(xmlPath))
                {
                    return XDocument.Load(xmlPath);
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        private static string GetMemberId(MemberInfo member)
        {
            string typeName = member.DeclaringType?.FullName ?? "";

            if (member is FieldInfo field)
            {
                return $"F:{typeName}.{field.Name}";
            }

            if (member is MethodInfo method)
            {
                var parameters = method.GetParameters();
                var paramString = string.Join(",", parameters.Select(p => p.ParameterType.FullName));
                if (!string.IsNullOrEmpty(paramString))
                {
                    paramString = $"({paramString})";
                }
                return $"M:{typeName}.{method.Name}{paramString}";
            }

            return "";
        }
    }

}

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Reflection;
using System.Text.RegularExpressions;
using Command = System.CommandLine.Command;

namespace clapnet
{
    enum CaseType
    {
        CamelCase,
        SnakeCase
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
            }
            return result;
        }

        private Func<ParseResult, object>? BuildArgumentForType(FieldInfo field, string argumentName, ref Command command)
        {
            var fieldName = $"--{argumentName}";
            if (field.FieldType == typeof(string))
            {
                var option = new Option<string>(fieldName);

                command.Add(option);
                return result => result.GetValue(option) ?? string.Empty;
            }
            else if (field.FieldType == typeof(int))
            {
                var option = new Option<int>(fieldName);

                command.Add(option);
                return result => result.GetValue(option);
            }
            else if (field.FieldType == typeof(bool))
            {
                var option = new Option<bool>(fieldName);

                command.Add(option);
                return result => result.GetValue(option);
            }
            else if (field.FieldType == typeof(float))
            {
                var option = new Option<float>(fieldName);

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
            if (parameterInfo.ParameterType == typeof(bool))
            {
                var def = false;
                if (parameterInfo.DefaultValue != null)
                {
                    def = (bool)parameterInfo.DefaultValue;
                }

                var boolArgument = new Argument<bool>(parameterInfo.Name);
                if (parameterInfo.HasDefaultValue)
                {
                    boolArgument.DefaultValueFactory = _ => def;
                }

                argument = boolArgument;
                func = parse => parse.GetValue(boolArgument);
            }
            else if (parameterInfo.ParameterType == typeof(string))
            {
                var option = new Argument<string>(parameterInfo.Name);
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
                var option = new Argument<int>(parameterInfo.Name);
                if (parameterInfo.HasDefaultValue)
                {
                    option.DefaultValueFactory = _ => (int)parameterInfo.DefaultValue;
                }

                argument = option;
                func = parse => parse.GetValue(option);
            }
            else if (parameterInfo.ParameterType == typeof(float))
            {
                var option = new Argument<float>(parameterInfo.Name);
                if (parameterInfo.HasDefaultValue)
                {
                    option.DefaultValueFactory = _ => (float)parameterInfo.DefaultValue;
                }

                argument = option;
                func = parse => parse.GetValue(option);
            }
            else if (parameterInfo.ParameterType == typeof(double))
            {
                var option = new Argument<double>(parameterInfo.Name);
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
            if (!string.IsNullOrEmpty(description))
            {
                cmd.Description = description;
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
            if (!string.IsNullOrEmpty(description))
            {
                _rootCommand.Description = description;
            }

            var method = func.Method;
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
}

using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Command = System.CommandLine.Command;

namespace clapnet;

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
    private readonly Dictionary<Type, Func<ParseResult, object>> _settingsBuilders = new Dictionary<Type, Func<ParseResult, object>>();
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

    private Func<ParseResult, object>? BuildArgumentForType(FieldInfo field)
    {
        var fieldName = $"--{field.Name.ToLower()}";
        if (field.FieldType == typeof(string))
        {
            var option = new Option<string>(fieldName);

            option.Recursive = true;
            _rootCommand.Add(option);
            return result => result.GetValue(option) ?? string.Empty;
        }else if (field.FieldType == typeof(int))
        {
            var option = new Option<int>(fieldName);

            option.Recursive = true;
            _rootCommand.Add(option);
            return result => result.GetValue(option);
        } else if (field.FieldType == typeof(bool))
        {
            var option = new Option<bool>(fieldName);

            option.Recursive = true;
            _rootCommand.Add(option);
            return result => result.GetValue(option);
        }

        return null;
    }

    /// <summary>
    /// Adds extra settings class to the CLI program.
    /// It will work that way that once a settings class is added, it can be used as a parameter in the added methods.
    /// The method will get a new object of that class with field overrides based on the passed options to the CLI.
    /// </summary>
    /// <returns>Builder</returns>
    public CommandBuilder WithSettings<T>()
    {
        var arguments = new Dictionary<string,Func<ParseResult,object>>();
        foreach (var field in
                 typeof(T).GetFields())
        {
            var optionFn = BuildArgumentForType(field);
            if (optionFn != null)
            {
                arguments.Add(field.Name, optionFn);
            }
        }
        _settingsBuilders.Add(typeof(T), result =>
        {
            var instance = Activator.CreateInstance(typeof(T));
            FieldInfo[] ps = instance.GetType().GetFields();
            foreach (var field in ps)
            {
                var o = field.GetValue(instance);
                var p = instance.GetType().GetField(field.Name);
                if (o != null)
                {
                    if (arguments.TryGetValue(field.Name, out Func<ParseResult, object> f))
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
        return this;
    }

    public CommandBuilder WithOption<T>(string name, string description = "", bool required = false)
    {
        var option = new Option<T>(name){Description = description, Required = required, Recursive = true};
        _rootCommand.Add(option);
        return this;
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
            var index = name.LastIndexOf("__");
            name = name.Substring(index+2);
        }

        name = name.ToLowerInvariant();
        Command cmd = new Command(name);

        var parameters = method.GetParameters();
        var isInt = method.ReturnType == typeof(int);
        var arguments = new List<Func<ParseResult,object>>();
        foreach (var parameterInfo in parameters)
        {
            if (_settingsBuilders.TryGetValue(parameterInfo.ParameterType, out var f))
            {
                arguments.Add(f);
            }
            else if (parameterInfo.ParameterType == typeof(bool))
            {
                var def = false;
                if (parameterInfo.DefaultValue != null)
                {
                    def =(bool)parameterInfo.DefaultValue;
                }
                var option = new Argument<bool>(parameterInfo.Name);
                if (parameterInfo.HasDefaultValue)
                {
                    option.DefaultValueFactory = _ => def;
                }
                cmd.Arguments.Add(option);
                arguments.Add(parse => parse.GetValue(option));
            } else if (parameterInfo.ParameterType == typeof(string))
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
                cmd.Arguments.Add(option);
                arguments.Add(parse => parse.GetValue(option) ?? string.Empty);
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
                Console.WriteLine($"Result is int: {i}");
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
            cmd.Description  = description;
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
            _rootCommand.Description  = description;
        }
        var method = func.Method;
        var parameters = method.GetParameters();
        var isInt = method.ReturnType == typeof(int);
        var arguments = new List<Func<ParseResult,object>>();
        foreach (var parameterInfo in parameters)
        {
            if (_settingsBuilders.TryGetValue(parameterInfo.ParameterType, out var f))
            {
                arguments.Add(f);
            }
            else if (parameterInfo.ParameterType == typeof(bool))
            {
                var def = false;
                if (parameterInfo.DefaultValue != null)
                {
                    def =(bool)parameterInfo.DefaultValue;
                }
                var option = new Argument<bool>(parameterInfo.Name);
                if (parameterInfo.HasDefaultValue)
                {
                    option.DefaultValueFactory = _ => def;
                }
                _rootCommand.Arguments.Add(option);
                arguments.Add(parse => parse.GetValue(option));
            } else if (parameterInfo.ParameterType == typeof(string))
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
                _rootCommand.Arguments.Add(option);
                arguments.Add(parse => parse.GetValue(option) ?? string.Empty);
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
                Console.WriteLine($"Result is int: {i}");
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
}

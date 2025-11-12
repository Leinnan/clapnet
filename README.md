# clapnet

[![Version](https://img.shields.io/nuget/v/clapnet.svg)](https://nuget.org/packages/clapnet)
[![Downloads](https://img.shields.io/nuget/dt/clapnet.svg)](https://nuget.org/packages/clapnet)

Small helper library that provides a simple way to use `System.CommandLine` by just passing a bunch of functions into a builder and creating a command line application.

## Usage

It works great with the newly introduced dotnet scripts. More about the scripts [here](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/).

Logic for how the library turns a function into a command is straightforward:
- each basic type (e.g. bool, string, int) parameter is turned into an argument
- each class parameter is turned into bunch of options, where each field is turned into an option. Default values from class declaration are respected.
- if return type is `int` then the command will return the value of the function. Otherwise, it will return 0.

### Small example

This is a valid program that can be run from the command line:

```cs
#!/usr/bin/env dotnet run
#:package clapnet@0.2.*

return clapnet.CommandBuilder.New()
    .WithRootCommand(()=> Console.WriteLine("Hello world"), "Small program")
    .Run(args);
```

### More feature-rich example

Example with extra settings and more commands:

```cs
#!/usr/bin/env dotnet run
#:project clapnet@0.2.*

var builder = clapnet.CommandBuilder.New();
return builder
    .With(()=> Console.WriteLine("ssss"), "", "lambda_two")
    .With(Gather, "Documentation for gather command")
    .With(Failing, "This command will fail")
    .With(()=> Console.WriteLine("ssss"),"Test command", "lambda")
    .WithRootCommand(Other, "Super command to show what can be done")
    .Run(args);

void Gather(SomeSettings settings, string test2 = "some", bool assert = false)
{
    Console.WriteLine("Hello World argument test: {0}", test2);
}

void Other(SomeSettings settings)
{
    Console.WriteLine("Hello World from the root command");
    Console.WriteLine("Hello World from the root command");
}

int Failing()
{
    /// Returning non zero value
    return 1;
}

class SomeSettings
{
    /// <summary>
    /// Test value, it will be possible to set its value by passing `--test true/false`.
    /// </summary>
    public bool Test = false;
    /// <summary>
    /// Test value, it will be possible to set its value by passing `--other "string value"`.
    /// </summary>
    public string other = "Default Value";
}
```

Now when called from the CLI it will print out:

```bash
 ./Test.cs -- --help
Description:
  Super command to show what can be done

Usage:
  Test [command] [options]

Options:
  --test
  --other <other>
  -?, -h, --help   Show help and usage information
  --version        Show version information

Commands:
  lambda_two
  gather <test> <assert>  Documentation for gather command [default: some]
  failing                 This command will fail
  lambda                  Test command
```

Take a look at the [examples](examples) directory for a simple example of how to use `clapnet`.

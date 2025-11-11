#!/usr/bin/env dotnet run
#:project ../clapnet/clapnet.csproj

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

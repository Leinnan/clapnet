#!/usr/bin/env dotnet run
#:project ../clapnet/clapnet.csproj

var builder = clapnet.CommandBuilder.New();
return builder
    .UseCamelCase()
    .With(() => Console.WriteLine("ssss"), "Other function to call", "lambda_two")
    .With(Gather, "Documentation for gather command")
    .With(Failing)
    .With(() => Console.WriteLine("ssss"), "Test command", "lambda")
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

/// <summary>
/// This command will fail
/// </summary>
int Failing()
{
    /// Returning non zero value
    return 1;
}

/// <summary>
/// Config
/// </summary>
class SomeSettings
{
    /// <summary>
    /// Test value.
    /// </summary>
    public bool TestV = false;
    /// <summary>
    /// Test value.
    /// </summary>
    public string other = "Default Value";

}

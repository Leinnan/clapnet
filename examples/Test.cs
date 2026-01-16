#!/usr/bin/env dotnet run
#:project ../clapnet/clapnet.csproj

var builder = clapnet.CommandBuilder.New();
return builder
    .UseCamelCase()
    .With(() => Console.WriteLine("ssss"), "Other function to call", "lambda_two")
    .With(Gather)
    .With(Failing)
    .With(() => Console.WriteLine("ssss"), "Test command", "lambda")
    .WithRootCommand(Other, "Super command to show what can be done")
    .Run(args);

/// <summary>
/// Documentation for gather command
/// </summary>
/// <param name="test2">extra argument</param>
/// <param name="assert">other argument</param>
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

class SomeSettings
{
    /// <summary>
    /// Test value, first one.
    /// </summary>
    public bool TestV = false;
    /// <summary>
    /// Test value.
    /// </summary>
    public string other = "Default Value";
}

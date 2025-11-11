#!/usr/bin/env dotnet run
#:package clapnet@0.1.*

return clapnet.CommandBuilder.New()
    .WithRootCommand(()=> Console.WriteLine("Hello world"), "Small program")
    .Run(args);

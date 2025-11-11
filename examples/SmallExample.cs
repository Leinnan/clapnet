#!/usr/bin/env dotnet run
#:package clapnet@0.2.*

return clapnet.CommandBuilder.New()
    .WithRootCommand(()=> Console.WriteLine("Hello world"), "Small program")
    .Run(args);

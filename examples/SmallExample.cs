#!/usr/bin/env dotnet run
#:package clapnet@0.2.*

return clapnet.CommandBuilder.New()
    .WithRootCommand((double argument = 1.0) => Console.WriteLine($"Twice: {argument * 2.0}"), "Small program")
    .Run(args);

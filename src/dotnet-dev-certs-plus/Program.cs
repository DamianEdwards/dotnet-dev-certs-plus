using System.CommandLine;
using DotnetDevCertsPlus.Commands;

var rootCommand = new RootCommand("Extended functionality for the dotnet dev-certs command");
rootCommand.Subcommands.Add(HttpsCommand.Create());

return await rootCommand.Parse(args).InvokeAsync();

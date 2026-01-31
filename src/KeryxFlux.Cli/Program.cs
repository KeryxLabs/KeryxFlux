using System.Text;
using Cocona;
using KeryxFlux.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

var builder = CoconaApp.CreateBuilder();

builder.Services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);

var app = builder.Build();

app.AddCommand("validate", ValidateCommand.Execute)
    .WithDescription("Validate a docket file");

app.AddCommand("preview", PreviewCommand.Execute)
    .WithDescription("Preview docket variable resolution and generated URLs");

app.AddCommand("create", CreateCommand.Execute)
    .WithDescription("Create a new docket from a template");

app.AddCommand("debug", DebugCommand.Execute)
    .WithDescription("Debug docket configuration and variable resolution");

await app.RunAsync();

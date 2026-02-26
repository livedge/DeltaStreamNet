using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Running;
using DeltaStreamNet;
using DeltaStreamNet.Benchmarks;

if (args.Contains("--payload-report"))
{
    PayloadHelpers.PrintPayloadSizeReport();
    return;
}

if (args.Contains("--json-demo"))
{
    PayloadHelpers.PrintJsonDemo();
    return;
}

if (args.Contains("--mode-comparison"))
{
    PayloadHelpers.PrintModeComparison();
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

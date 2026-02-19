using BenchmarkDotNet.Running;
using TutoProxy.Tests.Benchmarks;

// Run benchmarks when executed directly
// Usage: dotnet run -c Release -- --filter "*TcpThroughput*"
if(args.Length > 0 && args[0] == "--benchmark") {
    BenchmarkSwitcher.FromAssembly(typeof(TcpThroughputBenchmark).Assembly).Run(args.Skip(1).ToArray());
} else {
    Console.WriteLine("TutoProxy Integration Tests & Benchmarks");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  Run tests:      dotnet test");
    Console.WriteLine("  Run benchmarks: dotnet run -c Release -- --benchmark");
    Console.WriteLine();
}

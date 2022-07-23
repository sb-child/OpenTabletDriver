using System.Numerics;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Desktop.Interop.Input.Absolute;
using OpenTabletDriver.Desktop.Interop.Input.Relative;
using OpenTabletDriver.Platform.Pointer;

namespace OpenTabletDriver.Benchmarks.Output
{
    public class WindowsInteropBenchmark
    {
        private readonly IAbsolutePointer _absolutePointer;
        private readonly IRelativePointer _relativePointer;

        public WindowsInteropBenchmark()
        {
            var serviceProvider = new DesktopWindowsServiceCollection().BuildServiceProvider();
            _absolutePointer = serviceProvider.GetRequiredService<IAbsolutePointer>();
            _relativePointer = serviceProvider.GetRequiredService<IRelativePointer>();
        }

        [Benchmark]
        public void SendInputAbsolute()
        {
            _absolutePointer.SetPosition(Vector2.Zero);
        }

        [Benchmark]
        public void SendInputRelative()
        {
            _relativePointer.SetPosition(Vector2.Zero);
        }
    }
}

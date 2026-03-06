using System.Linq;
using OpenTabletDriver.Desktop.Interop.Input.Exotic;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Platform.Keyboard;
using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.Desktop.Binding.LinuxArtistMode
{
    [PluginName("Linux Artist Mode Pad Bindings"), SupportedPlatform(PluginPlatform.Linux)]
    public class LinuxArtistModePadBinding : IStateBinding
    {
        [Resolved]
        public IVirtualPad VirtualPad;

        public static string[] ValidKeys => EvdevVirtualPad.ValidButtons.Keys.ToArray();

        [Property("Button"), PropertyValidated(nameof(ValidKeys))]
        public string Button { get; set; }

        public void Press(TabletReference tablet, IDeviceReport report)
        {
            SetState(true);
        }

        public void Release(TabletReference tablet, IDeviceReport report)
        {
            SetState(false);
        }

        private void SetState(bool isPress)
        {
            VirtualPad.KeyEvent(Button, isPress);
        }

        public override string ToString() => $"{nameof(LinuxArtistModePadBinding)}: {Button ?? "<button not set>"}";
    }
}

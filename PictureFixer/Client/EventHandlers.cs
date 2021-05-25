using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;

namespace PictureFixer.Client
{
    [EventHandler("onregiondrawn", typeof(RegionDrawnEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
    public static class EventHandlers
    {
        // This static class configures the Razor compiler
    }


    public class RegionDrawnEventArgs : EventArgs, IAsyncDisposable
    {
        public IJSUnmarshalledObjectReference SourceImage { get; set; }
        public IJSUnmarshalledObjectReference SelectedRegion { get; set; }

        public async ValueTask DisposeAsync()
        {
            // The image data is held in JS memory so the .NET code can read it whenever it wants
            // When .NET is done with it, notify JS so it can release the buffers
            await SourceImage.DisposeAsync();
            await SelectedRegion.DisposeAsync();
        }
    }
}

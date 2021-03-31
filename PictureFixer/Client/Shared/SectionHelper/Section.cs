using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace PictureFixer.Client.Shared.SectionHelper
{
    // This is an example of how sections can be implemented

    public class Section : IComponent, IDisposable
    {
        private SectionRegistry _registry;

        [Parameter] public string Name { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }

        public void Attach(RenderHandle renderHandle)
        {
            _registry = SectionRegistry.GetRegistry(renderHandle);
        }

        public Task SetParametersAsync(ParameterView parameters)
        {
            parameters.SetParameterProperties(this);
            _registry.SetContent(Name, ChildContent);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                // This relies on the assumption that the old SectionContent gets disposed before the
                // new one is added to the output. This won't be the case in all possible scenarios.
                // We should have the registry keep track of which SectionContent is the most recent
                // one to supply new content, and disregard updates from ones that were superseded.
                _registry.SetContent(Name, null);
            }
        }
    }
}

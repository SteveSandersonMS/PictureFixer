using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace PictureFixer.Client.Shared.SectionHelper
{
    public class SectionOutlet : IComponent, IDisposable
    {
        private static RenderFragment EmptyRenderFragment = builder => { };
        private RenderHandle _renderHandle;
        private SectionRegistry _registry;
        private Action<RenderFragment> _onChangeCallback;
        private RenderFragment _currentContent;

        [Parameter] public string Name { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }

        public void Attach(RenderHandle renderHandle)
        {
            _renderHandle = renderHandle;
            _registry = SectionRegistry.GetRegistry(renderHandle);
            _onChangeCallback = content =>
            {
                _currentContent = content;
                Render();
            };
        }

        public Task SetParametersAsync(ParameterView parameters)
        {
            var suppliedName = parameters.GetValueOrDefault<string>(nameof(Name));

            if (suppliedName != Name)
            {
                _registry.Unsubscribe(Name, _onChangeCallback);
                _registry.Subscribe(suppliedName, _onChangeCallback);
                Name = suppliedName;
            }

            ChildContent = parameters.GetValueOrDefault<RenderFragment>(nameof(ChildContent));

            Render();
            return Task.CompletedTask;
        }

        private void Render()
            => _renderHandle.Render(_currentContent ?? ChildContent ?? EmptyRenderFragment);

        public void Dispose()
            => _registry?.Unsubscribe(Name, _onChangeCallback);
    }
}

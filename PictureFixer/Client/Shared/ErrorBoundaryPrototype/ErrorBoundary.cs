using System;
using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components
{
    // This is a temporary placeholder for the real ErrorBoundary feature which is currently expected
    // to first appear in .NET 6 Preview 4. It doesn't have all the features of the real implementation,
    // but is sufficient to show the main ideas in a demo.

    public class ErrorBoundary : ComponentBase
    {
        private Exception receivedException;

        [Parameter] public RenderFragment ChildContent { get; set; }
        [Parameter] public RenderFragment<Exception> ErrorContent { get; set; }
        [Parameter] public bool RecoverOnRender { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (receivedException == null)
            {
                builder.OpenComponent<CascadingValue<ErrorBoundary>>(0);
                builder.AddAttribute(1, "Value", this);
                builder.AddAttribute(1, "ChildContent", ChildContent);
                builder.CloseComponent();
            }
            else if (ErrorContent != null)
            {
                builder.AddContent(2, ErrorContent(receivedException));
            }
            else
            {
                builder.OpenElement(3, "div");
                builder.AddAttribute(4, "class", "error");
                builder.AddContent(5, receivedException.ToString());
                builder.CloseElement();
            }
        }

        protected override void OnParametersSet()
        {
            if (RecoverOnRender)
            {
                receivedException = null;
            }
        }

        internal void NotifyException(Exception exception)
        {
            if (receivedException == null && exception != null)
            {
                receivedException = exception;
                StateHasChanged();
            }
        }
    }
}

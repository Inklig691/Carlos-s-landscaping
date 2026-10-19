using FrontEnd.Services;
using Microsoft.AspNetCore.Components;

namespace FrontEnd.Shared;

public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject]
    protected LanguageState Language { get; set; } = default!;

    protected override void OnInitialized()
    {
        Language.Changed += OnLanguageChanged;
    }

    protected virtual void OnLanguageChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Language.Changed -= OnLanguageChanged;
}
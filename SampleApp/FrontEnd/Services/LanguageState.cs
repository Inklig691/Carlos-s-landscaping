namespace FrontEnd.Services;

public sealed class LanguageState
{
    public string Code { get; private set; } = "es-US";
    public bool IsEnglish => Code == "en-US";

    public event Action? Changed;

    public void SetLanguage(string code)
    {
        var normalized = code == "en-US" ? "en-US" : "es-US";
        if (Code == normalized)
        {
            return;
        }

        Code = normalized;
        Changed?.Invoke();
    }
}
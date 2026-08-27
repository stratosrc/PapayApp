namespace PapayApp.UI.Services;

public sealed class CertificateFolioStore : ICertificateFolioStore
{
    private const string LastUsedFolioKey = "PapayApp.Certificates.LastUsedFolio";
    private const int DefaultLastUsedFolio = 1029;

    public int LastUsedFolio
    {
        get => Preferences.Default.Get(LastUsedFolioKey, DefaultLastUsedFolio);
        set => Preferences.Default.Set(LastUsedFolioKey, value);
    }
}

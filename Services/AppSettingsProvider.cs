using LabelAva.Models;

namespace LabelAva.Services;

public class AppSettingsProvider
{
    private AppSettings _current;

    public AppSettings Current => _current;

    public event EventHandler<(AppSettings settings, SettingsChangeKind changes)>? SettingsChanged;

    public AppSettingsProvider()
    {
        _current = AppSettings.CreateDefaults();
    }

    public void Load()
    {
        _current = AppSettingsService.Load();
    }

    public void Update(AppSettings settings, SettingsChangeKind changes)
    {
        _current = settings;
        SettingsChanged?.Invoke(this, (settings, changes));
    }

    public void Save()
    {
        AppSettingsService.Save(_current);
    }
}

using System.Windows;
using System.Globalization;

namespace KoThumbMini;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SetLanguage();
    }

    private void SetLanguage()
    {
        string culture = CultureInfo.CurrentUICulture.Name;
        string dictPath = culture.StartsWith("ja") 
            ? "Resources/Langs.ja-JP.xaml" 
            : "Resources/Langs.en-US.xaml";

        ResourceDictionary dict = new ResourceDictionary();
        dict.Source = new Uri(dictPath, UriKind.Relative);
        
        // Remove existing lang dict if any, or just add
        this.Resources.MergedDictionaries.Add(dict);
    }
}

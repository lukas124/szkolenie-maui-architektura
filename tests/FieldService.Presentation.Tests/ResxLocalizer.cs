using System.Globalization;
using System.Xml.Linq;
using FieldService.Presentation.Common;

namespace FieldService.Presentation.Tests;

// Atrapa ILocalizer na prawdziwych zasobach aplikacji: teksty z Strings.resx, a dla innego języka niż polski
// nadpisane tekstami z Strings.<język>.resx. Kulturę i strefę czasową podaje test, nie maszyna z testami.
public sealed class ResxLocalizer(string culture, string timeZone) : ILocalizer
{
    private readonly Dictionary<string, string> _texts = Load(culture);

    public string this[string key] => _texts.GetValueOrDefault(key, key);

    public CultureInfo CurrentCulture { get; } = CultureInfo.GetCultureInfo(culture);

    // ustawiana także w trakcie testu – tak jak strefa telefonu może się zmienić przy działającej aplikacji
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.FindSystemTimeZoneById(timeZone);

    private static Dictionary<string, string> Load(string culture)
    {
        var texts = Read("Strings.resx");
        var language = culture.Split('-')[0];
        if (language == "pl") return texts;

        foreach (var (key, text) in Read($"Strings.{language}.resx")) texts[key] = text;
        return texts;
    }

    // plik zasobów z projektu UI (testy nie mogą odwołać się do projektu MAUI, więc czytają plik z repozytorium)
    private static Dictionary<string, string> Read(string fileName)
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root.GetFiles("FieldService.slnx").Length == 0) root = root.Parent!;

        var path = Path.Combine(root.FullName, "src", "FieldService.UI", "Resources", "Strings", fileName);
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(data => data.Attribute("name")!.Value, data => data.Element("value")!.Value);
    }
}

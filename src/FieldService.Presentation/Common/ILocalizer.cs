namespace FieldService.Presentation.Common;

// Port prezentacji: teksty interfejsu po kluczu (implementacja w UI na Strings.resx)
public interface ILocalizer
{
    string this[string key] { get; }
}

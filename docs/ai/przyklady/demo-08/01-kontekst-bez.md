Nagrana odpowiedź LLM do dema (plan B) – do weryfikacji, nie wzorzec.

# Demo 08 · krok 1 – pytanie bez kontekstu

Kopia repozytorium bez `AGENTS.md`, `src/*/AGENTS.md`, `.github/` i `docs/`.

## Prompt

```text
Dodaj regułę: wynik NotFixed wymaga co najmniej jednej zapisanej czynności. Gdzie i jak?
```

## Odpowiedź LLM

Najprościej dodać sprawdzenie w ViewModelu ekranu szczegółów, tuż przed wysłaniem zakończenia do API:

```csharp
// VisitDetailsViewModel.cs
private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.fieldservice.example/") };

[RelayCommand]
private async Task CompleteNotFixedAsync(string reason)
{
    if (Details is null) return;

    if (Details.Activities.Count == 0)
    {
        await Application.Current!.MainPage!.DisplayAlert(
            "Błąd", "Wynik „nie naprawiono” wymaga co najmniej jednej czynności.", "OK");
        return;
    }

    var response = await _http.PostAsJsonAsync($"api/visits/{Details.Id}/complete",
        new { outcome = "NotFixed", reason });
    if (!response.IsSuccessStatusCode)
        await Application.Current!.MainPage!.DisplayAlert("Błąd", "Nie udało się zakończyć wizyty.", "OK");
}
```

Reguła działa od razu, a komunikat jest widoczny dla technika.

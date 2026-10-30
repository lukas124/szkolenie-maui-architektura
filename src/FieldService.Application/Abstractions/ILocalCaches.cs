namespace FieldService.Application.Abstractions;

// Port: lokalne cache technika (katalog części, odpowiedzi HTTP z ETagiem, migawka listy, miniatury).
// Czyścimy przy wylogowaniu i zmianie technika – następna osoba nie zobaczy cudzych danych.
public interface ILocalCaches
{
    Task ClearAsync(CancellationToken ct);

    // liczba wpisów we wszystkich cache (Diagnostyka)
    Task<int> CountAsync(CancellationToken ct);
}

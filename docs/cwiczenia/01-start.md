# Rozruch — 01-start

**Blok:** Otwarcie i rozruch · dzień 1 · **Start:** gałąź `01-start`

## Cel
Masz działający szkielet FieldService: pięć pustych projektów z referencjami zgodnymi z regułą zależności, kontener
DI w `MauiProgram`, pusty ekran startowy i zielone testy.

## Kroki
1. Pobierz repozytorium i przejdź na checkpoint:
   ```bash
   git clone <adres repozytorium>
   cd szkolenie-maui-architektura
   git switch 01-start
   ```
2. Zbuduj i uruchom aplikację na emulatorze albo telefonie:
   ```bash
   dotnet build src/FieldService.UI -t:Run -f net10.0-android
   ```
3. Uruchom testy:
   ```bash
   dotnet test
   ```
4. Otwórz `FieldService.slnx` w IDE i obejrzyj referencje między projektami (`ProjectReference` w plikach `.csproj`):
   kto kogo zna?

## Kryteria akceptacji
- Na ekranie widać „FieldService”, numer wersji i adres API z `appsettings.json`.
- `dotnet test` – pięć testów dymnych na zielono.

## Podpowiedzi
- Pierwszy build i instalacja trwają długo – to normalne.
- Emulator bez akceleracji sprzętowej → telefon po USB.

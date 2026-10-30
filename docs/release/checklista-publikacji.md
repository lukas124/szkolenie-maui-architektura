# Checklista publikacji FieldService

Wydanie przechodzi przez pipeline CD (`.github/workflows/cd.yml` albo `azure-pipelines/cd.yml`) – nic nie budujemy ani nie
podpisujemy ręcznie na komputerze programisty. Jeden przebieg i jeden commit dają dwie aplikacje z tą samą wersją:

| Aplikacja | Identyfikator | Konfiguracja | Dokąd trafia |
|---|---|---|---|
| TEST | `pl.szkolenie.fieldservice.test` | `Test` | testerzy: Google Play – testy wewnętrzne, TestFlight |
| PROD | `pl.szkolenie.fieldservice` | `Release` | sklepy – dopiero po pozytywnych testach aplikacji TEST i zatwierdzeniu |

PROD nigdy nie trafia na ścieżkę testową. Testerzy sprawdzają TEST, a do sklepu idzie PROD zbudowany z tego samego kodu
w tym samym przebiegu – różni się tylko konfiguracją (identyfikator, środowisko, strona Diagnostyka).

## Gałęzie (gitflow)
- `develop` – bieżąca praca: pull requesty z gałęzi `feature/*`, CI przy każdym pushu i pull requeście.
- `release/1.4.0` – wydanie: gałąź z `develop`, tylko poprawki tego wydania, wersje `1.4.0-beta.N`.
- `main` – tylko wydania: pull request z `release/1.4.0` (albo `hotfix/1.4.1`), po scaleniu tag `v1.4.0` i scalenie
  z powrotem do `develop`. CD uruchamiasz na `main` albo na tagu `v*` – z innej gałęzi przebieg niczego nie buduje.
- `hotfix/1.4.1` – pilna poprawka wydania: gałąź z `main`, scalona do `main` i do `develop`.

## Przed uruchomieniem CD
- [ ] Gałąź `release/1.4.0` scalona do `main` przez pull request. CI zielony na `develop` i na `main` (wszystkie testy, także
      architektury: `tests/FieldService.Tests`), reguły ochrony gałęzi `develop` i `main` (wymagany zielony CI przed
      scaleniem) – dopiero wtedy czerwony test zatrzymuje scalenie.
- [ ] Obie aplikacje założone w Google Play Console i w App Store Connect; pierwszy AAB każdej aplikacji wgrywasz ręcznie
      (potem już API); App Store Connect: Export Compliance (szyfrowanie) uzupełnione.
- [ ] Wersja: tag `v1.4.0` na `main` po scaleniu `release/1.4.0` – `ApplicationDisplayVersion` liczy GitVersion,
      `ApplicationVersion` = numer przebiegu pipeline'u (zawsze rośnie, sklepy tego wymagają); TEST i PROD mają te same.
- [ ] Sekrety i pliki podpisu w CI (GitHub: Settings → Secrets; Azure DevOps: Library i Secure files, Service connections) –
      nigdy w repozytorium; nazwy w nagłówku `cd.yml`. iOS: osobny profil App Store dla TEST i dla PROD.
- [ ] Backend telemetrii: `OTLP_ENDPOINT`, `OTLP_HEADERS`, próbkowanie w Release – 0.2.
- [ ] Środowisko `production` z wymaganym zatwierdzeniem (osoba inna niż autor zmian).

## Po zbudowaniu
- [ ] Artefakty obu aplikacji: `fieldservice-aab-Test`/`-Release`, `fieldservice-ipa-Test`/`-Release` i symbole
      `symbols-*-Test`/`-Release` – symbole z TEGO SAMEGO buildu (czytelny stos w raportach awarii tej wersji).
- [ ] Testy aplikacji TEST (testy wewnętrzne Google Play, TestFlight): instalacja na telefonie testowym, logowanie, lista
      wizyt, zakończenie wizyty, tryb samolotowy i wysyłka zmian po powrocie sieci.
- [ ] Telemetria: ślady i metryki nowej wersji (`service.version`) widoczne w backendzie; brak nowych awarii.

## Wydanie
- [ ] Testy pozytywne → zatwierdzenie w środowisku `production`. Testy negatywne → bez zatwierdzenia, poprawka i nowy
      przebieg CD (PROD z tego przebiegu nigdy nie trafia do sklepu).
- [ ] Google Play: PROD na ścieżce produkcyjnej z wydawaniem stopniowym (10 %). App Store: build PROD w App Store Connect
      wysłany do review (opis wersji, zrzuty ekranów, informacje o prywatności).
- [ ] Obserwacja przez pierwsze dni: awarie i błędy wg `service.version`. Przy problemie – wstrzymanie wydawania stopniowego,
      poprawka na gałęzi `hotfix/1.4.1` z `main` tą samą linią (CI → CD), nowa wersja.

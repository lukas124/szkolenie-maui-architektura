# Ćwiczenie 05 — aplikacja na mapie FieldService

**Blok:** 05 · Logika biznesowa: mapa kontekstów · dzień 1 · **Czas:** 5 min · indywidualnie, bez kodu

Kartę zobaczysz na slajdzie „Ćwiczenie 05”; ten plik jest w checkpoincie `05-mapa-kontekstow` razem z rozwiązaniem
(`docs/architecture/mapa-kontekstow.md`) – zajrzyj do niego dopiero po ćwiczeniu.

## User story
Jako programista aplikacji mobilnej chcę wiedzieć, do których modeli serwera aplikacja się dostosowuje, a gdzie
potrzebuje warstwy ochronnej, aby zmiany w panelu dyspozytora nie psuły aplikacji technika.

## Materiał
Mapa FieldService ze slajdu „Mapa FieldService” – osiem modeli, każdy z głównym pytaniem:

| Model | Główne pytanie |
|---|---|
| Realizacja wizyty | Co technik zrobił na tej wizycie i czy klient to potwierdził? |
| Planowanie | Kto i kiedy jedzie do klienta? |
| Zlecenia | Czy sprawa klienta jest załatwiona i czy dotrzymaliśmy SLA? |
| Magazyn części | Czy mogę dostać tę część dla tej wizyty? |
| Protokoły | Co wpisać do protokołu z wizyty? |
| Klienci i urządzenia | Gdzie jest urządzenie i kto jest kontaktem? |
| Powiadomienia | Jak powiadomić klienta, że technik jedzie? |
| Tożsamość | Kim jest zalogowany technik? |

Aplikacja mobilna to „okno” na model Realizacja wizyty.

## Polecenie
Wypełnij tabelę dla czterech modeli, z których korzysta aplikacja:

| Model | Upstream aplikacji? (tak / nie) | Dostosowujemy się (Conformist) czy ACL? | Dlaczego (jedno zdanie) |
|---|---|---|---|
| Planowanie | | | |
| Zlecenia | | | |
| Magazyn części | | | |
| Klienci i urządzenia | | | |

## Kryteria akceptacji
- Cztery wiersze.
- Uzasadnienie odwołuje się do stabilności i dopasowania modelu upstreamu: czy jego model się zmienia, czy pasuje do
  naszego, czy wystarczy nam migawka danych.

## Podpowiedzi
- Kierunek relacji wyznacza to, czyim językiem mówi komunikat, a nie to, kto go fizycznie wysyła.
- Jeśli aplikacja nie jest downstreamem danego modelu (to on słucha jej komunikatów), w kolumnie strategii wpisz
  „–” i w uzasadnieniu napisz, jaki komunikat aplikacja wysyła.
- ACL to „strefa zgniotu”: zmiana cudzego modelu kończy się na jednej klasie tłumaczącej.
- Model, który wystawia jedno API dla wielu klientów, zwykle się nie zmienia pod jednego z nich.

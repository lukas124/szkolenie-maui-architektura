# Ćwiczenie 06 — od wymagań do modelu wizyty

**Blok:** 06 · Logika biznesowa: taktyczne DDD w aplikacji mobilnej · dzień 1 · **Czas:** 21 min (część 1: 5,
część 2: 16) · indywidualnie, bez kodu

Twoja karta z części 2 jest wejściem do ćwiczenia 07 – tam napiszesz swoje obiekty wartości w kodzie.

## Materiał

**Biznes w pięciu zdaniach**
1. Firma serwisuje urządzenia u klientów (pompy ciepła, klimatyzacja). Dyspozytor przyjmuje zgłoszenie klienta i
   zakłada zlecenie (urządzenie, adres, opis usterki, termin SLA).
2. Zlecenie jest realizowane w jednej lub kilku wizytach; dyspozytor planuje wizytę (okno czasowe) i przydziela ją
   technikowi albo brygadzie (dwóch techników, każdy z telefonem); telefony są firmowe i przechodzą między
   technikami – następny nie może widzieć danych poprzedniego.
3. Technik w aplikacji: widzi wizyty na dziś, rozpoczyna wizytę na miejscu (jest w jednym miejscu – w toku najwyżej
   jedna wizyta), zapisuje czynności i zużyte części, robi zdjęcia, pokazuje klientowi protokół (zakres prac, części,
   czas) i zbiera podpis, kończy wizytę z wynikiem: naprawione / potrzebna kolejna wizyta (np. brak części) /
   nie naprawiono.
4. Części są w aucie technika (zapas podręczny) albo w magazynie centralnym – część z magazynu technik rezerwuje i
   odbiera; ostatnią sztukę może chcieć zarezerwować kilku techników naraz.
5. Dyspozytor może wizytę anulować albo przenieść; zlecenie zamyka się, gdy wizyta zakończy się wynikiem
   „naprawione” z podpisem klienta. Technik często pracuje bez zasięgu (piwnice, kotłownie).

**Historie użytkownika**
- Jako technik chcę kończyć wizytę także bez zasięgu i od razu wiedzieć, czego mi brakuje (np. podpisu).
- Jako kierownik serwisu chcę, aby podpis klienta zawsze dotyczył faktycznie wykonanego zakresu prac, a wizyta
  „naprawiona” zawsze miała podpis – także gdy brygada pracuje na dwóch telefonach, a dyspozytor w tym czasie zmienia
  plan.
- Jako dyspozytor chcę anulować albo przenieść wizytę, zanim technik ją zakończy.

## Część 1 — zakreślacze (5 min)
Bez nazw DDD i bez nazw klas – tylko to, co jest w tekście:

| Rzeczowniki | Czynności | Fakty (co się wydarzyło) | Zdania „nie można / musi / tylko gdy” |
|---|---|---|---|
| | | | |

## Część 2 — model wizyty na telefonie (16 min)
1. Każdy rzeczownik z części 1: encja, obiekt wartości, inny model (tylko identyfikator) czy poza modelem – i dlaczego.
2. Komendy (co ktoś chce zrobić) i zdarzenia (co się stało).
3. Reguły: domenowa (wynika z biznesu wizyty) czy aplikacyjna (kto może, kiedy, w jakiej kolejności ekranów)?
4. Granica modelu wizyty: co jest w środku, co poza i dlaczego (które reguły potrzebują tych danych razem?).
5. Dla każdej reguły: telefon / serwer / oba – i co aplikacja robi, gdy serwer odmówi.

| Reguła | Domenowa / aplikacyjna | Telefon / serwer / oba | Reakcja aplikacji na odmowę |
|---|---|---|---|
| | | | |

## Kryteria akceptacji
- Każdy rzeczownik ma przypisaną rolę z uzasadnieniem („tożsamość i historia” albo „liczy się tylko wartość”).
- Co najmniej dwa obiekty wartości uzasadnione tym, że liczy się tylko wartość.
- Granica modelu wizyty wynika z reguł, które muszą być spełnione razem – nie z tego, co „pasuje tematycznie”.
- Każda reguła ma miejsce (telefon / serwer / oba) i reakcję aplikacji na odmowę z serwera.

## Podpowiedzi
- Dwa pytania przy każdym rzeczowniku: czy ma tożsamość i historię? Czy liczy się tylko wartość?
- Przy każdej granicy pytaj o konsekwencję: co trafi na telefon i kto jeszcze będzie zmieniał te dane w tym samym czasie?
- Reguła, do której potrzebne są dane innych użytkowników, nie może być rozstrzygnięta tylko na telefonie.

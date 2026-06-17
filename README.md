# PulseStreamer - Monitor Tętna BLE (WPF)

Aplikacja WPF służąca do monitorowania tętna w czasie rzeczywistym z opasek sportowych i czujników piersiowych Bluetooth LE (np. Polar H10, Garmin HRM) przy użyciu natywnego interfejsu WinRT BLE w systemie Windows.

---

## Główne Funkcje (Po Refaktoryzacji)

### 1. Nowy System Profili Użytkowników
Aplikacja obsługuje teraz pełne zarządzanie wieloma profilami:
* **Zarządzanie profilami**: Możliwość dodawania i usuwania profili za pomocą wygodnego panelu w lewym pasku bocznym. Każdy profil posiada swój okrągły awatar z inicjałem.
* **Niezależne parametry**: Każdy profil przechowuje własny wiek, wagę, płeć, preferowany kolor wykresu, rodzaj aktywności oraz ustawienia Asystenta Treningowego.
* **Automatyczne wiązanie czujnika**: Profil zapamiętuje ostatnio połączoną opaskę BLE i przy włączonej opcji "Połącz automatycznie" łączy się z nią natychmiast po uruchomieniu lub zmianie profilu.

### 2. Dwuetapowy Cykl "Treningu"
* **Podgląd na żywo**: Samo połączenie z czujnikiem pokazuje aktualny puls i strefę na żywo w pasku bocznym oraz w kardiogramie podglądowym. Punkty nie są jeszcze zapisywane do historii ani nie są naliczane kalorie.
* **Rozpocznie Treningu**: Kliknięcie przycisku **Rozpocznij Trening** uruchamia stoper sesji, zliczanie kalorii, zapis próbek oraz asystenta Zone Guard.
* **Zakończenie Treningu i Podsumowanie**: Po kliknięciu **Zakończ Trening** stoper zatrzymuje się, sesja jest zapisywana w katalogu `/sessions` jako plik JSON, a na ekranie pojawia się eleganckie okno podsumowania pokazujące:
  * Całkowity czas treningu.
  * Spalone kalorie (dokładniejszy algorytm oparty na formule Keytela dla HR >= 85 i MET dla HR < 85).
  * Średnie oraz maksymalne tętno.
  * Dokładny czas spędzony w każdej z 5 stref tętna (w sekundach i procentach na wykresach paskowych).

### 3. Inteligentny Asystent Strefowy (Zone Guard)
Zlokalizowany w lewym pasku bocznym panel pozwala na:
1. Włączenie asystenta strefowego.
2. Wybór strefy docelowej (Strefy 1-5).
3. Wybór częstotliwości sprawdzania (np. co 30 sekund).
* **Działanie**: Co określony czas asystent sprawdza, czy tętno użytkownika mieści się w wybranej strefie (progowe wartości obliczane są dynamicznie z wieku profilu). Jeśli tętno jest za niskie, aplikacja asynchronicznie odtwarza dźwięk "Przyśpiesz". Jeśli za wysokie – "Zwolnij".

### 4. Stabilność BLE i "Coast Mode" (Packet Loss Concealment)
* **Eliminacja rozłączeń**: Zaimplementowano globalne referencje do obiektów WinRT, zapobiegając błędnemu rozłączaniu opaski po około minucie przez Garbage Collector.
* **Coast Mode (6 sekund)**: W przypadku chwilowej utraty sygnału lub chwilowego fizycznego rozłączenia opaski, aplikacja płynnie przedłuża wykres przez 6 sekund, utrzymując ostatnie znane tętno i pokazując status "Coast Mode (Słaby sygnał)". Jeśli w tym czasie sygnał powróci, urządzenie kontynuuje pracę. Jeśli nie – następuje oficjalne rozłączenie i zapisanie danych.

### 5. Optymalizacja Wydajności i Dźwięk Asynchroniczny
* **Downsampling wykresu**: Przy renderowaniu bardzo długich treningów historycznych z tysiącami punktów, aplikacja redukuje punkty o odległości mniejszej niż 2 piksele na osi X. Zapewnia to renderowanie Beziera bez obciążenia procesora.
* **Asynchroniczny zapis**: Zapis plików JSON sesji odbywa się w pełni asynchronicznie poza głównym wątkiem interfejsu użytkownika, eliminując jakiekolwiek przycięcia wykresu.
* **WAV Beep**: Dawny, blokujący dźwięk `Console.Beep()` został zastąpiony asynchronicznym odtwarzaczem `SoundPlayer` plików `.wav`, które generowane są automatycznie przy starcie aplikacji w folderze `/sounds`.

### 6. Integracja ze streamowaniem (OBS Studio)
Aplikacja została zaprojektowana z myślą o streamerach pokazujących swoje tętno widzom. Oferuje dwie metody na dodanie tętna do nakładki na żywo:
1. **Pliki Tekstowe (Najprostsza metoda)**: W folderze `obs/` aplikacja stale aktualizuje pliki takie jak `OBS_Bpm.txt`, `OBS_Calories.txt` czy `OBS_Zone.txt`. W OBS wystarczy dodać "Źródło tekstu (GDI+)" i zaznaczyć opcję "Czytaj z pliku".
2. **Serwer HTTP (Dla widżetów webowych)**: Aplikacja wystawia lokalny serwer JSON pod adresem `http://127.0.0.1:8080/api/hr/`, dzięki czemu można łatwo zintegrować tętno ze źródłami przeglądarkowymi (Browser Source).

---

## Konfiguracja i Użycie

1. **Wybierz lub Utwórz Profil**: W panelu "Profile Użytkowników" wybierz profil domyślny lub kliknij **Dodaj**, wpisz imię i wybierz kolor awatara.
2. **Skonfiguruj parametry**: Dostosuj suwaki wieku i wagi oraz wybierz płeć i aktywność (np. VR Fitness, Bieżnia) w panelu "Konfiguracja Wykresu".
3. **Włącz Asystenta (Opcjonalnie)**: W panelu "Asystent Treningowy" zaznacz checkbox, wybierz swoją strefę docelową (np. Cardio) oraz interwał (np. Co 30 sekund).
4. **Połącz Czujnik**: Włącz swój czujnik tętna i kliknij **Skanuj** w górnym panelu. Wybierz go z listy wykrytych opasek. Po połączeniu dioda zmieni kolor na zielony.
5. **Trening**: Kliknij **Rozpocznij Trening** na zakładce "Trening na żywo". Po zakończeniu ćwiczeń kliknij **Zakończ Trening**, aby zobaczyć pełne podsumowanie i zapisać sesję.

---

## Logowanie zdarzeń (Diagnostyka)
Wszystkie kluczowe zdarzenia (skanowanie, błędy połączenia, zapisywanie plików, aktywność asystenta strefowego) są rejestrowane w plikach logu w katalogu aplikacji:
```
\bin\Debug\net9.0-windows10.0.19041.0\logs\app_YYYY-MM-DD.log
```
W razie problemów z połączeniem, zawartość tego pliku pomoże szybko zdiagnozować przyczynę.

# PulseStreamer - Monitor Tętna BLE (WPF)

Aplikacja WPF służąca do monitorowania tętna w czasie rzeczywistym z opasek sportowych i czujników piersiowych Bluetooth LE (np. Polar H10, Garmin HRM) przy użyciu natywnego interfejsu WinRT BLE w systemie Windows.

---

## Główne Funkcje

### 1. System Profili Użytkowników
Aplikacja obsługuje pełne zarządzanie wieloma profilami:
* **Zarządzanie profilami**: Możliwość dodawania i usuwania profili za pomocą wygodnego panelu w lewym pasku bocznym. Każdy profil posiada swój okrągły awatar z inicjałem.
* **Niezależne parametry**: Każdy profil przechowuje własny wiek, wagę, płeć, tętno spoczynkowe (Resting HR), rodzaj aktywności oraz ustawienia Asystenta Treningowego.
* **Automatyczne wiązanie czujnika**: Profil zapamiętuje ostatnio połączoną opaskę BLE i przy włączonej opcji "Połącz automatycznie" łączy się z nią natychmiast po uruchomieniu lub zmianie profilu.

### 2. Dwuetapowy Cykl "Treningu"
* **Podgląd na żywo**: Samo połączenie z czujnikiem pokazuje aktualny puls i strefę na żywo w pasku bocznym oraz w kardiogramie podglądowym. Punkty nie są jeszcze zapisywane do historii ani nie są naliczane kalorie.
* **Rozpocznie Treningu**: Kliknięcie przycisku **Rozpocznij Trening** uruchamia stoper sesji, zliczanie kalorii, zapis próbek oraz asystenta Zone Guard.
* **Zakończenie Treningu i Podsumowanie**: Po kliknięciu **Zakończ Trening** stoper zatrzymuje się, sesja jest zapisywana w katalogu `/sessions` jako plik JSON, a na ekranie pojawia się eleganckie okno podsumowania pokazujące:
  * Całkowity czas treningu.
  * Spalone kalorie z podziałem na trening i efekt afterburn (EPOC).
  * Średnie oraz maksymalne tętno.
  * Dokładny czas spędzony w każdej z 5 stref tętna (w sekundach i procentach na wykresach paskowych).

### 3. Zaawansowany System Kalkulacji Kalorii
Aplikacja wykorzystuje naukowe algorytmy zoptymalizowane pod kątem dokładności i zależności od typu treningu:

* **Formuła Keytela (HR ≥ 85 BPM)**: Naukowo zwalidowana formuła (Keytel et al. 2005, EJAP) z oddzielnymi równaniami dla mężczyzn i kobiet, uwzględniająca tętno, wagę i wiek.
* **Dynamiczny MET (HR < 85 BPM)**: Zamiast stałych wartości MET, system oblicza wartość dynamicznie na bazie **Heart Rate Reserve (HRR)** — skaluje MET między wartością minimalną a maksymalną dla danej aktywności, dopasowując się do rzeczywistej intensywności użytkownika.
* **Wartości MET z Compendium 2024**: Zakresy MET zaktualizowane do najnowszego Compendium of Physical Activities (Ainsworth et al. 2024).
* **Formuła Tanaka (Max HR)**: Dokładniejsza formuła `208 - 0.7 × wiek` zamiast przestarzałej `220 - wiek` (Tanaka et al. 2001, JACC).
* **Współczynniki aktywności**: Specyficzne korekty Keytela dla różnych typów ćwiczeń (bieżnia, rower, boks, VR Fitness, aerobik).
* **Tętno spoczynkowe**: Konfigurowalny parametr profilu używany w obliczeniach HRR dla precyzyjniejszego dynamicznego MET.
* **Estymacja EPOC (Afterburn)**: Po zakończeniu treningu system oblicza dodatkowe spalanie kalorii po wysiłku (Excess Post-Exercise Oxygen Consumption), zależne od intensywności (3-15%) i czasu trwania treningu. Wyświetlane w dedykowanej karcie w podsumowaniu.

### 4. Inteligentny Asystent Strefowy (Zone Guard)
Zlokalizowany w lewym pasku bocznym panel pozwala na:
1. Włączenie asystenta strefowego.
2. Wybór strefy docelowej (Strefy 1-5).
3. Wybór częstotliwości sprawdzania (np. co 30 sekund).
* **Działanie**: Co określony czas asystent sprawdza, czy tętno użytkownika mieści się w wybranej strefie (progowe wartości obliczane dynamicznie z formuły Tanaka). Jeśli tętno jest za niskie, aplikacja asynchronicznie odtwarza dźwięk "Przyśpiesz". Jeśli za wysokie – "Zwolnij".

### 5. Stabilność BLE i "Coast Mode" (Packet Loss Concealment)
* **Eliminacja rozłączeń**: Zaimplementowano globalne referencje do obiektów WinRT, zapobiegając błędnemu rozłączaniu opaski po około minucie przez Garbage Collector.
* **Coast Mode (6 sekund)**: W przypadku chwilowej utraty sygnału, aplikacja płynnie przedłuża wykres przez 6 sekund, utrzymując ostatnie znane tętno i pokazując status "Coast Mode (Słaby sygnał)". Jeśli w tym czasie sygnał powróci, urządzenie kontynuuje pracę.

### 6. Optymalizacja Wydajności i Dźwięk Asynchroniczny
* **Downsampling wykresu**: Przy renderowaniu bardzo długich treningów historycznych, aplikacja redukuje punkty o odległości mniejszej niż 2 piksele na osi X.
* **Asynchroniczny zapis**: Zapis plików JSON sesji odbywa się w pełni asynchronicznie poza głównym wątkiem UI.
* **WAV Beep**: Asynchroniczny odtwarzacz `SoundPlayer` plików `.wav`, generowanych automatycznie przy starcie aplikacji.

### 7. Integracja ze streamowaniem (OBS Studio)
Aplikacja została zaprojektowana z myślą o streamerach:
1. **Pliki Tekstowe**: W folderze `obs/` aplikacja aktualizuje pliki `OBS_Bpm.txt`, `OBS_Calories.txt`, `OBS_Zone.txt`.
2. **Serwer HTTP**: Lokalny serwer JSON pod adresem `http://127.0.0.1:8080/api/hr/` dla źródeł przeglądarkowych (Browser Source) w OBS.

---

## Konfiguracja i Użycie

1. **Wybierz lub Utwórz Profil**: W panelu "Profile Użytkowników" wybierz profil domyślny lub kliknij **Dodaj**.
2. **Skonfiguruj parametry**: Dostosuj suwaki wieku, wagi i tętna spoczynkowego oraz wybierz płeć i aktywność (np. VR Fitness, Bieżnia) w panelu "Dane Użytkownika".
3. **Włącz Asystenta (Opcjonalnie)**: W panelu "Asystent Strefy" zaznacz toggle, wybierz strefę docelową i interwał.
4. **Połącz Czujnik**: Włącz swój czujnik tętna i kliknij **Skanuj**. Wybierz go z listy wykrytych opasek.
5. **Trening**: Kliknij **Rozpocznij Trening**. Po zakończeniu kliknij **Zakończ Trening**, aby zobaczyć podsumowanie z kaloriami, EPOC i strefami.

---

## Logowanie zdarzeń (Diagnostyka)
Wszystkie kluczowe zdarzenia (skanowanie, błędy połączenia, zapisywanie plików, aktywność asystenta strefowego) są rejestrowane w plikach logu w katalogu aplikacji:
```
\bin\Debug\net9.0-windows10.0.19041.0\logs\app_YYYY-MM-DD.log
```
W razie problemów z połączeniem, zawartość tego pliku pomoże szybko zdiagnozować przyczynę.

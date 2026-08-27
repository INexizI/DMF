# DMF – Подробное руководство пользователя

**DMF (Desktop Media Formatter)** — это графическая оболочка для FFmpeg, предназначенная для быстрого перекодирования, обрезки и создания GIF-анимаций из видео- и аудиофайлов. Программа ориентирована на пользователей Windows, которые хотят управлять FFmpeg без командной строки.

---

## 1. Требования и установка

### Системные требования

- **ОС**: Windows 10 / 11 (x64)
- **FFmpeg** (включая `ffprobe`) — должен быть установлен и доступен в `PATH`, либо вы можете указать путь к исполняемым файлам вручную.
- **.NET Runtime** — не требуется, так как приложение поставляется как self-contained `.exe`.

### Установка

1. Скачайте последний релиз с [страницы релизов](https://github.com/INexizI/DMF/releases).
2. Распакуйте архив в любую папку.
3. Запустите `DMF.exe`.

При первом запуске программа создаст папку `%LOCALAPPDATA%\DMF`, где будут храниться:

- `settings.json` — настройки окна, пути к FFmpeg/ffprobe, опции.
- `settings.json.bak` — резервная копия настроек (создаётся автоматически при сохранении).
- `log.txt` — журнал событий (полезен для отладки).
- `update_cache.json` — кэш проверки обновлений.

---

## 2. Обзор интерфейса

Окно программы состоит из **вкладок** (TabControl), каждая из которых отвечает за определённую группу параметров.

### Вкладка **Basic** (Основные)

- **Input** — путь к исходному файлу (можно перетащить файл мышью).
- **Output** — путь к выходному файлу (автоматически подставляется, если выбран Input).
- **Format** — контейнер выходного файла (список обновляется при переключении флажка _Audio only_).
- **Trim mode** — `Source` (без обрезки) или `Range` (задать начальное и конечное время в формате `HH:MM:SS`).
- **Video codec** / **Audio codec** — выбор кодека. По умолчанию `copy` (без перекодирования, максимальная скорость). При выборе другого кодека активируются соответствующие настройки на вкладках Video/Audio.
- **Чекбоксы**:
  - `Audio only` — отключает видео, создаёт только аудиофайл.
  - `Overwrite` — перезаписывать выходной файл, если он существует.
  - `Open folder on success` — после завершения открывает папку с результатом в Проводнике.

### Вкладка **Presets** (Готовые настройки)

Содержит предустановленные профили:

- **Web** — H.264/AAC, MP4, CRF 23, medium, для веб-публикации.
- **HD** — высокое качество, H.264, CRF 18, slow.
- **Mobile** — оптимизация для мобильных устройств (базовый профиль, низкий битрейт).
- **Lossless** — H.264 lossless (CRF 0), FLAC, MKV, 4:4:4.
- **2K / 4K** — настройки для соответствующего разрешения с H.264/H.265 и целевым битрейтом.
- **GIF** — быстро переключает формат на `gif` и заполняет параметры GIF-вкладки (FPS=30, масштаб 640 px, палитра + дизеринг).

При нажатии **Apply Preset** выбранный профиль применяется ко всем вкладкам.

### Вкладка **Video** (Параметры видео)

Доступны при выборе видеокодека, отличного от `copy`:

- **CRF** — качество (0–51; чем ниже, тем лучше). Для аппаратных кодеков (NVENC, AMF, QSV) это значение интерпретируется как `-cq` (NVENC) или `-qp` (AMF/QSV).
- **Preset** — скорость кодирования (ultrafast … veryslow). Влияет на размер файла и качество.
- **Pixel format** — битность и цветовая субдискретизация (8/10/12 бит, 4:2:0/4:2:2/4:4:4). Для HDR (BT.2020) требуется 10-битный формат (например, `yuv420p10le`).
- **Bitrate (v)** — целевой видеобитрейт (например, `1500k`, `2M`). Если задан, то CRF игнорируется.
- **Maxrate / Buffer size** — параметры для VBR-кодирования.
- **Profile** — профиль совместимости (например, `baseline` для мобильных, `high` для HD, `main10` для H.265 10-бит).
- **GOP size** — расстояние между ключевыми кадрами (0 = авто).
- **Output FPS** — принудительная частота кадров на выходе (0 = сохранить исходную).
- **Color matrix** — цветовое пространство (BT.709, BT.2020 и др.) – записывается в метаданные.
- **Color range** — диапазон яркости (limited – для ТВ, full – для ПК).

### Вкладка **Audio** (Параметры аудио)

- **Bitrate** — целевой аудиобитрейт (например, `128k`).
- **Audio quality** — параметр VBR (0–10; ниже = лучше качество, но больше размер). Применяется не для всех кодеков.

### Вкладка **Filters** (Фильтры)

- **Video filter** — произвольная строка фильтров FFmpeg (например, `scale=1280:-2, crop=1920:1080:0:0, hflip`). Если фильтр активен, а кодек выбран `copy`, программа автоматически переключит видеокодек на `libx264` (т.к. фильтры требуют перекодирования).
- **Audio filter** — аналогично для звука (например, `volume=2, afade=in:0:5`).

В поле подсказки приведены примеры часто используемых фильтров.

### Вкладка **Subtitles** (Субтитры)

- Включение субтитров (`Add subtitles`).
- Источник: из входного файла (выбор дорожки по номеру) или внешний файл (SRT, ASS, VTT).
- `Copy subtitles (no re-encode)` — если выключено, субтитры будут перекодированы (обычно не требуется).

### Вкладка **Advanced** (Дополнительно)

- **Map streams** — позволяет вручную сопоставить потоки (например, `0:v:0 0:a:1`). Используется для выбора конкретных потоков.
- **HW Accel** — аппаратное ускорение декодирования (CUDA, VAAPI, QSV, D3D11VA, Vulkan). Для работы с аппаратными кодерами выбирайте соответствующий кодек во вкладке Basic.

### Вкладка **GIF** (Настройки GIF)

Доступна только при выборе формата `gif`:

- **Output FPS** — частота кадров результирующего GIF.
- **Scale** — ширина и высота (0 = автоматически, с сохранением пропорций).
- **Crop** — обрезка в формате `w:h:x:y` (например, `640:480:0:0`). При включённом предпросмотре область обрезки отображается красной рамкой.
- **Palette** — генерация оптимальной палитры (рекомендовано).
- **Dithering** — алгоритм дизеринга (Bayer, Floyd‑Steinberg и др.). Для Bayer доступна настройка `Bayer scale` (0–5).
- **Open Preview** / **Update Preview** — показывает первый кадр с наложенной областью обрезки (обновляется при изменении параметров).

### Вкладка **Info** (Информация о системе)

Здесь отображаются:

- Версия DMF и установленных FFmpeg/ffprobe.
- Пути к исполняемым файлам, настройкам и логу.
- Версии .NET и ОС.
- Кнопки **Copy info** (копирует все данные в буфер обмена) и **Open data folder** (открывает папку с настройками).

---

## 3. Пошаговая инструкция по конвертации

1. **Выберите входной файл**:
   - Нажмите **Browse...** рядом с полем Input или перетащите файл мышью в это поле.
2. **Укажите выходной файл**:
   - Автоматически будет предложен путь в папке «Загрузки» с именем `output-<имя_исходного>.mp4`. Вы можете изменить его через **Browse...**.
3. **Настройте основные параметры** (формат, кодек, обрезку).
4. **При необходимости** переключитесь на вкладки Video/Audio/Filters, чтобы задать дополнительные параметры.
5. **Для GIF** выберите формат `gif` и настройте параметры во вкладке GIF. Рекомендуется использовать предпросмотр (кнопка **Open Preview**), чтобы убедиться, что обрезка и масштаб выбраны правильно.
6. **Нажмите Run FFmpeg**. Процесс начнётся, прогресс будет отображаться в процентах. Вы можете отменить операцию кнопкой **Cancel**.
7. По завершении откроется папка с выходным файлом (если включён соответствующий чекбокс).

---

## 4. Советы по выбору параметров

- **Если вы не знаете, что выбрать** — оставьте кодек `copy` (без перекодирования). Тогда файл будет скопирован «как есть», только возможно обрезан по времени (если включён Trim mode). Качество не пострадает, а скорость максимальная.
- **Для уменьшения размера файла** выберите видеокодек `libx264` или `libx265` (HEVC), установите CRF 23–28 (чем выше, тем сильнее сжатие). Для аудио — `aac` с битрейтом 128k.
- **Для максимального качества** используйте `libx264` с CRF 18 или `libx265` с CRF 20, а также `flac` для аудио.
- **Аппаратное ускорение** (NVENC, AMF, QSV) даёт высокую скорость кодирования, но качество может быть немного ниже, чем у программных кодеков при том же битрейте. Для аппаратных кодеков используйте параметр CRF как управление качеством (он будет транслироваться в `-cq` или `-qp`).
- **BT.2020 и HDR** требуют 10-битного пиксельного формата (например, `yuv420p10le`) и соответствующего цветового матрикса. Убедитесь, что ваш плеер поддерживает HDR.

---

## 5. Устранение неполадок

- **FFmpeg не найден** — если вы видите сообщение об ошибке, установите FFmpeg с официального сайта и добавьте его в `PATH`, либо нажмите **Change** на вкладке Info и укажите путь к `ffmpeg.exe` вручную.
- **Ошибка при кодировании** — прочитайте сообщение в диалоговом окне. Оно содержит конкретную причину (неподдерживаемый кодек, неверный параметр фильтра, проблемы с доступом к файлу и т.д.). Полный лог доступен в `%LOCALAPPDATA%\DMF\log.txt`.
- **Предпросмотр не обновляется** — убедитесь, что входной файл существует и содержит видеопоток. Некоторые форматы (например, MP3) не поддерживают предпросмотр.
- **Субтитры не отображаются** — проверьте, что выбран правильный трек (номер начинается с 0). Для внешних субтитров убедитесь, что файл существует и имеет поддерживаемое расширение (SRT, ASS, VTT).
- **GIF получается слишком большим** — уменьшите FPS, масштаб или используйте более агрессивный дизеринг. Включите палитру (она уменьшает размер за счёт оптимизации цветов).

---

## 6. Где хранятся настройки и логи?

Все файлы находятся в `%LOCALAPPDATA%\DMF`. Вы можете быстро открыть эту папку через кнопку **Open data folder** на вкладке Info.

- `settings.json` — текущие настройки.
- `settings.json.bak` — резервная копия (восстанавливается автоматически при повреждении основного файла).
- `log.txt` — журнал событий.
- `update_cache.json` — кэш проверки обновлений (не редактируйте вручную).

---

## 7. Автоматическое обновление

При запуске программа проверяет наличие новой версии на GitHub (не чаще раза в час). Если обновление найдено, вы получите уведомление и сможете перейти на страницу релизов для скачивания.

---

## 8. Лицензия

DMF распространяется под лицензией MIT. Исходный код доступен на [GitHub](https://github.com/INexizI/DMF).

---

_Документация актуальна для версии 0.6.3 и выше._

---

# English Version

## DMF – Detailed User Guide

**DMF (Desktop Media Formatter)** is a graphical front‑end for FFmpeg, designed for quick transcoding, trimming, and creating GIF animations from video and audio files. The program is aimed at Windows users who want to control FFmpeg without the command line.

---

## 1. Requirements and Installation

### System Requirements

- **OS**: Windows 10 / 11 (x64)
- **FFmpeg** (including `ffprobe`) — must be installed and available in `PATH`, or you can specify the path to the executables manually.
- **.NET Runtime** — not required, as the application is shipped as a self‑contained `.exe`.

### Installation

1. Download the latest release archive `DMF-<version>-win-x64.zip` from the [Releases page](https://github.com/INexizI/DMF/releases).
2. Extract the archive to any folder.
3. Run `DMF.exe`.

On first launch, the program creates the folder `%LOCALAPPDATA%\DMF`, which stores:

- `settings.json` — window settings, FFmpeg/ffprobe paths, options.
- `settings.json.bak` — backup of settings (created automatically on save).
- `log.txt` — event log (useful for debugging).
- `update_cache.json` — update check cache.

---

## 2. Interface Overview

The program window consists of **tabs** (TabControl), each responsible for a specific group of parameters.

### **Basic** Tab

- **Input** — path to the source file (you can drag and drop a file).
- **Output** — path to the output file (automatically filled when Input is selected).
- **Format** — container of the output file (the list updates when the _Audio only_ checkbox is toggled).
- **Trim mode** — `Source` (no trimming) or `Range` (set start and end time in `HH:MM:SS` format).
- **Video codec** / **Audio codec** — codec selection. Default is `copy` (no re‑encoding, maximum speed). When another codec is chosen, the corresponding settings on the Video/Audio tabs become active.
- **Checkboxes**:
  - `Audio only` — disables video, creates only an audio file.
  - `Overwrite` — overwrite the output file if it exists.
  - `Open folder on success` — opens the output folder in File Explorer after completion.

### **Presets** Tab

Contains predefined profiles:

- **Web** — H.264/AAC, MP4, CRF 23, medium, for web publishing.
- **HD** — high quality, H.264, CRF 18, slow.
- **Mobile** — optimised for mobile devices (baseline profile, low bitrate).
- **Lossless** — H.264 lossless (CRF 0), FLAC, MKV, 4:4:4.
- **2K / 4K** — settings for the respective resolution with H.264/H.265 and target bitrate.
- **GIF** — switches format to `gif` and fills the GIF tab parameters (FPS=30, scale 640 px, palette + dithering).

Clicking **Apply Preset** applies the selected profile to all tabs.

### **Video** Tab

Available when a video codec other than `copy` is selected:

- **CRF** — quality (0–51; lower is better). For hardware codecs (NVENC, AMF, QSV), this value is interpreted as `-cq` (NVENC) or `-qp` (AMF/QSV).
- **Preset** — encoding speed (ultrafast … veryslow). Affects file size and quality.
- **Pixel format** — bit depth and chroma subsampling (8/10/12 bit, 4:2:0/4:2:2/4:4:4). HDR (BT.2020) requires a 10‑bit format (e.g., `yuv420p10le`).
- **Bitrate (v)** — target video bitrate (e.g., `1500k`, `2M`). If set, CRF is ignored.
- **Maxrate / Buffer size** — parameters for VBR encoding.
- **Profile** — compatibility profile (e.g., `baseline` for mobile, `high` for HD, `main10` for H.265 10‑bit).
- **GOP size** — distance between keyframes (0 = auto).
- **Output FPS** — forced output frame rate (0 = keep source).
- **Color matrix** — colour space (BT.709, BT.2020, etc.) – written to metadata.
- **Color range** — luma range (limited – for TV, full – for PC).

### **Audio** Tab

- **Bitrate** — target audio bitrate (e.g., `128k`).
- **Audio quality** — VBR parameter (0–10; lower = better quality but larger size). Not all codecs support it.

### **Filters** Tab

- **Video filter** — arbitrary FFmpeg filter string (e.g., `scale=1280:-2, crop=1920:1080:0:0, hflip`). If a filter is active and the codec is `copy`, the program automatically switches the video codec to `libx264` (since filters require re‑encoding).
- **Audio filter** — similarly for audio (e.g., `volume=2, afade=in:0:5`).

The hint field provides examples of commonly used filters.

### **Subtitles** Tab

- Enable subtitles (`Add subtitles`).
- Source: from the input file (select track number) or an external file (SRT, ASS, VTT).
- `Copy subtitles (no re-encode)` — if unchecked, subtitles will be re‑encoded (usually not needed).

### **Advanced** Tab

- **Map streams** — allows manual stream mapping (e.g., `0:v:0 0:a:1`). Used to select specific streams.
- **HW Accel** — hardware decoding acceleration (CUDA, VAAPI, QSV, D3D11VA, Vulkan). For hardware encoders, choose the corresponding codec in the Basic tab.

### **GIF** Tab

Available only when the format `gif` is selected:

- **Output FPS** — frame rate of the resulting GIF.
- **Scale** — width and height (0 = automatic, preserving aspect ratio).
- **Crop** — cropping in the format `w:h:x:y` (e.g., `640:480:0:0`). When preview is enabled, the crop area is displayed with a red rectangle.
- **Palette** — generate an optimal palette (recommended).
- **Dithering** — dithering algorithm (Bayer, Floyd‑Steinberg, etc.). For Bayer, the `Bayer scale` (0–5) is available.
- **Open Preview** / **Update Preview** — shows the first frame with the crop area overlaid (updates when parameters change).

### **Info** Tab

Displays:

- DMF version and installed FFmpeg/ffprobe versions.
- Paths to executables, settings, and log.
- .NET and OS versions.
- Buttons **Copy info** (copies all data to clipboard) and **Open data folder** (opens the settings folder).

---

## 3. Step‑by‑Step Conversion Guide

1. **Select the input file**:
   - Click **Browse...** next to the Input field or drag and drop a file onto the field.
2. **Specify the output file**:
   - A default path in the Downloads folder with the name `output-<original_name>.mp4` will be suggested. You can change it via **Browse...**.
3. **Configure basic parameters** (format, codec, trimming).
4. **If needed**, switch to the Video/Audio/Filters tabs to set additional options.
5. **For GIF**, select the `gif` format and adjust the parameters in the GIF tab. It is recommended to use the preview (**Open Preview**) to ensure cropping and scaling are correct.
6. **Click Run FFmpeg**. The process will start, and progress will be shown as a percentage. You can cancel the operation with the **Cancel** button.
7. Upon completion, the output folder will open (if the corresponding checkbox is enabled).

---

## 4. Tips for Choosing Parameters

- **If you are unsure** — leave the codec as `copy` (no re‑encoding). The file will be copied as‑is, only trimmed if Trim mode is enabled. Quality is preserved and speed is maximal.
- **To reduce file size** — choose video codec `libx264` or `libx265` (HEVC), set CRF 23–28 (higher = more compression). For audio, use `aac` with bitrate 128k.
- **For maximum quality** — use `libx264` with CRF 18 or `libx265` with CRF 20, and `flac` for audio.
- **Hardware acceleration** (NVENC, AMF, QSV) gives high encoding speed, but quality may be slightly lower than software codecs at the same bitrate. For hardware codecs, use the CRF parameter as a quality control (it will be translated to `-cq` or `-qp`).
- **BT.2020 and HDR** require a 10‑bit pixel format (e.g., `yuv420p10le`) and the appropriate colour matrix. Ensure your player supports HDR.

---

## 5. Troubleshooting

- **FFmpeg not found** — if you see an error message, install FFmpeg from the official site and add it to `PATH`, or click **Change** on the Info tab and manually point to `ffmpeg.exe`.
- **Encoding error** — read the message in the dialog box. It contains the specific cause (unsupported codec, invalid filter parameter, file access issues, etc.). The full log is available in `%LOCALAPPDATA%\DMF\log.txt`.
- **Preview does not update** — make sure the input file exists and contains a video stream. Some formats (e.g., MP3) do not support preview.
- **Subtitles are not displayed** — check that the correct track is selected (numbering starts from 0). For external subtitles, ensure the file exists and has a supported extension (SRT, ASS, VTT).
- **GIF is too large** — reduce FPS, scale, or use more aggressive dithering. Enable palette generation (it reduces size by optimising colours).

---

## 6. Where Are Settings and Logs Stored?

All files are located in `%LOCALAPPDATA%\DMF`. You can quickly open this folder via the **Open data folder** button on the Info tab.

- `settings.json` — current settings.
- `settings.json.bak` — backup (restored automatically if the main file is corrupted).
- `log.txt` — event log.
- `update_cache.json` — update cache (do not edit manually).

---

## 7. Automatic Updates

On startup, the program checks for a new version on GitHub (no more than once an hour). If an update is found, you will receive a notification and can go to the releases page to download it.

---

## 8. License

DMF is distributed under the MIT License. Source code is available on [GitHub](https://github.com/INexizI/DMF).

---

_Documentation is valid for version 0.6.3 and above._

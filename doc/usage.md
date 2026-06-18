# DMF User Guide

## Overview

DMF is a graphical front-end to FFmpeg that focuses on **trimming** and **transcoding** media files. It avoids common pitfalls by placing trimming options _before_ the input file, which is recommended for accurate seeking and avoiding black frames.

## Main Interface

<!-- ![DMF interface](screenshot.png) _(you can add a screenshot later)_ -->

The window is divided into these areas:

- **Input file** - click _Browse..._ to select the file you want to process.
- **Output file** - click _Browse..._ to set where the resulting file will be saved.
- **Trim mode** - choose how you want to trim the file:
  - `None (full file)` - no trimming; the whole file is processed.
  - `Duration` - you specify a **start time** and a **duration**. The output will contain the segment from `start` to `start + duration`.
  - `End time` - you specify a **start time** and an **end time**. The output will contain the segment from `start` to `end` (FFmpeg's `-to` is exclusive of the end time).
- **Start time** - in `HH:MM:SS` format (e.g., `00:01:30` for 1 minute 30 seconds).
- **Duration** (visible when _Duration_ mode is selected) - in `HH:MM:SS` format.
- **End time** (visible when _End time_ mode is selected) - in `HH:MM:SS` format.
- **Audio codec** - drop-down with:
  - `copy` (default) - no re-encoding, lossless, fastest.
  - `aac`, `mp3`, `libmp3lame`, `ac3`, `flac` - re-encode audio.
- **Video codec** - drop-down with:
  - `copy` (default) - no re-encoding, lossless, fastest.
  - `libx264`, `libx265`, `libvpx-vp9`, `mpeg4` - re-encode video.
- **Run FFmpeg** - starts the processing.
- **Status label** - shows current state (`Ready`, `Processing...`, `Done!`, `Error`).
- **Progress bar** - shown while processing (marquee style).

## The `copy` option - why you should use it

- **What it does**: `copy` tells FFmpeg to copy the audio/video streams **as-is** without any re-encoding.
- **Advantages**:
  - **Zero quality loss** - the output is identical to the input.
  - **Extremely fast** - only data is copied, no CPU-heavy encoding.
  - **Perfect for trimming** - when you only cut a segment, `copy` is the best choice.
- **When not to use `copy`**: If you need to change the container format, reduce file size, or convert to a different codec that your device supports, choose a specific codec instead.

## FFmpeg Command Construction

When you click _Run FFmpeg_, the application builds a command like this:

### Full file (no trim) with `copy` for both:

```bash
ffmpeg -i "input.mp4" -c:a copy -c:v copy "output.mp4"
```

### Trim with duration (start at 5s, duration 10s) using `copy`:

```bash
ffmpeg -ss 00:00:05 -t 00:00:10 -i "input.mp4" -c:a copy -c:v copy "output.mp4"
```

### Trim with end time (start at 5s, end at 30s) using `copy`:

```bash
ffmpeg -ss 00:00:05 -to 00:00:30 -i "input.mp4" -c:a copy -c:v copy "output.mp4"
```

### With re-encoding (audio to AAC, video to H.264):

```bash
ffmpeg -ss 00:00:05 -t 00:00:10 -i "input.mp4" -c:a aac -c:v libx264 "output.mp4"
```

**Important**: All trimming options (`-ss`, `-t`, `-to`) are placed **before** `-i`. This is critical for:

- **Accurate seeking** - FFmpeg will use keyframe-accurate seeking.
- **Avoiding black frames** - when placed after `-i`, FFmpeg may decode frames but not output them correctly, leading to black frames at the start.

## Tips

- **Use `copy` for both codecs** when you only want to trim or remux - it's the fastest and lossless option.
- **Choose a codec** if you need to change the format or reduce file size - but be aware that re-encoding takes time and loses quality.
- **Start time can be `00:00:00`** - if you want to start from the beginning.
- **Duration must be greater than zero**.
- **End time must be greater than start time**.

## Troubleshooting

- **FFmpeg not found** - ensure FFmpeg is installed and added to your system `PATH`. Alternatively, edit the code and replace `"ffmpeg"` with the full path to the executable.
- **File not supported** - check that your input file format is supported by FFmpeg.
- **Output file extension** - use a proper extension (e.g., `.mp4`, `.mkv`) that matches the codec container.

## Limitations

- The current version does **not** support multiple input files or complex filter graphs.
- It does **not** show real-time progress percentage (only an indeterminate progress bar).
- All processing is done in a separate thread; you can queue multiple jobs by re-running the process after completion.

## Future Enhancements

- Real-time progress output (parse FFmpeg's `-progress` option).
- Save/load recent files and codec selections.
- Batch processing (multiple files).
- Audio/video filter options (e.g., scaling, bitrate).

---

_For any bugs or feature requests, please open an issue on the GitHub repository._

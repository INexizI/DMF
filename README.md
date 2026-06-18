# DMF - Desktop Media Form

A lightweight Windows Forms GUI for **FFmpeg** that simplifies trimming and transcoding of media files.

## Features

- **Trim videos** by selecting:
  - No trimming (full file)
  - Duration (start + duration)
  - End time (start + end timestamp)
- **Choose audio and video codecs** - default is `copy` (no re-encoding, lossless and fastest).
- **FFmpeg arguments are correctly ordered** (`-ss` and `-t`/`-to` **before** `-i`) to avoid black-frame issues and ensure accurate seeking.
- **Persistent window settings** - remembers size, position, and maximised state.
- **Asynchronous processing** - UI stays responsive while FFmpeg runs.
- **Clean and simple interface** - built with .NET Windows Forms.

## Why `copy`?

- **`copy`** tells FFmpeg to copy the audio/video streams **without re-encoding**.
- This is **lossless** (no quality loss) and **extremely fast** - ideal for trimming, cutting, or remuxing.
- Only use other codecs if you need to change the format or compress the file.

## Prerequisites

- [.NET SDK 6.0 or later](https://dotnet.microsoft.com/en-us/download) (to build and run)
- [FFmpeg](https://ffmpeg.org/download.html) installed and accessible in your `PATH` (or you can modify the code to use a full path).

## Installation

1. Clone this repository:
   ```bash
   git clone https://github.com/INexizI/DMF.git
   ```
2. Build the project:

   ```bash
   cd DMF
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

Or open the solution in Visual Studio and build/run from there.

## Usage

- Select an input media file (click Browse...).

- Choose an output file (click Browse...).

- Set the trim mode:
  - Source - process the entire file.

  - Duration - specify a start time and a duration.

  - End time - specify a start time and an end time.

- Select audio and video codecs - the default is copy for both (fastest, lossless). Change only if you need to re-encode.

- Click Run FFmpeg.

- The progress bar will show activity; a message box will confirm success or show errors.

For detailed usage and examples, see the [user guide](doc/usage.md)

## Building from Source

```bash
dotnet restore
dotnet build --configuration Release
```

The executable will be in bin/Release/net6.0-windows/ (or your target framework).

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgements

FFmpeg - the backbone of this tool.

.NET and Windows Forms teams for the platform.

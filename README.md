# Videofy

![Videofy app preview](images/app.png)

Videofy is a cross-platform desktop application for batch video compression with quality control.

Videofy does not implement or distribute its own codecs. All video processing is performed by FFmpeg.

## Features

- Batch compression of multiple video files
- Cross-platform support (Windows and Linux)
- MacOS support coming soon!

## FFmpeg Dependency and Licensing

Videofy relies entirely on FFmpeg for video decoding and encoding.  
The application itself is a graphical frontend and does not contain any video codec implementations.

### Distribution Details

- The Windows release ships as a standalone `.exe` and invokes FFmpeg as an external process.
- Linux releases include a separate FFmpeg binary for convenience.
- FFmpeg remains a distinct component and is not statically linked to Vidoefy.

FFmpeg is licensed under the LGPL/GPL depending on how it is built.  
Users are responsible for ensuring that their use of FFmpeg complies with all applicable licenses and local regulations.

Videofy is not affiliated with the FFmpeg project.

## How It Works

Videofy re-encodes videos using FFmpeg with CRF (Constant Rate Factor) settings.  
Lower CRF values produce higher quality and larger files, while higher values reduce file size at the cost of quality. This approach provides consistent visual results across different source videos.

## Requirements

- Windows: no external dependencies beyond the provided FFmpeg binary
- Linux: FFmpeg binary included in the release package
- .NET runtime compatible with the current release of 9.0

## Installation

1. Download the latest release from the GitHub Releases page.
2. Extract the archive.
3. Launch Videofy.

## Usage

1. Open a folder containing video files.
2. Select the files you want to compress.
3. Adjust the quality slider (CRF).
4. Start compression and monitor progress.
5. Output files are generated according to the configured behavior alongside the original videos.

## Supported Formats

Videofy supports all video formats supported by the bundled FFmpeg build, including but not limited to:

- MP4
- MKV
- AVI
- MOV
- WEBM

## Contributing

Source code will be available on GitHub once the initial release for macOS is made.
Contributions are welcome. Please open an issue or submit a pull request for bug fixes, improvements, or new features.

## License

Videofy is licensed under the MIT License.

FFmpeg is licensed separately under the LGPL/GPL.  
See the FFmpeg project for full licensing details.

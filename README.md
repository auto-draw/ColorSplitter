# Color Splitter

## Overview
Color Splitter, is a tool designed split any image you input, into seperate image.
This software takes an input image, quantizes it down to best fit, and then seperates each image out into Lossless PNG into whatever folder you chose.
The use case of this application primarily focuses on AutoDraw, and other similar applications.
This application can also be used in other fields of use, such as color analysis, palette extraction, and general use in image processing amonst other tools.

## TODO:
We plan on switching out the Algorithm to includem multiple algorithms for Quantization, to provide a more visually accurate and more capable set of algorithms for greater potential.
Current algorithms to be added:
  Wu's Algorithm - Exceptional Visual Accuracy, mediocore performance
  Spatial Color - Beyond Exceptional < 16 Color Accuracy, however atrocious performance
In addition, we may add:
  NeuQuant Neural Network - Beyond Exceptional Color Accuracy - good performance, however pain to implement.

## Installation
To install and run Color Splitter, you can find a downloadable binary on the right on Releases.

## Building
You can either build via your choice of an IDE (Suggested JetBrains Rider or Visual Studio), or via our Release Build Script


**Usage of Release Build Script.**

### Prequisites:
[.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
Any bash supporting terminal (Any Linux Terminal, Powershell, Git Bash, etc)

**Clone the repository:**
```bash
git clone https://github.com/auto-draw/csp.git
```

**Run the build script:**
```bash
./build.sh
```

You'll be greeted with the following text, choose whichever operating system you want.
```
Building for release.
Please choose which files to build:
[1] Windows x64
[2] Windows x86
[3] Windows Selfcontained-x64
[4] MacOS
[5] Linux
[6] All
Please enter your choice:
```

Once you've chosen your target operating system, it will generate a build file into /Builds/ as a .zip file.

## License
This project is licensed under the MIT License. See the LICENSE file for details.

## Contact
For any questions or suggestions, feel free to contact AlexDalas or Siydge on our [Discord](https://discord.gg/2rvVtb32qA).

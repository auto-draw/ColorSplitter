# Color Splitter

## Overview
Color Splitter, is a tool designed split any image you input, into separate image.
This software takes an input image, quantize's it down to best fit, and then separates each image out into Lossless PNG into whatever folder you chose.
The use case of this application primarily focuses on AutoDraw, and other similar applications.
This application can also be used in other fields of use, such as color analysis, palette extraction, and general use in image processing, amongst other tools.

*Application icon is provided by [fatcow](http://www.fatcow.com/free-icons) under CC Attribution 3.0*

### Lets compare:

Please note, **no color space is perfect** for one job, just as much as **no algorithm is perfect**, so play around with them when quantizing images, you may find better results that way.

### Available Algorithms
| Quantizer  | Initializer | Overview                                                                                                                                                                                               |
|------------|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| K-Means    |             | Great for accurate results, however very slow processing speed.<br/>Iterations feature allows for user control on color accuracy<br/>Useful to maintain image stylization in some cases.               |
|            | K-Means++   | Fast initialization, however results are random<br/>Can suffer at low color-counts.<br>Good for random color-tables for cheap                                                                          |
|            | Median Cut  | Combines Median Cut's accurate color table abilities<br/>with K-Means. This results in color accurate and predictable results.<br/>Great for accurate results, however its compute intensive and slow. |
| Median Cut |             | Abysmally fast, but not very accurate. Good for quick rough results.                                                                                                                                   |

### Colorspaces
| Color Space | Overview                                                                                                                      |
|-------------|-------------------------------------------------------------------------------------------------------------------------------|
| sRGB        | General Use Case, however outdated,<br/> can look inaccurate at times.<br/>Much more dark preferential.                       |
| Oklab       | Great for colorful scenes, suffers when<br/> there's a large amount of dark colors <br/> introduced to a scene.               |
| CIELAB      | Great for colorful scenes, doesn't suffer<br/> as much to dark scenes like Oklab,<br/> but has worse color accuracy in cases. |

You can compare the color spaces gradient abilities and quantization [here](https://raphlinus.github.io/color/2021/01/18/oklab-critique.html) for a general understanding.

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
Icon is provided by [fatcow](http://www.fatcow.com/free-icons) under CC Attribution 3.0

## Contact
For any questions or suggestions, feel free to contact AlexDalas or Siydge on our [Discord](https://discord.gg/2rvVtb32qA).

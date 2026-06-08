# Third-Party Notices

This project uses third-party software through NuGet packages and SDK-managed publish output. Keep this file in sync with the restored dependency graph whenever packages are added, removed, or updated.

Run the license check before merging dependency changes:

```powershell
./Scripts/Check-Licenses.ps1
```

The release artifacts also include this file at the publish root.

## NuGet Packages

The following package list was generated from the restored `obj/project.assets.json` dependency graph on 2026-06-07.

| Package | Version | License | Attribution |
| --- | --- | --- | --- |
| Avalonia | 12.0.4 | MIT | Avalonia Team |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-3-Clause-style ANGLE license file | The ANGLE Project Authors; Avalonia Team package metadata |
| Avalonia.BuildServices | 11.3.2 | MIT | Avalonia Team |
| Avalonia.Controls.DataGrid | 12.0.0 | MIT | Avalonia Team |
| Avalonia.Desktop | 12.0.4 | MIT | Avalonia Team |
| Avalonia.FreeDesktop | 12.0.4 | MIT | Avalonia Team |
| Avalonia.FreeDesktop.AtSpi | 12.0.4 | MIT | Avalonia Team |
| Avalonia.HarfBuzz | 12.0.4 | MIT | Avalonia Team |
| Avalonia.Native | 12.0.4 | MIT | Avalonia Team |
| Avalonia.Remote.Protocol | 12.0.4 | MIT | Avalonia Team |
| Avalonia.Skia | 12.0.4 | MIT | Avalonia Team |
| Avalonia.Themes.Fluent | 12.0.4 | MIT | Avalonia Team |
| Avalonia.Win32 | 12.0.4 | MIT | Avalonia Team |
| Avalonia.X11 | 12.0.4 | MIT | Avalonia Team |
| GitHub.Copilot.SDK | 1.0.0 | MIT | GitHub |
| HarfBuzzSharp | 8.3.1.3 | MIT | Microsoft |
| HarfBuzzSharp.NativeAssets.Linux | 8.3.1.3 | MIT | Microsoft |
| HarfBuzzSharp.NativeAssets.macOS | 8.3.1.3 | MIT | Microsoft |
| HarfBuzzSharp.NativeAssets.WebAssembly | 8.3.1.3 | MIT | Microsoft |
| HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.3 | MIT | Microsoft |
| MicroCom.Runtime | 0.11.4 | MIT | MicroCom.Runtime |
| Microsoft.Extensions.AI.Abstractions | 10.2.0 | MIT | Microsoft |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.2 | MIT | Microsoft |
| Microsoft.Extensions.Logging.Abstractions | 10.0.2 | MIT | Microsoft |
| SkiaSharp | 3.119.4 | MIT | Microsoft |
| SkiaSharp.NativeAssets.Linux | 3.119.4 | MIT | Microsoft |
| SkiaSharp.NativeAssets.macOS | 3.119.4 | MIT | Microsoft |
| SkiaSharp.NativeAssets.WebAssembly | 3.119.4 | MIT | Microsoft |
| SkiaSharp.NativeAssets.Win32 | 3.119.4 | MIT | Microsoft |
| System.Diagnostics.DiagnosticSource | 10.0.2 | MIT | Microsoft |
| System.IO.Pipelines | 10.0.2 | MIT | Microsoft |
| System.Text.Encodings.Web | 10.0.2 | MIT | Microsoft |
| System.Text.Json | 10.0.2 | MIT | Microsoft |
| Tmds.DBus.Protocol | 0.92.0 | MIT | Tom Deseyn |
| YamlDotNet | 18.0.0 | MIT | Antoine Aubry |

## Bundled GitHub Copilot CLI Runtime

The `GitHub.Copilot.SDK` package can publish a platform-specific `copilot-cli` runtime directory with this app. The Copilot CLI is licensed separately under the GitHub Copilot CLI License. Do not remove, alter, or obscure its proprietary notices. Release artifacts must retain the CLI license file at the SDK-published path under `copilot-cli`.

The publish-output check verifies that every generated artifact includes this notice file and that any bundled `copilot-cli` directory retains a `LICENSE.md` file.

## MIT License Text

Packages marked `MIT` above are licensed under the MIT License:

```text
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## ANGLE License Text

The `Avalonia.Angle.Windows.Natives` package includes ANGLE native binaries and a packaged `LICENSE` file. Its notice text is reproduced here for binary distribution:

```text
Copyright 2018 The ANGLE Project Authors.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

    Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer.

    Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution.

    Neither the name of TransGaming Inc., Google Inc., 3DLabs Inc.
    Ltd., nor the names of their contributors may be used to endorse
    or promote products derived from this software without specific
    prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
```
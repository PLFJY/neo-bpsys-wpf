# neo-bpsys-wpf

A modern Identity V Bp system, which can help you live a Identity V game with beauty bp view in a easy way.

# Build

You should prepare [.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0) before you build the app

``` cmd
mkdir build
dotnet publish ".\neo-bpsys-wpf\neo-bpsys-wpf.csproj" -c Release -o ".\build\neo-bpsys-wpf"
:: Pack installer
".\InstallerGenerate\Inno Setup 6\ISCC.exe" ".\InstallerGenerate\build_Installer.iss"
```

You also can use the script version

``` cmd
.\build.bat
```


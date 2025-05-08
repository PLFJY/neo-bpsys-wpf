# neo-bpsys-wpf

A modern Identity V Bp system, which can help you live a Identity V game with beauty bp view in a easy way.

# Build

``` cmd
mkdir build
dotnet publish ".\neo-bpsys-wpf\neo-bpsys-wpf.csproj" -c Release -o ".\build\neo-bpsys-wpf"
:: Pack installer
".\InstallerGenerate\iscc\ISCC.exe" ".\InstallerGenerate\build_Installer.iss"
```

If you trust me, you also can use the script ver.

``` cmd
.\build.bat
```


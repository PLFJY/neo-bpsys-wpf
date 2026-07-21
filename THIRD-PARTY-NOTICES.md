# Third-Party Notices

## 7-Zip

- **Upstream**: https://github.com/ip7z/7zip
- **Version**: 见 `third_party/7zip/7zip.lock.json`
- **Architecture**: x64
- **License**: GNU LGPL + BSD 3-clause + unRAR restriction (see License.txt)
- **Native files**: `third_party/7zip/win-x64/7z.exe`, `third_party/7zip/win-x64/7z.dll`
- **Shipped location**: `<AppBase>/Tools/7Zip/{7z.exe, 7z.dll, License.txt}`
- **Usage**: Runtime archive extraction (.7z/.zip) and SmartBP module packaging.

The application ships the official x64 7-Zip binaries. Users are not required to
install 7-Zip separately.

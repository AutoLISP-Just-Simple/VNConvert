# VNConvert (.NET 8.0 Native AOT)

Thư viện và công cụ chuyển đổi bảng mã Tiếng Việt hiệu năng cao, được viết lại hoàn toàn trên nền tảng **.NET 8.0**, hỗ trợ biên dịch **Native AOT** (cả dưới dạng thư viện `.NET`, thư viện liên kết động C-ABI `.dll` nguyên bản thay thế `vnconv.dll` của C++, và công cụ dòng lệnh độc lập `vnconv.exe`).

Dự án kế thừa và tương thích 100% thuật toán cùng hệ thống 19 bảng mã từ thư viện `vnconv` (UniKey Project - Phạm Kim Long).

AJS - lisp.vn code lại bằng Google Gemini HN T10/2026
---

## Cấu trúc Solution

- **[src/VNConvert](src/VNConvert)**: Thư viện lõi chuyển đổi bảng mã Tiếng Việt (`net8.0`, `IsAotCompatible=true`, hỗ trợ `Span<byte>`, `Stream`, `String` và xuất hàm C-ABI qua `[UnmanagedCallersOnly]`).
- **[src/VNConvert.Cli](src/VNConvert.Cli)**: Công cụ dòng lệnh `vnconv` biên dịch Native AOT độc lập, không cần cài đặt .NET Runtime.
- **[tests/VNConvert.Tests](tests/VNConvert.Tests)**: Bộ kiểm thử đơn vị (xUnit) kiểm tra toàn diện 19 bảng mã, loại bỏ dấu, đổi hoa/thường, VIQR escape và chuyển đổi file.

---

## Danh sách 19 bảng mã hỗ trợ

| ID | Tên bảng mã | Enum `VnCharsetId` | Mô tả |
|---:|---|---|---|
| 0 | `UNICODE` | `Unicode` | Unicode UTF-16 LE |
| 1 | `UTF-8` | `Utf8` | Unicode UTF-8 |
| 2 | `NCR-DEC` | `UnicodeRef` | Numeric Character Reference Decimal (`&#D;`) |
| 3 | `NCR-HEX` | `UnicodeRefHex` | Numeric Character Reference Hex (`&#xH;`) |
| 4 | `UNI-COMP` | `UnicodeDecomposed` | Unicode Tổ hợp / Decomposed (UTF-16 LE) |
| 5 | `WINCP-1258` | `WinCp1258` | Windows Code Page 1258 |
| 6 | `UNI-CSTRING` | `UnicodeCString` | Unicode C-String Escape (`\xHHHH`) |
| 10 | `VIQR` | `Viqr` | Vietnamese Quoted-Readable |
| 11 | `UVIQR` | `Utf8Viqr` | UTF-8 / VIQR Mixed |
| 20 | `TCVN3` | `Tcvn3` | TCVN3 (ABC) - 1 byte |
| 21 | `VPS` | `Vps` | VPS - 1 byte |
| 22 | `VISCII` | `Viscii` | VISCII - 1 byte |
| 23 | `BKHCM1` | `Bkhcm1` | Bách Khoa HCM 1 - 1 byte |
| 24 | `VIETWARE-F` | `VietwareF` | Vietware-F - 1 byte |
| 25 | `ISC` | `Isc` | ISC - 1 byte |
| 40 | `VNI-WIN` | `VniWin` | VNI-Windows - 2 bytes |
| 41 | `BKHCM2` | `Bkhcm2` | Bách Khoa HCM 2 - 2 bytes |
| 42 | `VIETWARE-X` | `VietwareX` | Vietware-X - 2 bytes |
| 43 | `VNI-MAC` | `VniMac` | VNI-Macintosh - 2 bytes |

---

## Hướng dẫn sử dụng (Managed .NET API)

```csharp
using VNConvert;

// 1. Chuyển đổi chuỗi giữa các bảng mã
string vniText = VnConverter.Convert("Tiếng Việt", VnCharsetId.Utf8, VnCharsetId.VniWin);
string utf8Text = VnConverter.Convert(vniText, VnCharsetId.VniWin, VnCharsetId.Utf8);

// 2. Bỏ dấu Tiếng Việt
string noTone = VnConverter.RemoveTone("Cộng hòa Xã hội Chủ nghĩa Việt Nam");
// -> "Cong hoa Xa hoi Chu nghia Viet Nam"

// 3. Chuyển đổi chữ HOA / chữ thường chuẩn Tiếng Việt
string upper = VnConverter.ToUpper("Tiếng Việt"); // -> "TIẾNG VIỆT"
string lower = VnConverter.ToLower("TIẾNG VIỆT"); // -> "tiếng việt"

// 4. Chuyển đổi trực tiếp mảng byte / ReadOnlySpan<byte>
byte[] outputBytes = VnConverter.Convert(inputBytes, VnCharsetId.Tcvn3, VnCharsetId.Utf8);

// 5. Chuyển đổi File
VnConverter.ConvertFile("input_tcvn3.txt", "output_utf8.txt", VnCharsetId.Tcvn3, VnCharsetId.Utf8);
```

---

## Hướng dẫn biên dịch Native AOT

### 1. Biên dịch Native DLL (C-ABI thay thế `vnconv.dll` gốc)
Xuất đầy đủ các hàm C-ABI: `VnConvert`, `VnConvert0`, `VnConvertSB`, `VnConvertChar`, `VnConvertStr`, `VnFileConvert`, `VnConvertString`, `VnConvErrMsg`, `VnConvSetOptions`, `VnConvGetOptions`, `VnConvResetOptions`.

```powershell
dotnet publish .\src\VNConvert\VNConvert.csproj -c Release -r win-x64 -p:NativeLib=Shared
```
File kết quả: `src/VNConvert/bin/Release/net8.0/win-x64/publish/VNConvert.dll`

### 2. Biên dịch công cụ dòng lệnh `vnconv.exe` (Native AOT)
```powershell
dotnet publish .\src\VNConvert.Cli\VNConvert.Cli.csproj -c Release -r win-x64
```
File kết quả: `src/VNConvert.Cli/bin/Release/net8.0/win-x64/publish/vnconv.exe`

Ví dụ sử dụng CLI:
```powershell
# Xem danh sách bảng mã
vnconv --list

# Chuyển đổi chuỗi từ VIQR sang UTF-8
vnconv -f VIQR -t UTF-8 'Vie^.t Nam'

# Chuyển đổi và bỏ dấu + viết hoa
vnconv -f VIQR -t UTF-8 -r -u 'Vie^.t Nam'

# Chuyển đổi file từ TCVN3 sang UTF-8
vnconv -f TCVN3 -t UTF-8 -i input.txt -o output.txt
```

### 3. Chạy kiểm thử (Unit Tests)
```powershell
dotnet test VNConvert.sln
```

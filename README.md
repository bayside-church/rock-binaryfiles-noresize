# Rock RMS BinaryFilesNoResize Plugin

A Rock RMS plugin that provides an alternative binary file upload endpoint with optional image resize control.

## The Problem

Rock RMS's standard `/api/BinaryFiles/Upload` endpoint automatically resizes all uploaded images to fit within **1024x768 pixels**, regardless of the `BinaryFileType.MaxWidth/MaxHeight` configuration settings.

**Root Cause:**
- `BinaryFilesController.Upload()` calls `FileUtilities.GetFileContentStream(uploadedFile)` without passing the `resizeIfImage` parameter
- `FileUtilities.GetFileContentStream()` defaults `resizeIfImage=true`
- When `resizeIfImage=true`, the method calls `RoughResize(bmp, 1024, 768)` with hard-coded dimensions

**Impact:**
- API consumers cannot upload images at their original resolution
- `BinaryFileType.MaxWidth/MaxHeight` settings above 1024x768 are effectively ignored
- Workflows requiring exact image dimensions or downstream resizing are broken

## The Solution

This plugin adds a new REST endpoint that allows API consumers to control whether images are resized during upload via the `resizeIfImage` query parameter.

---

## API Reference

### Endpoint

```
POST /api/BinaryFilesNoResize/Upload
```

### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `binaryFileTypeGuid` | Guid | Yes | - | The GUID of the BinaryFileType for the upload |
| `resizeIfImage` | bool | No | `true` | When `false`, skips the 1024x768 resize |

### Request

- **Content-Type:** `multipart/form-data`
- **Authentication:** Required (API Key or Rock session)
- **Body:** File in form data

### Response Codes

| Code | Condition | Body |
|------|-----------|------|
| 201 Created | Success | `BinaryFile.Id` (integer) |
| 400 Bad Request | Invalid GUID or no file | Error message string |
| 401 Unauthorized | Missing/invalid authentication | - |
| 500 Internal Server Error | Unexpected error | Error message string |

### Usage Examples

**Upload without resize (preserve original dimensions):**
```bash
curl -X POST \
  "https://your-rock-server/api/BinaryFilesNoResize/Upload?binaryFileTypeGuid=03BD8476-8A9F-4078-B628-5B538F967AFC&resizeIfImage=false" \
  -H "Authorization-Token: YOUR-API-KEY" \
  -F "file=@high-resolution-image.jpg"
```

**Upload with resize (matches Rock's default behavior):**
```bash
curl -X POST \
  "https://your-rock-server/api/BinaryFilesNoResize/Upload?binaryFileTypeGuid=03BD8476-8A9F-4078-B628-5B538F967AFC&resizeIfImage=true" \
  -H "Authorization-Token: YOUR-API-KEY" \
  -F "file=@image.jpg"
```

**PowerShell Example:**
```powershell
$headers = @{ "Authorization-Token" = "YOUR-API-KEY" }
$uri = "https://your-rock-server/api/BinaryFilesNoResize/Upload?binaryFileTypeGuid=03BD8476-8A9F-4078-B628-5B538F967AFC&resizeIfImage=false"

$form = @{
    file = Get-Item -Path "C:\path\to\image.jpg"
}

Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Form $form
```

---

## Deployment

### Prerequisites

- Rock RMS v13.0 or later
- Administrator access to the Rock server

### Option 1: Download Pre-built DLL (Recommended)

1. Download `org.obsidian.BinaryFilesNoResize.dll` from the [Releases](../../releases) page

2. Copy the DLL to your Rock installation's `Bin` folder:
   ```
   C:\inetpub\wwwroot\Bin\org.obsidian.BinaryFilesNoResize.dll
   ```
   (Adjust path based on your Rock installation location)

3. Restart the IIS Application Pool:
   ```powershell
   # Run as Administrator
   iisreset
   ```
   Or recycle the specific app pool from IIS Manager.

4. Verify the plugin loaded by checking:
   - **Admin Tools > Security > REST Controllers** - Look for `BinaryFilesNoResize`
   - Or query the database:
     ```sql
     SELECT * FROM RestController WHERE ClassName LIKE '%NoResize%'
     ```

### Option 2: Build from Source

#### Prerequisites for Building

- Access to Rock.dll and Rock.Rest.dll from your Rock installation
- The Roslyn C# compiler (included in Rock's `Bin\roslyn` folder)

#### Build Steps

1. Clone this repository:
   ```bash
   git clone https://github.com/bayside-church/rock-binaryfiles-noresize.git
   cd rock-binaryfiles-noresize
   ```

2. Update the reference paths in `org.obsidian.BinaryFilesNoResize.csproj` to point to your Rock installation:
   ```xml
   <HintPath>C:\inetpub\wwwroot\Bin\Rock.dll</HintPath>
   <HintPath>C:\inetpub\wwwroot\Bin\Rock.Rest.dll</HintPath>
   ```

3. Compile using Rock's Roslyn compiler:
   ```powershell
   & 'C:\inetpub\wwwroot\Bin\roslyn\csc.exe' `
     /target:library `
     /out:bin\Release\org.obsidian.BinaryFilesNoResize.dll `
     /reference:'C:\inetpub\wwwroot\Bin\Rock.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\Rock.Rest.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\System.Web.Http.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\System.Web.Http.WebHost.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\System.Net.Http.Formatting.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\EntityFramework.dll' `
     /reference:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.dll' `
     /reference:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Net.Http.dll' `
     BinaryFilesNoResizeController.cs
   ```

4. Copy the built DLL to Rock's Bin folder and restart IIS (see Option 1, steps 2-4)

---

## Security Configuration

After deployment, configure permissions for the new endpoint:

1. Navigate to **Admin Tools > Security > REST Controllers**

2. Find **BinaryFilesNoResize** in the list

3. Click on the controller to configure security:
   - By default, the endpoint requires authentication
   - Configure which security roles can access the Upload action
   - Recommended: Limit to specific API users or roles that need upload capability

### Creating an API Key

1. Navigate to **Admin Tools > Security > REST Keys**
2. Click **Add Key**
3. Configure:
   - **Name:** Descriptive name (e.g., "Image Upload Service")
   - **User:** Associate with a Person record
   - **Key:** Auto-generated or custom
4. Save and use the key in the `Authorization-Token` header

---

## Finding BinaryFileType GUIDs

To find the GUID for your desired BinaryFileType:

**Via Rock Admin:**
1. Navigate to **Admin Tools > General Settings > File Types**
2. Click on the file type
3. The GUID is shown in the URL or can be found in the file type details

**Via Database:**
```sql
SELECT Id, Name, [Guid] FROM BinaryFileType ORDER BY Name
```

**Common BinaryFileType GUIDs:**
| Name | GUID |
|------|------|
| Person Image | `03BD8476-8A9F-4078-B628-5B538F967AFC` |
| Content Channel Item Image | `8DBF874C-F3C2-4848-8137-C963C431EB0B` |
| Check-in Label | `DE0E5C50-234B-474C-940C-C571F385E65F` |

---

## Troubleshooting

### Endpoint returns 404

- Verify the DLL is in the Rock `Bin` folder
- Restart IIS (`iisreset`)
- Check that the controller is registered:
  ```sql
  SELECT * FROM RestController WHERE ClassName LIKE '%NoResize%'
  ```

### Endpoint returns 401 Unauthorized

- Verify your API key is valid and active
- Ensure the API key's associated user has permission to the REST controller
- Check that the `Authorization-Token` header is correctly formatted

### Endpoint returns 400 Bad Request

- Verify the `binaryFileTypeGuid` parameter is a valid GUID
- Ensure the BinaryFileType exists in Rock
- Check that a file is included in the multipart form data

### Images still being resized

- Ensure you're passing `resizeIfImage=false` in the query string
- Verify you're calling the new endpoint (`/api/BinaryFilesNoResize/Upload`) not the standard one

---

## Technical Details

### How It Works

The plugin creates a new REST controller that mirrors Rock's standard `BinaryFilesController.Upload()` method, with one key difference:

```csharp
// Standard Rock behavior (always resizes):
ContentStream = FileUtilities.GetFileContentStream(uploadedFile);

// This plugin (respects parameter):
ContentStream = FileUtilities.GetFileContentStream(uploadedFile, resizeIfImage);
```

### Project Structure

```
org.obsidian.BinaryFilesNoResize/
├── BinaryFilesNoResizeController.cs   # REST API controller
├── org.obsidian.BinaryFilesNoResize.csproj
└── bin/
    └── Release/
        └── org.obsidian.BinaryFilesNoResize.dll
```

### Dependencies

- .NET Framework 4.7.2
- Rock.dll (from Rock RMS installation)
- Rock.Rest.dll (from Rock RMS installation)
- System.Web.Http (ASP.NET Web API)
- EntityFramework 6.x

---

## Version History

### v1.0.0
- Initial release
- `POST /api/BinaryFilesNoResize/Upload` endpoint
- `resizeIfImage` query parameter support
- Full Rock RMS security integration

---

## License

MIT License - See [LICENSE](LICENSE) file for details.

---

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## Support

For issues and feature requests, please use the [GitHub Issues](../../issues) page.

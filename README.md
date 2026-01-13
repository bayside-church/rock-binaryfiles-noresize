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
| `binaryFileTypeId` | int | Yes | - | The ID of the BinaryFileType for the upload |
| `resizeIfImage` | bool | No | `true` | When `false`, skips the 1024x768 resize |

### Request

- **Content-Type:** `multipart/form-data`
- **Authentication:** Required (API Key or Rock session)
- **Body:** File in form data

### Response Codes

| Code | Condition | Body |
|------|-----------|------|
| 201 Created | Success | `BinaryFile.Id` (integer) |
| 400 Bad Request | No file provided | Error message string |
| 401 Unauthorized | Missing/invalid authentication | - |
| 500 Internal Server Error | Unexpected error | Error message string |

### Usage Examples

**Upload without resize (preserve original dimensions):**
```bash
curl -X POST \
  "https://your-rock-server/api/BinaryFilesNoResize/Upload?binaryFileTypeId=5&resizeIfImage=false" \
  -H "Authorization-Token: YOUR-API-KEY" \
  -F "file=@high-resolution-image.jpg"
```

**Upload with resize (matches Rock's default behavior):**
```bash
curl -X POST \
  "https://your-rock-server/api/BinaryFilesNoResize/Upload?binaryFileTypeId=5&resizeIfImage=true" \
  -H "Authorization-Token: YOUR-API-KEY" \
  -F "file=@image.jpg"
```

**PowerShell Example:**
```powershell
$headers = @{ "Authorization-Token" = "YOUR-API-KEY" }
$uri = "https://your-rock-server/api/BinaryFilesNoResize/Upload?binaryFileTypeId=5&resizeIfImage=false"

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

### Installation

1. Download `com.BaysideChurch.BinaryFilesNoResize.dll` from the [Releases](../../releases) page

2. Copy the DLL to your Rock installation's `Bin` folder:
   ```
   C:\inetpub\wwwroot\Bin\com.BaysideChurch.BinaryFilesNoResize.dll
   ```
   (Adjust path based on your Rock installation location)

3. Restart the IIS Application Pool:
   ```powershell
   # Run as Administrator
   iisreset
   ```
   Or recycle the specific app pool from IIS Manager.

4. Verify the plugin loaded by checking **Admin Tools > Security > REST Controllers** for `BinaryFilesNoResize`

That's it! Rock automatically discovers and loads plugin DLLs from the Bin folder on startup.

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

## Finding BinaryFileType IDs

To find the ID for your desired BinaryFileType:

**Via Rock Admin:**
1. Navigate to **Admin Tools > General Settings > File Types**
2. Click on the file type
3. The ID is shown in the URL (e.g., `/BinaryFileType/5`)

**Via Database:**
```sql
SELECT Id, Name FROM BinaryFileType ORDER BY Name
```

**Common BinaryFileType IDs:**
| Id | Name |
|----|------|
| 1 | Check-in Label |
| 2 | Transaction Image |
| 3 | Unsecured |
| 5 | Person Image |
| 6 | Content Channel Item Image |

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
com.BaysideChurch.BinaryFilesNoResize/
├── BinaryFilesNoResizeController.cs   # REST API controller
├── com.BaysideChurch.BinaryFilesNoResize.csproj
└── bin/
    └── Release/
        └── com.BaysideChurch.BinaryFilesNoResize.dll
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

## Building from Source

<details>
<summary>Click to expand build instructions (for developers only)</summary>

If you want to modify the plugin, you can build from source using Rock's Roslyn compiler:

1. Clone this repository
2. Update the reference paths in `com.BaysideChurch.BinaryFilesNoResize.csproj` to point to your Rock installation
3. Compile:
   ```powershell
   & 'C:\inetpub\wwwroot\Bin\roslyn\csc.exe' `
     /target:library `
     /out:com.BaysideChurch.BinaryFilesNoResize.dll `
     /reference:'C:\inetpub\wwwroot\Bin\Rock.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\Rock.Rest.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\System.Web.Http.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\System.Net.Http.Formatting.dll' `
     /reference:'C:\inetpub\wwwroot\Bin\EntityFramework.dll' `
     /reference:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.dll' `
     /reference:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Net.Http.dll' `
     com.BaysideChurch.BinaryFilesNoResize\BinaryFilesNoResizeController.cs
   ```
4. Copy the DLL to Rock's Bin folder and restart IIS

</details>

---

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## Support

For issues and feature requests, please use the [GitHub Issues](../../issues) page.

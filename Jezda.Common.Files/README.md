# Jezda.Common.Files

Centralizovana biblioteka za validaciju fajlova u mikroservisima Jezda Solutions.

## Ključne funkcionalnosti
- Detekcija MIME tipa na osnovu sadržaja (magic numbers).
- Validacija veličine, ekstenzije, MIME tipa i naziva fajla.
- Opciono: heuristike bezbednosti (zip bomb, izvršni fajlovi, dimenzije slika).
- Opciono: SHA-256 hashing sadržaja.
- Jednostavan API (`IFormFile` i `Stream`).
- DI integracija: `services.AddFileValidation()`.

## Brzi start
```csharp
// Program.cs
builder.Services.AddFileValidation();

// Endpoint
var overrides = new FileValidationOverrides
{
    MaxSizeBytes = 10 * 1024 * 1024,
    AllowedExtensions = new[] { "png", "jpg", "jpeg", "pdf" },
    AllowedMimeTypes = new[] { "image/png", "image/jpeg", "application/pdf" }
};

var result = await fileValidator.ValidateAsync(req.File, overrides, ct);
if (!result.IsValid)
{
    return Results.BadRequest(result.Errors.Select(e => new { e.Code, e.Message }));
}
```

## Konfiguracija (primer)
```json
{
  "FileValidation": {
    "MaxSizeBytes": 10485760,
    "AllowedMimeTypes": ["image/png", "image/jpeg", "application/pdf"],
    "AllowedExtensions": ["png", "jpg", "jpeg", "pdf"],
    "BlockedExtensions": ["exe", "dll"],
    "BlockExecutables": true,
    "EnableZipSafetyChecks": true,
    "ZipMaxDepth": 5,
    "ZipMaxEntries": 1000,
    "ZipMaxCompressionRatio": 200.0,
    "EnableImageDimensionChecks": false,
    "EnableHashing": true,
    "EnableMalwareHeuristics": true
  }
}
```

## Napomene
- Stream se koristi pažljivo (seek/reset) bez nepotrebnog učitavanja u memoriju.
- MIME detekcija primarno gleda sadržaj; ekstenzije su sekundarni signal.
- Za AV integraciju, registrujte sopstveni `IFileScanner` preko DI.
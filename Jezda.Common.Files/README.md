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
- `ValidateAsync(Stream)` zahteva seekable stream (stream koji nije seekable baca `ArgumentException`); sadržaj se čita od trenutne pozicije, a pozicija se vraća posle validacije. `IFormFile.OpenReadStream()` jeste seekable.
- `IFileService.UploadAsync` čuva fajl pod imenom koje vrati validator (`FileValidationResult.NormalizedFileName`, po `FileValidationOptions.Policy`), pa sopstvena politika imena ide u opcije validacije, ne u `FileService`. **Breaking:** `FileService` više ne prima `FileNamePolicy`; `FileNamePolicy` registrovan u DI se više ne koristi — prebacite ga u `FileValidationOptions.Policy`. Sopstveni `IFileValidator` mora da postavi `NormalizedFileName` za neprazno ime.
- `LocalFileStorage` čuva fajl kao `<guid>-<ime>` i skraćuje osnovu imena (ekstenzija ostaje) tako da stane u 255 UTF-8 bajtova (`NAME_MAX` na Linuxu); `FileDescriptor.Name` zadržava puno ime. `IFileStorage.SaveAsync` pozvan direktno čuva prazno ime kao `file`, dok ga `IFileService.UploadAsync` odbija.
- Neuspela validacija pri uploadu baca `FileValidationException` (nasleđuje `InvalidOperationException`); `Errors` nosi kodove, npr. `TooLarge` za 413.
- MIME detekcija primarno gleda sadržaj; ekstenzije su sekundarni signal.
- Za AV integraciju, registrujte sopstveni `IFileScanner` preko DI.
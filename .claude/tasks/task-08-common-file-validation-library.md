# Task 08 — Centralizovana validacija fajlova u Jezda.Common.Files

Ovim taskom uvodimo zajedničku biblioteku za rad sa fajlovima, kako bismo uklonili duplikate i obezbedili jedinstven, konfigurabilan pristup validaciji fajlova u svim mikroservisima.

## Naziv i Paket
- Paket/biblioteka: `Jezda.Common.Files`
- Ključna komponenta: `FileValidation` (detekcija, validacija, bezbednosne proverе)
- DI ekstenzija: `services.AddFileValidation()` iz `Jezda.Common.Files`

## Cilj
- Centralizovati validaciju fajlova (veličina, tip/MIME, ekstenzija, sadržaj) za sve mikroservise.
- Obezbediti konzistentan, konfigurabilan i proširiv API sa obaveznim i opcionim parametrima.
- Omogućiti opcionu “lightweight” sigurnosnu proveru (heuristike protiv malicioznih fajlova: zip-bomb, izvršne binarne).

## Obavezne Funkcionalnosti
- Validacija veličine fajla (min/max).
- Validacija tipa fajla na osnovu sadržaja (magic numbers), ne samo ekstenzije.
- Validacija dozvoljenih ekstenzija i MIME tipova.
- Validacija naziva fajla (dužina, dozvoljeni karakteri, normalizacija).
- Vraćanje strukturisanog rezultata validacije sa kodovima grešaka i detaljima.
- Jednostavan API koji radi sa `IFormFile` i sa `Stream`.

## Opcione Funkcionalnosti
- Heuristička provera “malicioznosti” (opciono):
  - Zip bomb detekcija (max dubina, broj fajlova, kompresioni odnos).
  - Blokiranje izvršnih binarnih (`.exe`, `.dll`, ELF, Mach-O) — kada je omogućeno.
  - “Shebang” detekcija u tekst fajlovima (`#!/bin/sh`, `#!/usr/bin/python`).
- Provera dimenzija slika (min/max širina/visina).
- Hashiranje sadržaja (SHA-256) radi audit loga.
- Eksterni antivirus skener preko plugina (`IFileScanner`), podrazumevano `NoopFileScanner`.
- Sanacija naziva fajla (zamena nevalidnih karaktera, Unicode normalizacija).
- Per-poziv override opcija (endpoint može tražiti strožije limite od globalnih).

## Struktura Biblioteke
- `Jezda.Common.Files/Validation/`
  - `IFileValidator`
  - `FileValidator` (podrazumevana implementacija)
  - `FileValidationOptions` (globalne opcije preko `IOptions<T>`)
  - `FileValidationOverrides` (per-poziv override)
  - `FileValidationResult` (rezultat sa listom `FileValidationError`)
  - `FileValidationErrorCode` (enum)
- `Jezda.Common.Files/Detection/`
  - `IMimeTypeDetector`
  - `MagicNumberMimeTypeDetector` (registar potpisa: PNG/JPEG/PDF/ZIP/DOCX/XLSX/TXT, itd.)
  - `FileSignature` (tipovi i potpis kao bajt patterni)
- `Jezda.Common.Files/Security/`
  - `IFileScanner` (AV ili heuristike)
  - `NoopFileScanner`
  - `ZipSafetyChecker`
  - `ExecutableDetector`
  - `ImageSafetyChecker`
- `Jezda.Common.Files/Naming/`
  - `FileNamePolicy` (max dužina, dozvoljeni karakteri, Unicode politika, sanitizacija)
- `Jezda.Common.Files/Extensions/`
  - `ServiceCollectionExtensions` → `AddFileValidation(this IServiceCollection services)`
  - `FormFileExtensions` → helperi za stream čitanje, hashiranje

## Predloženi Interfejsi i Klase (potpisi)
```csharp
public interface IFileValidator
{
    Task<FileValidationResult> ValidateAsync(
        IFormFile file,
        FileValidationOverrides? overrides = null,
        CancellationToken ct = default);

    Task<FileValidationResult> ValidateAsync(
        Stream fileStream,
        string? fileName = null,
        FileValidationOverrides? overrides = null,
        CancellationToken ct = default);
}

public sealed class FileValidationOptions
{
    public long MaxSizeBytes { get; set; }
    public string[]? AllowedMimeTypes { get; set; }
    public string[]? AllowedExtensions { get; set; }
    public string[]? BlockedExtensions { get; set; }
    public bool BlockExecutables { get; set; } = true;
    public bool EnableZipSafetyChecks { get; set; }
    public int ZipMaxDepth { get; set; } = 5;
    public int ZipMaxEntries { get; set; } = 1000;
    public double ZipMaxCompressionRatio { get; set; } = 200.0; // kompresija 200x
    public bool EnableImageDimensionChecks { get; set; }
    public int? MinWidth { get; set; }
    public int? MaxWidth { get; set; }
    public int? MinHeight { get; set; }
    public int? MaxHeight { get; set; }
    public FileNamePolicy Policy { get; set; } = new();
    public bool EnableHashing { get; set; }
    public bool EnableMalwareHeuristics { get; set; }
}

public sealed class FileValidationOverrides
{
    public long? MaxSizeBytes { get; set; }
    public string[]? AllowedMimeTypes { get; set; }
    public string[]? AllowedExtensions { get; set; }
    public string[]? BlockedExtensions { get; set; }
    public bool? BlockExecutables { get; set; }
    public bool? EnableZipSafetyChecks { get; set; }
    public int? ZipMaxDepth { get; set; }
    public int? ZipMaxEntries { get; set; }
    public double? ZipMaxCompressionRatio { get; set; }
    public bool? EnableImageDimensionChecks { get; set; }
    public int? MinWidth { get; set; }
    public int? MaxWidth { get; set; }
    public int? MinHeight { get; set; }
    public int? MaxHeight { get; set; }
    public bool? EnableHashing { get; set; }
    public bool? EnableMalwareHeuristics { get; set; }
}

public sealed class FileValidationResult
{
    public bool IsValid { get; set; }
    public string? MimeType { get; set; }
    public string? NormalizedFileName { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public IReadOnlyList<FileValidationError> Errors { get; set; } = Array.Empty<FileValidationError>();
}

public enum FileValidationErrorCode
{
    TooLarge,
    EmptyFile,
    InvalidMimeType,
    InvalidExtension,
    ExecutableBlocked,
    ZipBombSuspected,
    InvalidImageDimensions,
    InvalidFileName,
    UnsupportedSignature,
    MalwareSuspected,
    ScannerError
}

public sealed class FileValidationError
{
    public FileValidationErrorCode Code { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface IMimeTypeDetector
{
    Task<string?> DetectAsync(Stream stream, CancellationToken ct = default);
}

public interface IFileScanner
{
    Task<FileScanResult> ScanAsync(Stream stream, CancellationToken ct = default);
}

public sealed class FileScanResult
{
    public bool IsClean { get; set; }
    public string? Engine { get; set; }
    public string? Report { get; set; }
    public string[]? Findings { get; set; }
}
```

## DI Registracija
- `services.AddFileValidation()` registruje:
  - `IFileValidator` → `FileValidator`
  - `IMimeTypeDetector` → `MagicNumberMimeTypeDetector`
  - `IFileScanner` → `NoopFileScanner` (može se zameniti u mikroservisu)
  - `IOptions<FileValidationOptions>` (uvek postoji, default vrednosti)

## Konfiguracija (u appsettings svakog servisa)
- Primer:
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

## Primer Upotrebe u Mikroservisu
- `Program.cs`:
```csharp
builder.Services.AddFileValidation();
```
- U endpointu (FastEndpoints / minimal API):
```csharp
var overrides = new FileValidationOverrides
{
    MaxSizeBytes = 10 * 1024 * 1024,
    AllowedExtensions = new[] { "png", "jpg" }
};

var result = await fileValidator.ValidateAsync(req.File, overrides, ct);
if (!result.IsValid)
{
    return Results.BadRequest(result.Errors.Select(e => new { e.Code, e.Message }));
}
// dalje: snimanje fajla u storage...
```

## Testovi
- Jedinični:
  - Detekcija MIME po sadržaju (magic numbers).
  - Validacija veličine/ekstenzija/naziva.
  - Zip heuristike (dubina/entries/ratio).
  - Blokiranje izvršnih fajlova.
  - Image dimenzije.
- Integracioni:
  - DI registracija i čitanje konfiguracije.
  - Per-poziv override.
- Performanse:
  - Veliki fajlovi (streaming), bez nepotrebnog učitavanja u memoriju.

## Migracija u Postojeće Servise
- Pretražiti mesta sa custom file validation i zameniti pozive `IFileValidator` iz `Jezda.Common.Files`.
- Konsolidovati MIME/ekstenzije u `appsettings.json` servisa ili ostaviti default vrednosti iz `Jezda.Common.Files`.
- Ako neki servis koristi AV, injektovati custom `IFileScanner` implementaciju umesto `NoopFileScanner`.

## Definition of Done
- `Jezda.Common.Files` paket objavljen i referenciran u svim mikroservisima.
- Svi servisi koriste `IFileValidator` umesto lokalnih validacija.
- Konfiguracija centralizovana u `appsettings.json` sa podrškom za override.
- Testovi pokrivaju ključne scenarije (≥80% za modul).
- Dokumentacija sa primerima upotrebe i listom podržanih tipova/signatura.

## Implementacioni Koraci
1) Skaffold biblioteke `Jezda.Common.Files` i podprojekta testova.
2) Implementirati `FileValidationOptions`, `FileValidationOverrides`, `FileValidationResult` i `FileValidationError`.
3) Implementirati `MagicNumberMimeTypeDetector` sa inicijalnim skupom potpisa.
4) Implementirati `FileNamePolicy` (sanitizacija/normativ).
5) Implementirati `FileValidator` (glavna orkestracija: veličina → naziv → MIME → ekstenzije → opcione sigurnosne provere → hashing).
6) Implementirati sigurnosne komponente: `ZipSafetyChecker`, `ExecutableDetector`, `ImageSafetyChecker`.
7) Dodati DI ekstenziju `AddFileValidation`.
8) Napisati testove (unit + integration) i CI hook za pokretanje.
9) Dokumentovati upotrebu i primere.
10) Migrirati servise na novi validator (postepeno, po modulima).

## Napomene
- Voditi računa o performantnom radu sa streamovima (čitanje header-a i signatura bez nepotrebnog kopiranja).
- MIME detekciju zasnivati prvenstveno na sadržaju; ekstenzije koristiti kao sekundarni signal.
- Ostaviti mogućnost da servisi doregistruju sopstveni `IFileScanner` (npr. Cloud AV).
- Osigurati da rezultati validacije sadrže dovoljno konteksta za audit/log (npr. `Sha256` kada je omogućeno).

## Primer: Integracija u JS.TMS.API i unapređenja

### Šta je dodato u mikroservis
- Registracija biblioteke: u `Program.cs` pozvati `services.AddFileValidation()`.
- Konfiguracija: u `appsettings.json` sekcija `FileValidation` (primer gore).
- Endpoint za upload priloga (primer): koristi `IFileValidator` pre snimanja fajla u storage.

```csharp
// Primer FastEndpoints endpointa za upload priloga na WorkItem
using FastEndpoints;
using Jezda.Common.Files.Validation; // IFileValidator

public sealed class UploadWorkItemAttachmentRequest
{
    public Guid WorkItemId { get; set; }
    public IFormFile File { get; set; } = default!;
}

public sealed class UploadAttachmentResponse
{
    public Guid AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
}

public sealed class UploadWorkItemAttachmentEndpoint : Endpoint<UploadWorkItemAttachmentRequest, UploadAttachmentResponse>
{
    private readonly IFileValidator _fileValidator;
    private readonly IAttachmentStorage _storage; // postojeći storage servis u mikroservisu

    public UploadWorkItemAttachmentEndpoint(IFileValidator fileValidator, IAttachmentStorage storage)
    {
        _fileValidator = fileValidator;
        _storage = storage;
    }

    public override void Configure()
    {
        Post("/work-items/{WorkItemId:guid}/attachments");
        AllowAnonymous(false);
        // Permissions/roles po potrebi
    }

    public override async Task HandleAsync(UploadWorkItemAttachmentRequest req, CancellationToken ct)
    {
        var overrides = new FileValidationOverrides
        {
            MaxSizeBytes = 10 * 1024 * 1024, // 10MB za ovaj konkretan endpoint
            AllowedExtensions = new[] { "png", "jpg", "jpeg", "pdf" },
            AllowedMimeTypes = new[] { "image/png", "image/jpeg", "application/pdf" }
        };

        var validation = await _fileValidator.ValidateAsync(req.File, overrides, ct);
        if (!validation.IsValid)
        {
            await SendErrorsAsync(validation.Errors.Select(e => new { e.Code, e.Message }), 400, ct);
            return;
        }

        // Snimanje fajla u storage (streaming, bez učitavanja celog fajla u memoriju)
        var attachmentId = await _storage.SaveAsync(req.WorkItemId, req.File, ct);

        var rsp = new UploadAttachmentResponse
        {
            AttachmentId = attachmentId,
            FileName = validation.NormalizedFileName ?? req.File.FileName,
            MimeType = validation.MimeType,
            SizeBytes = validation.SizeBytes,
            Sha256 = validation.Sha256
        };

        await SendCreatedAtAsync($"/work-items/{req.WorkItemId}/attachments/{attachmentId}", rsp, generateAbsoluteUrl: false, cancellation: ct);
    }
}
```

### Kako možemo unaprediti
- Antivirus/heuristike: injektovati custom `IFileScanner` (npr. ClamAV, VirusTotal) umesto `NoopFileScanner` i aktivirati `EnableMalwareHeuristics`.
- Quarantine + async skeniranje: fajl snimiti u “quarantine” lokaciju, vratiti 202 Accepted; nakon skeniranja premeštaj u produkcionu lokaciju.
- Hash deduplikacija: koristiti `Sha256` za detekciju duplikata i preskakanje re-upload-a.
- Resumable/chunked upload: uvesti chunking (Tus protocol ili custom) radi pouzdanosti na slabim vezama.
- Image normalizacija: ako su slike, raditi normalizaciju (strip EXIF, re-encode) i enforce dimenzije preko `ImageSafetyChecker`.
- Rate limiting i zaštita od abuse-a: ograničiti broj upload-a po korisniku/IP.
- Observability: dodati metrike (broj pokušaja, fail/success, veličine), log-ove sa `Sha256`, MIME i ishodima validacije.
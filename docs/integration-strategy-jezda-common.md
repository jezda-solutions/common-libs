# Strategija Integracija u Jezda.Common

Ovaj dokument definiše arhitekturu i standarde za implementaciju eksternih integracija (Azure DevOps, Jira, Slack, itd.) u okviru `Jezda.Common` biblioteke.

Cilj je kreirati **centralizovan, ponovo iskoristiv (reusable) i tipiziran** sloj za komunikaciju sa eksternim servisima, kako bi se izbeglo dupliranje koda u različitim modulima (TMS, HRMS, Release Management).

---

## 1. Arhitektonski Koncept: "Internal SDK"

Umesto kreiranja zasebnih mikroservisa za svaku integraciju, koristimo **Shared Library (SDK)** pristup. `Jezda.Common` će služiti kao wrapper oko eksternih API-ja, pružajući našim modulima jednostavne metode fokusirane na biznis logiku, dok sakriva kompleksnost HTTP poziva, autentifikacije i serijalizacije.

### Ključni Principi
*   **Encapsulation (Enkapsulacija):** Potrošač (npr. TMS modul) ne treba da zna da li Azure DevOps koristi OAuth ili PAT token, niti koji je tačan URL endpointa.
*   **Strong Typing (Tipizacija):** Sve metode vraćaju C# objekte (DTO), a ne sirovi JSON/`dynamic`.
*   **Dependency Injection:** Sve integracije se registruju u DI kontejneru.
*   **Configuration:** Podešavanja (URL, Tokeni) se čitaju iz `appsettings.json` preko Options pattern-a.

---

## 2. Predložena Struktura Projekta (`Jezda.Common`)

Unutar `Jezda.Common` projekta, organizacija treba da izgleda ovako:

```text
Jezda.Common/
├── Integrations/
│   ├── Abstractions/                  <-- Interfejsi koje moduli koriste
│   │   ├── IAzureDevOpsClient.cs
│   │   ├── IJiraClient.cs
│   │   └── IIntegrationService.cs
│   │
│   ├── AzureDevOps/                   <-- Konkretna implementacija za ADO
│   │   ├── AzureDevOpsClient.cs       <-- Implementacija IAzureDevOpsClient
│   │   ├── AzureDevOpsOptions.cs      <-- Konfiguracija (BaseUrl, PAT)
│   │   └── Models/                    <-- DTO klase (Data Transfer Objects)
│   │       ├── AdoWorkItem.cs
│   │       ├── AdoWorkItemUpdate.cs
│   │       └── AdoProject.cs
│   │
│   ├── Jira/                          <-- Konkretna implementacija za Jiru
│   │   ├── JiraClient.cs
│   │   └── ...
│   │
│   └── Extensions/
│       └── IntegrationServiceCollectionExtensions.cs <-- Extension metode za DI
```

---

## 3. Implementacioni Detalji (Primer: Azure DevOps)

### 3.1. Konfiguracija (`AzureDevOpsOptions.cs`)
Definišemo klasu koja mapira sekciju iz `appsettings.json`.

```csharp
public class AzureDevOpsOptions
{
    public const string SectionName = "Integrations:AzureDevOps";
    
    public string BaseUrl { get; set; } = string.Empty; // npr. https://dev.azure.com/{organization}
    public string PersonalAccessToken { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "7.1";
}
```

### 3.2. Interfejs (`IAzureDevOpsClient.cs`)
Definišemo šta naši moduli mogu da traže od ADO-a.

```csharp
public interface IAzureDevOpsClient
{
    // Vraća work iteme na osnovu WIQL upita
    Task<List<AdoWorkItem>> GetWorkItemsByQueryAsync(string query, CancellationToken cancellationToken = default);
    
    // Vraća detalje jednog work itema
    Task<AdoWorkItem?> GetWorkItemByIdAsync(int id, CancellationToken cancellationToken = default);
    
    // Kreira novi work item (npr. automatski bug report)
    Task<AdoWorkItem> CreateWorkItemAsync(string project, string type, Dictionary<string, object> fields, CancellationToken cancellationToken = default);
}
```

### 3.3. Implementacija (`AzureDevOpsClient.cs`)
Ova klasa koristi `HttpClient` i bavi se "prljavim" detaljima.

```csharp
public class AzureDevOpsClient : IAzureDevOpsClient
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsClient> _logger;

    public AzureDevOpsClient(HttpClient httpClient, IOptions<AzureDevOpsOptions> options, ILogger<AzureDevOpsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<AdoWorkItem>> GetWorkItemsByQueryAsync(string query, CancellationToken cancellationToken)
    {
        // 1. Priprema requesta (Authentication header se može podesiti i u DI registraciji)
        // 2. Slanje POST requesta sa WIQL upitom
        // 3. Deserijalizacija JSON odgovora u List<AdoWorkItem>
        // 4. Hendlovanje grešaka (try-catch, logging)
    }
}
```

### 3.4. Registracija (`IntegrationServiceCollectionExtensions.cs`)
Olakšavamo potrošačima da uključe integraciju.

```csharp
public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddAzureDevOpsIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureDevOpsOptions>(configuration.GetSection(AzureDevOpsOptions.SectionName));

        services.AddHttpClient<IAzureDevOpsClient, AzureDevOpsClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AzureDevOpsOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{options.PersonalAccessToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        })
        // Opciono: Dodati Polly polisu za retry ovde
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(600)));

        return services;
    }
}
```

---

## 4. Kako koristiti u Modulima (npr. TMS Modul)

### Korak 1: Podešavanje (`appsettings.json` u TMS API-ju)
```json
{
  "Integrations": {
    "AzureDevOps": {
      "BaseUrl": "https://dev.azure.com/mojabirma/",
      "PersonalAccessToken": "moj-tajni-token-xxx"
    }
  }
}
```

### Korak 2: Registracija servisa (`Program.cs` u TMS API-ju)
```csharp
// Učitavamo integraciju iz Jezda.Common
builder.Services.AddAzureDevOpsIntegration(builder.Configuration);
```

### Korak 3: Korišćenje u servisu (`TimesheetSyncService.cs`)
```csharp
public class TimesheetSyncService
{
    private readonly IAzureDevOpsClient _adoClient;

    public TimesheetSyncService(IAzureDevOpsClient adoClient)
    {
        _adoClient = adoClient;
    }

    public async Task SyncDailyHoursAsync()
    {
        // apstrahovano - ne bavimo se HTTP-om, JSON-om, Tokenima
        var workItems = await _adoClient.GetWorkItemsByQueryAsync("Select [System.Id] From WorkItems Where ...");
        
        foreach(var item in workItems)
        {
             // Mapiranje u TMS entitet i čuvanje u bazu
        }
    }
}
```

---

## 5. Plan za Buduće Integracije

Kada se pojavi potreba za novom integracijom (npr. Jira):

1.  **Analiza**: Koje podatke nam trebaju? (Samo čitanje taskova ili i ažuriranje?)
2.  **Struktura**: Kreirati novi folder `Jezda.Common/Integrations/Jira`.
3.  **Abstrakcija**: Definisati `IJiraClient`.
4.  **Implementacija**: Implementirati klijent i DTO modele.
5.  **Registracija**: Dodati `AddJiraIntegration` u ekstenzije.

Ovaj pristup garantuje da su svi moduli (TMS, HRMS, itd.) "čisti" od eksternih zavisnosti i da se sva komunikacija odvija na standardizovan, kontrolisan način.

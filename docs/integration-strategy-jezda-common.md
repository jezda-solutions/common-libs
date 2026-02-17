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

## 2. Implementirane Integracije

Trenutno su dostupne sledeće integracije:

1.  **Azure DevOps** (`Jezda.Common.Integrations.AzureDevOps`)
    *   **Features**: Work Items, Worklogs (Time Tracking), Projects, WIQL queries.
    *   **Auth**: PAT (Personal Access Token).
2.  **GitHub** (`Jezda.Common.Integrations.GitHub`)
    *   **Features**: Repositories, Issues.
    *   **Auth**: Bearer Token (PAT).
3.  **Jira** (`Jezda.Common.Integrations.Jira`)
    *   **Features**: Projects, Issues (Search via JQL).
    *   **Auth**: Basic Auth (Email + API Token).

## 3. Predložena Struktura Projekta (`Jezda.Common`)

Unutar `Jezda.Common` projekta, organizacija izgleda ovako:

```text
Jezda.Common/
├── Integrations/
│   ├── Abstractions/                  <-- Interfejsi koje moduli koriste
│   │   ├── IAzureDevOpsClient.cs
│   │   ├── IGitHubClient.cs
│   │   ├── IJiraClient.cs
│   │   └── IIntegrationService.cs
│   │
│   ├── AzureDevOps/                   <-- Konkretna implementacija za ADO
│   │   ├── AzureDevOpsClient.cs       <-- Implementacija IAzureDevOpsClient
│   │   ├── AzureDevOpsOptions.cs      <-- Konfiguracija (BaseUrl, PAT)
│   │   └── Models/                    <-- DTO klase (Data Transfer Objects)
│   │       ├── AdoWorkItem.cs
│   │       ├── AdoWorkLog.cs
│   │       └── AdoProject.cs
│   │
│   ├── GitHub/                        <-- Konkretna implementacija za GitHub
│   │   ├── GitHubClient.cs
│   │   ├── GitHubOptions.cs
│   │   └── Models/
│   │       ├── GitHubRepository.cs
│   │       └── GitHubIssue.cs
│   │
│   ├── Jira/                          <-- Konkretna implementacija za Jiru
│   │   ├── JiraClient.cs
│   │   ├── JiraOptions.cs
│   │   └── Models/
│   │       ├── JiraProject.cs
│   │       └── JiraIssue.cs
│   │
│   └── Extensions/
│       └── IntegrationServiceCollectionExtensions.cs <-- Extension metode za DI
```

---

## 4. Implementacioni Detalji (Primeri)

### 4.1. Azure DevOps
Koristi se za upravljanje taskovima i praćenje vremena.
```csharp
// Program.cs
builder.Services.AddAzureDevOpsIntegration(builder.Configuration);

// Service
var workItems = await _adoClient.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems WHERE [System.State] = 'Active'");
```

### 4.2. GitHub
Koristi se za pristup repozitorijumima i issue-ima.
```csharp
// Program.cs
builder.Services.AddGitHubIntegration(builder.Configuration);

// Service
var repos = await _gitHubClient.GetRepositoriesAsync();
```

### 4.3. Jira
Koristi se kao alternativa za ADO, sa podrškom za JQL pretragu.
```csharp
// Program.cs
builder.Services.AddJiraIntegration(builder.Configuration);

// Service
var issues = await _jiraClient.SearchIssuesAsync("project = 'MYPROJ' AND priority = High");
```

---

## 5. Kako koristiti u Modulima (npr. TMS Modul)

### Korak 1: Podešavanje (`appsettings.json`)
```json
{
  "Integrations": {
    "AzureDevOps": {
      "BaseUrl": "https://dev.azure.com/mojabirma/",
      "PersonalAccessToken": "moj-tajni-token-xxx"
    },
    "GitHub": {
      "BaseUrl": "https://api.github.com/",
      "AccessToken": "moj-github-pat"
    },
    "Jira": {
      "BaseUrl": "https://mojabirma.atlassian.net/",
      "Email": "email@example.com",
      "ApiToken": "moj-jira-token"
    }
  }
}
```

### Korak 2: Registracija servisa
U `Program.cs` ili `Startup.cs`:

```csharp
// Učitavamo integracije po potrebi
builder.Services.AddAzureDevOpsIntegration(builder.Configuration);
builder.Services.AddGitHubIntegration(builder.Configuration);
builder.Services.AddJiraIntegration(builder.Configuration);
```

### Korak 3: Korišćenje u servisu
Injektujte odgovarajući interfejs (`IAzureDevOpsClient`, `IGitHubClient`, `IJiraClient`) i koristite metode koje vraćaju tipizirane objekte.

---

## 6. Plan za Buduće Integracije

Kada se pojavi potreba za novom integracijom (npr. Jira):

1.  **Analiza**: Koje podatke nam trebaju? (Samo čitanje taskova ili i ažuriranje?)
2.  **Struktura**: Kreirati novi folder `Jezda.Common/Integrations/Jira`.
3.  **Abstrakcija**: Definisati `IJiraClient`.
4.  **Implementacija**: Implementirati klijent i DTO modele.
5.  **Registracija**: Dodati `AddJiraIntegration` u ekstenzije.

Ovaj pristup garantuje da su svi moduli (TMS, HRMS, itd.) "čisti" od eksternih zavisnosti i da se sva komunikacija odvija na standardizovan, kontrolisan način.

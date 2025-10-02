# Jezda.Common.Extensions

Extension methods for common .NET types, Hangfire integration, and utility helpers.

## 📦 Installation

```bash
dotnet add package Jezda.Common.Extensions
```

## 🎯 What's Included

### Extension Methods

- **`DateTimeExtensions`** - DateTime manipulation and formatting
- **`PagedListExtensions`** - Extensions for working with paginated results
- **`HttpResponseDataExtension`** - HTTP response data helpers

### Hangfire Extensions

- **`HangfireExtensions`** - Hangfire configuration and setup helpers
- **PostgreSQL integration** - Hangfire with PostgreSQL storage

## 💡 Quick Examples

```csharp
// DateTime extensions
var date = DateTime.Now;
// [Add specific examples based on your DateTimeExtensions]

// PagedList extensions
var pagedData = items.ToPagedList(pageNumber, pageSize);

// Hangfire setup
services.AddHangfireWithPostgres(configuration);
```

## 🔗 Dependencies

- **Hangfire 1.8.21** - Background job processing
- **Hangfire.PostgreSql 1.20.12** - PostgreSQL storage for Hangfire
- **Entity Framework Core 9.0.8**
- **Npgsql 9.0.3** - PostgreSQL provider

## 📚 Documentation

For complete documentation, see the [main repository README](https://github.com/jezda-solutions/jezda-common-libs).

## 📄 License

MIT License - see [LICENSE](https://github.com/jezda-solutions/jezda-common-libs/blob/master/LICENSE) for details.

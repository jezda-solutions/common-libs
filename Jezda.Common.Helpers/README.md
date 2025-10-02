# Jezda.Common.Helpers

Utility classes and helper implementations for common tasks including encryption, string manipulation, date/time handling, and more.

## 📦 Installation

```bash
dotnet add package Jezda.Common.Helpers
```

## 🎯 What's Included

### Helper Classes

- **`EncryptionHelper`** - Encryption and decryption utilities
- **`StringHelper`** - String manipulation and validation
- **`DateTimeOffsetHelper`** - DateTimeOffset utilities and conversions
- **`CurrencyCodeHelper`** - Currency code validation and utilities
- **`DisplayMasker`** - Masking sensitive data for display (e.g., credit cards, emails)
- **`PermissionHelper`** - Permission and authorization helpers
- **Identity utilities** - User identity and authentication helpers

## 💡 Quick Examples

```csharp
// Encryption
var encrypted = EncryptionHelper.Encrypt("sensitive data");
var decrypted = EncryptionHelper.Decrypt(encrypted);

// String masking
var maskedEmail = DisplayMasker.MaskEmail("user@example.com");
// Output: u***@example.com

var maskedCard = DisplayMasker.MaskCreditCard("1234567890123456");
// Output: ************3456

// Date/time utilities
var utcNow = DateTimeOffsetHelper.GetUtcNow();

// Currency validation
bool isValid = CurrencyCodeHelper.IsValidCode("USD");

// String helpers
var slug = StringHelper.ToSlug("Hello World!");
// Output: hello-world
```

## 🔗 Dependencies

- **Microsoft.Extensions.Identity.Core 9.0.8** - Identity framework support
- **Jezda.Common.Abstractions** - Common interfaces
- **Jezda.Common.Domain** - Domain models

## 📚 Documentation

For complete documentation, see the [main repository README](https://github.com/jezda-solutions/jezda-common-libs).

## 📄 License

MIT License - see [LICENSE](https://github.com/jezda-solutions/jezda-common-libs/blob/master/LICENSE) for details.

# Jezda.Common.Contracts

Shared request and response contracts exchanged between Jezda services.

## Installation

```bash
dotnet add package Jezda.Common.Contracts
```

## What's Included

### Permissions

- **`PermissionRegistrationRequest`** - sent by a service at startup to register its permissions
  (service code, name, version and the permission list)
- **`PermissionRegistrationDto`** - a single permission: a `module:resource:action` code and a
  description
- **`PermissionRegistrationResponse`** - the result: success flag, message, and counts and codes of
  added and existing permissions

## License

MIT License - see [LICENSE](https://github.com/jezda-solutions/common-libs/blob/master/LICENSE.txt) for details.

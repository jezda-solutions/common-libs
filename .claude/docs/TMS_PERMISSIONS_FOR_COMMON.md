# TMS Permissions for Jezda.Common

Complete list of all TMS permissions that need to be added to the global Jezda.Common library.

**✅ STATUS: IMPLEMENTED** - All permissions added to `Jezda.Common.Domain/Constants/PermissionConstants.cs` (December 2024)

---

## Work Items

```
tms:work-item:create
tms:work-item:read
tms:work-item:update
tms:work-item:delete
tms:work-item:assign
tms:work-item:unassign
tms:work-item:reorder-children
```

### Work Item Assignments

```
tms:work-item-assignment:create
tms:work-item-assignment:read
tms:work-item-assignment:update
tms:work-item-assignment:delete
```

### Work Item Bulk Operations

```
tms:work-item:bulk:start
tms:work-item:bulk:cancel
tms:work-item:bulk:read
tms:work-item:bulk:update
tms:work-item:bulk:delete
```

---

## Work Item Comments

```
tms:work-item-comment:create
tms:work-item-comment:read
tms:work-item-comment:update
tms:work-item-comment:delete
```

### Mentions

```
tms:mentions:read
```

---

## Work Item Attachments

```
tms:work-item-attachment:create
tms:work-item-attachment:read
tms:work-item-attachment:delete
```

### Comment Attachments

```
tms:work-item-comment-attachment:create
tms:work-item-comment-attachment:read
tms:work-item-comment-attachment:delete
```

---

## Work Item Reactions

```
tms:work-item-comment-reaction:create
tms:work-item-comment-reaction:read
tms:work-item-comment-reaction:delete
```

---

## Work Item Dependencies

```
tms:work-item-dependency:create
tms:work-item-dependency:read
tms:work-item-dependency:delete
```

### Dependency Analysis

```
tms:work-item-dependency-graph:read
tms:work-item-dependency-impact:read
```

---

## Work Item Recurrences

```
tms:work-item-recurrence:create
tms:work-item-recurrence:read
tms:work-item-recurrence:update
tms:work-item-recurrence:delete
tms:work-item-recurrence:generate
tms:work-item-recurrence:trigger-job
tms:work-item-recurrence:job-status
```

---

## Work Item Statuses

```
tms:work-item-status:create
tms:work-item-status:read
tms:work-item-status:update
tms:work-item-status:delete
```

### Status Templates

```
tms:work-item-status-template:read
tms:work-item-status-template:apply
```

---

## Work Item Types

```
tms:work-item-type:create
tms:work-item-type:view
tms:work-item-type:update
tms:work-item-type:delete
```

### Type Templates

```
tms:work-item-type-template:read
tms:work-item-type-template:apply
```

---

## Work Item Tags

```
tms:work-item-tag:create
tms:work-item-tag:read
tms:work-item-tag:update
tms:work-item-tag:delete
```

---

## Projects

```
tms:project:create
tms:project:read
tms:project:update
tms:project:delete
```

### Project Members

```
tms:project-member:add
tms:project-member:remove
tms:project-member:read
```

---

## Time Logs

```
tms:time-log:create
tms:time-log:read
tms:time-log:update
tms:time-log:delete
```

---

## Organisation Settings

```
tms:organisation-settings:read
tms:organisation-settings:update
```

### Onboarding

```
tms:onboarding-status:read
```

---

## Organisation Glossaries

```
tms:organisation-glossary:create
tms:organisation-glossary:read
tms:organisation-glossary:update
tms:organisation-glossary:delete
```

---

## Task Set Templates

```
tms:task-set-template:read
tms:task-set-template:apply
```

---

## Total Count

**82 permissions** in total across all TMS modules.

---

## Notes

- All permissions follow the pattern: `tms:entity:action`
- **Entities strictly use kebab-case** (e.g., `work-item`, `work-item-status`, `work-item-type`)
- Actions are: `create`, `read`, `update`, `delete`, plus domain-specific actions
- **No exceptions** - all permissions use consistent kebab-case naming

---

## Changelog

### December 2024 - Naming Convention Standardization

**BREAKING CHANGE**: Work Item Type permissions updated to use consistent kebab-case naming.

**Changed:**
- ❌ `tms:workitemtype:*` → ✅ `tms:work-item-type:*`
- ❌ `tms:workitemtype-template:*` → ✅ `tms:work-item-type-template:*`

**Rationale:** All TMS permissions now strictly follow kebab-case convention without exceptions. Consumers of these permissions will need to update their permission checks accordingly.

**Implementation:** Completed in `Jezda.Common.Domain/Constants/PermissionConstants.cs`

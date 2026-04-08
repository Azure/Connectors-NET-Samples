# Copilot Instructions for Connectors-NET-Samples

## Overview

This repository contains sample projects demonstrating how to use the [Azure Connectors .NET SDK](https://github.com/Azure/Connectors-NET-SDK). Code must follow the team's coding conventions based on BPM repo standards.

## Quick Reference: Coding Style Rules

### File Structure

```csharp
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Azure.Connectors.Sdk;

namespace DirectConnector
{
    public class YourClass
    {
    }
}
```

**Rules:**

- Copyright header: Use `//----` (4 dashes) format with double space before "All rights reserved"
- Usings OUTSIDE namespace (standard C# convention)
- Usings sorted: System.* first, then alphabetically
- No empty lines between using groups

### Naming and Qualification

| Element | Rule | Example |
|---------|------|---------|
| Static members | Qualify with class name | `MyClass.StaticMethod()` |
| Instance members | Qualify with `this.` | `this.instanceField` |
| Private fields | Use `_camelCase` | `private readonly string _connectionString;` |
| Constants | Qualify with class name | `MyClass.DefaultTimeout` |
| Local variables | Use complete English terms | `parameter` not `p`, `method` not `m` |
| Lambda parameters | Use descriptive names | `methods.Where(method => ...)` not `m => ...` |

**Variable naming rules:**

- Use complete, unabbreviated English terms for all identifiers
- No single-letter variable names, even in lambdas (use `arg`, `item`, `method`, `parameter`)
- No placeholder names (`blah`, `foo`, `temp`, `x`) — always use meaningful names

### Async/Await Format

**ALWAYS use this multi-line format:**

```csharp
var result = await this.httpClient
    .GetAsync(requestUri)
    .ConfigureAwait(continueOnCapturedContext: false);
```

**Rules:**

- Period on NEW line, not end of previous line
- Arguments indented ONE level (4 spaces)
- ALWAYS use `ConfigureAwait(continueOnCapturedContext: false)` with explicit parameter name

**DO NOT:**

```csharp
// Wrong: ConfigureAwait without named parameter
.ConfigureAwait(false);

// Wrong: Method call on same line as object
var result = await this.httpClient.GetAsync(requestUri)
    .ConfigureAwait(continueOnCapturedContext: false);

// Wrong: Missing ConfigureAwait
var result = await this.httpClient.GetAsync(requestUri);
```

### Exception Handling

```csharp
catch (SpecificException ex)
{
    this.logger.LogError(ex, "Failed: '{Message}'.", ex.Message);
    throw;
}
catch (Exception ex) when (!ex.IsFatal())
{
    throw new InvalidOperationException(message: "Operation failed.", innerException: ex);
}
```

**Rules:**

- Exception variable name: `ex` (not `exception`)
- Wrap inserted values in single quotes in error messages
- End error messages with period

### Boolean Parameters

**Boolean parameters MUST always use named arguments:**

```csharp
// Correct
IdentifierNormalizer.Normalize(name, isVariableName: true);
this.CreateNode(schema, isRequired: false);

// Wrong - unnamed boolean is ambiguous
IdentifierNormalizer.Normalize(name, true);
this.CreateNode(schema, false);
```

### String Comparison

**ALWAYS use StringComparison:**

```csharp
string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase)
```

### Variable Declaration

**Use `var` when type is obvious:**

```csharp
var items = new List<string>();
var response = await this.GetResponseAsync();
```

### Ternary Operators

**Put `?` and `:` at START of new line:**

```csharp
var result = condition
    ? valueIfTrue
    : valueIfFalse;
```

### Access Modifiers

**ALWAYS explicit - order: access, static, readonly, other:**

```csharp
public static readonly string DefaultValue = "default";
private readonly ILogger _logger;
```

## Patterns to Avoid

| Anti-Pattern | Correct Pattern |
|--------------|-----------------|
| `.Result` on Task | `await task.ConfigureAwait(continueOnCapturedContext: false)` |
| `.Wait()` on Task | `await task.ConfigureAwait(continueOnCapturedContext: false)` |
| `Task.Run()` for I/O | `await` the async method directly |
| `new Exception("msg.")` | `new SpecificException(message: "msg.")` |
| Magic numbers | Named constants (e.g., `MyClass.DefaultTimeoutSeconds`) |
| Magic strings (e.g., `"type"`, `"object"`) | Named constants (e.g., `SchemaPropertyNames.Type`) |
| `[0]` or `.First()` | `.Single()` (or `.SingleOrDefault()` + explicit validation) |

## Git Workflow

- Branch naming: `feature/description`, `fix/description`, `docs/description`
- Never push directly to main
- Always create PR for review

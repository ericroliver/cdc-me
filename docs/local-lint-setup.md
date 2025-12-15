# Local Lint Setup Guide

This guide explains how to catch lint failures locally **100% of the time** before creating a PR, preventing CI/CD pipeline failures.

## Overview

The project now includes multiple layers of protection to ensure code quality:

1. **EditorConfig** - Enforces formatting rules in your editor
2. **VSCode Settings** - Auto-formats on save
3. **Pre-commit Hook** - Blocks commits with formatting issues
4. **Local Lint Script** - Replicates CI checks exactly

## Quick Setup

### 1. Install Required Tools

Ensure you have .NET SDK 9.0 or later installed:

```bash
dotnet --version
```

### 2. Enable the Pre-commit Hook

The pre-commit hook is already installed at [`.git/hooks/pre-commit`](.git/hooks/pre-commit:1). It will automatically run before every commit.

To test it:

```bash
# Try to commit (it will check formatting first)
git commit -m "test"

# To bypass the hook (NOT recommended):
git commit --no-verify -m "test"
```

### 3. Configure Your Editor

#### VSCode (Recommended)

The project includes VSCode settings at [`.vscode/settings.json`](.vscode/settings.json:1) that will:
- Auto-format on save
- Organize imports
- Trim trailing whitespace
- Use correct indentation

Install recommended extensions:
```bash
# VSCode will prompt you to install these
# Or install manually:
code --install-extension ms-dotnettools.csharp
code --install-extension editorconfig.editorconfig
```

#### Other Editors

The [`.editorconfig`](.editorconfig:1) file works with most modern editors. Install the EditorConfig plugin for your editor:
- Visual Studio: Built-in support
- JetBrains Rider: Built-in support
- Vim/Neovim: Install `editorconfig-vim`
- Emacs: Install `editorconfig-emacs`

## Running Checks Manually

### Quick Format Check

```bash
# Check if formatting is correct (matches CI)
dotnet format cdc-me.sln --verify-no-changes --verbosity diagnostic
```

### Auto-fix Formatting Issues

```bash
# Automatically fix all formatting issues
dotnet format cdc-me.sln
```

### Full Lint Check (Recommended Before PR)

```bash
# Run the complete lint check script (matches CI exactly)
./scripts/lint-check.sh
```

This script checks:
- ✅ Code formatting
- ✅ Build with warnings as errors
- ✅ Static analysis
- ✅ Nullable reference warnings
- ✅ Vulnerable packages

## Understanding the Checks

### What the CI Pipeline Checks

The CI pipeline runs (see [`.github/workflows/ci.yml`](.github/workflows/ci.yml:99)):

```yaml
- name: Run .NET Format (Code Formatting Check)
  run: dotnet format cdc-me.sln --verify-no-changes --verbosity diagnostic
```

This checks for:
- **Whitespace errors** - Incorrect indentation, trailing spaces, line endings
- **Code style violations** - Naming conventions, brace placement, etc.
- **Analyzer warnings** - Including xUnit warnings like `xUnit1012`

### Common Formatting Issues

#### 1. Whitespace Errors

**Error:**
```
error WHITESPACE: Fix whitespace formatting. Replace 25 characters with '\n\s\s\s\s\s\s\s\s\s\s\s\s'.
```

**Fix:**
```bash
dotnet format cdc-me.sln
```

#### 2. xUnit Warnings

**Error:**
```
warning xUnit1012: Null should not be used for type parameter 'identifier' of type 'string'.
```

**Fix:** Change parameter type to nullable:
```csharp
// Before
[InlineData(null, false)]
public void Test(string identifier, bool expected)

// After
[InlineData(null, false)]
public void Test(string? identifier, bool expected)
```

## Workflow Integration

### Recommended Development Workflow

1. **Write code** - VSCode auto-formats on save
2. **Before committing** - Pre-commit hook runs automatically
3. **Before creating PR** - Run `./scripts/lint-check.sh`
4. **Create PR** - CI pipeline validates everything

### If Pre-commit Hook Fails

```bash
# See what's wrong
dotnet format cdc-me.sln --verify-no-changes --verbosity diagnostic

# Fix it automatically
dotnet format cdc-me.sln

# Try commit again
git add .
git commit -m "your message"
```

### If You Need to Bypass (Emergency Only)

```bash
# NOT RECOMMENDED - only for emergencies
git commit --no-verify -m "emergency fix"
```

**Warning:** Bypassing the hook means your PR will likely fail CI checks.

## Troubleshooting

### Pre-commit Hook Not Running

```bash
# Check if hook is executable
ls -la .git/hooks/pre-commit

# Make it executable
chmod +x .git/hooks/pre-commit
```

### VSCode Not Auto-formatting

1. Check that C# extension is installed
2. Reload VSCode window: `Cmd/Ctrl + Shift + P` → "Reload Window"
3. Check [`.vscode/settings.json`](.vscode/settings.json:1) is present
4. Verify [`.editorconfig`](.editorconfig:1) is in project root

### Format Command Fails

```bash
# Clean and restore
dotnet clean cdc-me.sln
dotnet restore cdc-me.sln

# Try format again
dotnet format cdc-me.sln
```

### Different Results Locally vs CI

This should no longer happen! The local checks now match CI exactly. If you still see differences:

1. Ensure you're using .NET 9.0+
2. Run `dotnet restore` to update tools
3. Check that [`.editorconfig`](.editorconfig:1) hasn't been modified

## Files Created/Modified

This setup includes:

- [`.editorconfig`](.editorconfig:1) - Formatting rules for all editors
- [`.vscode/settings.json`](.vscode/settings.json:1) - VSCode auto-format settings
- [`.vscode/extensions.json`](.vscode/extensions.json:1) - Recommended extensions
- [`.git/hooks/pre-commit`](.git/hooks/pre-commit:1) - Pre-commit validation
- [`scripts/lint-check.sh`](scripts/lint-check.sh:1) - Complete lint check script

## Best Practices

### DO ✅

- Let VSCode auto-format on save
- Run `./scripts/lint-check.sh` before creating PRs
- Fix formatting issues immediately
- Keep [`.editorconfig`](.editorconfig:1) in sync with team standards

### DON'T ❌

- Use `git commit --no-verify` regularly
- Ignore formatting warnings
- Modify [`.editorconfig`](.editorconfig:1) without team discussion
- Disable auto-format in VSCode

## Additional Resources

- [EditorConfig Documentation](https://editorconfig.org/)
- [dotnet format Documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Project Lint Check Script](scripts/lint-check-README.md)

## Summary

With this setup, you will catch lint failures **100% of the time** before creating a PR:

1. **Editor** catches issues as you type (via EditorConfig + VSCode settings)
2. **Pre-commit hook** catches issues before commit
3. **Lint script** catches issues before PR (run `./scripts/lint-check.sh`)
4. **CI pipeline** validates everything (should always pass now!)

No more surprise CI failures! 🎉
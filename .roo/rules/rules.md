You are an expert in c#.

- use SOLID principles at all times
- we don't overuse interfaces but we use them in the important places
- we keep our classes small and on point.
- When we can, we like to extract testable code to small self contained functions
- We use XUnit for all new tests
- New code requires tests
- When changing code, ensure all tests are kept up to date
- when you are writing code and see that you have to deviate from the stated plan. you need to halt and collaborate with me.
- Keep test code out of production projects
- it is important that you do not make architectural or systemic decisions without collaborating with me first.
- we don't like hard coded structures that will require maintenance.
- a task is not complete until it builds: dotnet build cdc-me.sln
- This development environment is running in a docker container. Therefore we cannot run docker builds in this environment.
- and all tests pass: dotnet test cdc-me.sln
- this repo has warnings as errors turned on.

# Developer Warnings Prevention Guide

This document provides specific guidance to help developers avoid the most common warnings in the codebase. These guidelines complement the main rules in `.roo/rules/rules.md`.

## Common Warnings to Avoid When Writing Code

### XML Documentation Requirements

- **Always add XML documentation for test method parameters**: Use `<param name="paramName">Description</param>` for all test method parameters, especially in parameterized tests with `[Theory]` and `[InlineData]`
- **Document return values for test helper methods**: Add `<returns>Description of what is returned</returns>` to any test method that returns data (like test data generators)
- **Add blank lines before single-line comments**: Ensure single-line comments (`//`) are preceded by a blank line for better readability

### Performance and Memory Optimization

- **Reuse JsonSerializerOptions instances**: Create static readonly `JsonSerializerOptions` fields instead of creating new instances in each test method
  ```csharp
  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
  ```
- **Use static readonly for constant arrays**: Replace inline constant arrays with static readonly fields when used repeatedly
  ```csharp
  private static readonly string[] TestArgs = { "--provider", "openai", "--start-date", "2024-01-01" };
  ```
- **Avoid unnecessary async/await**: Don't use async/await if you're just returning `Task.CompletedTask` or a completed task directly

### String Comparison Best Practices

- **Always specify StringComparison**: Use `Assert.Contains(expected, actual, StringComparison.Ordinal)` instead of the overload without StringComparison parameter
- **Be explicit about culture sensitivity**: Choose appropriate StringComparison values (Ordinal, OrdinalIgnoreCase, CurrentCulture, etc.)

### Variable and Code Cleanup

- **Remove unused variables**: Don't assign values to variables that are never used - either use them or remove the assignment
- **Clean up test setup code**: Ensure all variables created in test setup are actually used in the test logic

### Test Method Patterns to Follow

- **Parameterized test documentation**: When using `[Theory]` with `[InlineData]`, always document what each parameter represents
  ```csharp
  /// <summary>
  /// Tests amount parsing with various input formats.
  /// </summary>
  /// <param name="amountString">The input amount string to parse.</param>
  /// <param name="expectedAmount">The expected parsed decimal value.</param>
  [Theory]
  [InlineData("1.23", 1.23)]
  public void ParseAmount_ValidInput_ReturnsExpectedValue(string amountString, decimal expectedAmount)
  ```

### Code Organization

- **Keep JsonSerializerOptions at class level**: Define serialization options as static readonly fields at the top of test classes
- **Group related test data**: Use static readonly arrays for test data that's used across multiple test methods
- **Minimize object creation in tests**: Reuse expensive objects like HttpClient, JsonSerializerOptions, etc.

### Async/Await Guidelines

- **Only use async when necessary**: If a method just returns `Task.CompletedTask`, make it synchronous and return the task directly
- **Don't forget ConfigureAwait(false)**: Always use `.ConfigureAwait(false)` on async calls in library code (already mentioned in main rules)

### Common Anti-Patterns to Avoid

- Creating new `JsonSerializerOptions()` in every test method
- Using `Assert.Contains(string, string)` without StringComparison parameter
- Declaring variables that are assigned but never used
- Missing XML documentation on public test methods
- Using async/await for simple task returns
- Inline constant arrays instead of static readonly fields

### Quick Checklist Before Committing

- [ ] All test method parameters have XML documentation
- [ ] Test helper methods that return values have `<returns>` documentation
- [ ] No new JsonSerializerOptions instances created in test methods
- [ ] String comparisons use explicit StringComparison parameter
- [ ] No unused variable assignments
- [ ] Async methods only used when actually awaiting operations
- [ ] Constant arrays extracted to static readonly fields if used multiple times

## Project Overview

The CDC Testing Framework is a research project designed to create a repeatable testing environment for database change validation using SQL Server's Change Data Capture (CDC) functionality. The framework enables teams to capture, replay, and compare database changes to ensure data consistency across different implementations and performance optimizations.

## Core Concept

The framework implements a sophisticated workflow for database testing:

1. **Snapshot Creation**: Create named database snapshots as baseline states
2. **Change Capture**: Enable CDC and trace functionality to monitor data modifications
3. **Scenario Execution**: Run test scenarios while capturing all changes
4. **Data Profiling**: Extract and store CDC data for analysis
5. **Replay & Validation**: Restore snapshots, replay changes, and compare results
6. **Performance Testing**: Validate that optimized procedures produce identical data changes

Because of the use of Snaphots, Traces and CDC, the database under test must be a sql server database.

This is a great technical overview of the project: docs/architecture.md

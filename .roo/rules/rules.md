You are an expert in c#. util is a c# project.
You are an expert in python development, src is a python project
You are an expery in typescript/react development. the agentx_client is a typescript and react project

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
- and all tests pass: dotnet test cdc-me.sln

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

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Runtime & Platform

**The Linux build and tests are the source of truth, not Windows.** Azure DevOps build agents are
**Linux containers**, and the service ships in a Linux Docker image. You may develop and run tests
on a Windows dev box, but everything you write — production code, unit tests, and service tests —
MUST behave identically on Linux. A green Windows build means nothing until the Linux build and
tests are also green. You do not need to run a Linux build locally before pushing; just write code
and tests that follow the Linux-safety rules (source encoding, case-sensitive paths, explicit
`CultureInfo`, cross-platform graphics) so the agent stays green.

For the full rules and rationale see @.claude\docs\linux-runtime.md.

# Development Commands

## Building
- Use `dotnet build src` to build the solution

## Testing
- Use `dotnet test src` to run all tests
- Run the unit test project on its own while working; the whole suite before pushing
- Tests run on the Linux build agents, so they MUST pass on Linux. Develop and run tests on Windows
  as usual, but follow the Linux-safety rules above so a green Windows run also stays green on the
  agent (you do not need to run a Linux build locally before pushing).

# Writing Code

REMINDER: Did you write tests first? If not, read the Testing section below.

- CRITICAL: NEVER USE --no-verify WHEN COMMITTING CODE
- We prefer simple, clean, maintainable solutions over clever or complex ones, even if the latter are more concise or
  performant. Readability and maintainability are primary concerns.
- Make the smallest reasonable changes to get to the desired outcome. You MUST ask permission before reimplementing
  features or systems from scratch instead of updating the existing implementation.
- When editing code, respect the style you found there. The only caveat is `.editorconfig` rules that the file does not
  follow. In that case feel free to adjust it so it follows them.
- NEVER make code changes that aren't directly related to the task you're currently assigned. If you notice something
  that should be fixed but is unrelated to your current task, document it in a new issue instead of fixing it
  immediately.
- For new comments, always strive to make code readable like well-written prose. Code should explain itself in the vast
  majority of cases. For complex situations that can't be further simplified, consider adding a comment.
- When writing comments, avoid referring to temporal context about refactors or recent changes. Comments should be
  evergreen and describe the code as it is, not how it evolved or was recently changed.
- When you are trying to fix a bug or compilation error or any other issue, YOU MUST NEVER throw away the old
  implementation and rewrite without explicit permission from the user. If you are going to do this, YOU MUST STOP and
  get explicit permission from the user.
- NEVER name things as 'improved' or 'new' or 'enhanced', etc. Code naming should be evergreen. What is new someday will
  be "old" someday.
- Build output should be pristine. When making changes you should always ensure that the output does not contain any
  errors or warnings.
- ALWAYS ask for clarification rather than making assumptions.
- If you're having trouble with something, it's ok to stop and ask for help.

# Testing

## TDD IS NON-NEGOTIABLE - We Practice Test-Driven Development

### The TDD Cycle (MANDATORY FOR EVERY FEATURE):

1. **RED**: Write a failing test that defines desired functionality
2. **GREEN**: Write MINIMAL code to make the test pass
3. **REFACTOR**: Improve code design while keeping tests green
4. **REPEAT**: For each new requirement

**Before starting ANY coding task, you MUST:**

1. Acknowledge that you will write tests first
2. Identify what test(s) you'll write
3. Write and run the failing test
4. Only then proceed with implementation

### Common TDD Violations (AUTOMATIC TASK FAILURE):

- Writing implementation code before tests
- Writing multiple features before their tests
- Skipping the RED phase (test must fail first)
- Writing more code than needed to pass the test
- Not running tests before writing implementation

## Unit Tests

Unit tests should cover **almost all code**. They are the baseline requirement for every project. Use TDD as described
above, with the caveat that legacy or hard-to-test code may warrant asking the user first.

**When to write unit tests:**
- All new business logic, services, handlers, and mappers
- Edge cases, error handling, and validation logic
- Any code that can be isolated from infrastructure with mocking

For full guidelines, patterns, naming conventions, and messaging handler examples see: @.claude\docs\unit-tests.md

## Service Tests

Service tests are **required** for all microservice solutions. They cover the **main flows** of the microservice by
spinning up the real ASP.NET Core host in-process via `WebApplicationFactory`, making HTTP requests, and asserting on
responses.

Where possible, service tests should be written **from the side of the business**, derived from the acceptance criteria
of the story. Scenarios should read like business requirements, not technical implementation details. This guidance
applies to business-facing flows — purely technical assertions (e.g., verifying a 200 status code or that a header is
forwarded) are fine to express in technical terms.

**When to write service tests:**
- All main flows of the microservice (happy paths through each endpoint)
- Critical business capabilities, even if they are edge cases
- Authentication and authorization flows

**When NOT to use service tests:**
- General edge cases — use unit tests instead
- Validation logic — use unit tests instead
- Internal helper/utility behavior — use unit tests instead

Service tests should only cover edge cases when the edge case represents a **critical business capability** (e.g., a
specific error response that downstream systems depend on, a fallback behavior during third-party outage).

For full setup details, patterns, and examples see: @.claude\docs\service-tests.md

## Testing Strategy Summary

| What | Test Type | Coverage Goal |
|---|---|---|
| Business logic, handlers, mappers | Unit tests | All code paths including edge cases |
| Main endpoint flows | Service tests | All main flows |
| Critical business edge cases | Service tests | Only when business-critical |
| General edge cases & validation | Unit tests | Comprehensive |
| Client library methods | Service tests | Every public method at least once |

# Specific Technologies

## Data

- @.claude\docs\data.md

## C# and API

- @.claude\docs\csharp-coding-standards.md
- @.claude\docs\csharp-naming.md
- @.claude\docs\csharp-patterns.md
- @.claude\docs\api-design.md

## Platform and testing

- @.claude\docs\distributed-messaging.md
- @.claude\docs\linux-runtime.md
- @.claude\docs\unit-tests.md
- @.claude\docs\service-tests.md
- @.claude\docs\source-control.md

# Git Principles

## 1. Mandatory Pre-Commit Failure Protocol

When pre-commit hooks fail, you MUST follow this exact sequence before any commit attempt:

1. Read the complete error output aloud (explain what you're seeing)
2. Identify which tool failed and why
3. Explain the fix you will apply and why it addresses the root cause
4. Apply the fix and re-run hooks
5. Only proceed with commit after all hooks pass

NEVER commit with failing hooks. NEVER use --no-verify. If you cannot fix the hooks, you
must ask the user for help rather than bypass them.

## 2. Explicit Git Flag Prohibition

FORBIDDEN GIT FLAGS: --no-verify, --no-hooks, --no-pre-commit-hook

Before using ANY git flag, you must:

- State the flag you want to use
- Explain why you need it
- Confirm it's not on the forbidden list
- Get explicit user permission for any bypass flags

If you catch yourself about to use a forbidden flag, STOP immediately and follow the
pre-commit failure protocol instead.

## 3. Pressure Response Protocol

When users ask you to "commit" or "push" and hooks are failing:

- Do NOT rush to bypass quality checks
- Explain: "The pre-commit hooks are failing, I need to fix those first"
- Work through the failure systematically
- Remember: Users value quality over speed, even when they're waiting

User pressure is NEVER justification for bypassing quality checks.

## 4. Accountability Checkpoint

Before executing any git command, ask yourself:

- "Am I bypassing a safety mechanism?"
- "Would this action violate the CLAUDE.md instructions?"
- "Am I choosing convenience over quality?"

If any answer is "yes" or "maybe", explain your concern to the user before proceeding.

## 5. Learning-Focused Error Response

When encountering tool failures:

- Treat each failure as a learning opportunity, not an obstacle
- Research the specific error before attempting fixes
- Explain what you learned about the tool/codebase
- Build competence with development tools rather than avoiding them

Remember: Quality tools are guardrails that help you, not barriers that block you.

## 6. Commit Message Guidelines

- NEVER mention yourself in commit messages
- Don't co-author commits

---

**Did you write tests first? If not, you have violated the prime directive. Stop and write tests NOW.**

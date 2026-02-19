# Claude Code Instructions

## General

- Start each new conversation by reading `README.md` to understand the project context

## Build

```bash
dotnet build Projects/TutoProxy/TutoProxy.sln
```

## Test

```bash
dotnet test Projects/TutoProxy/TutoProxy.sln
```

## Code Style

- Do NOT add `Async` suffix to async method names

## Commit Messages

- Keep commit messages short and concise
- Use conventional commits format: `type: short description`
- Types: `build`, `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `tooling`
- Do NOT add `Co-Authored-By` line
- Language: English

Example:
```
fix: resolve race condition in HubClientsService.Connect()
```

## Pull Request Format

- Language: English
- Format: Markdown

### Structure

```markdown
## <Stage Name>: <Short Description>

<Introductory paragraph: context from previous stage, what is implemented now, key achievement>

### Key Changes

#### 1. <Change Category>

<Description of changes>

**Key files**: `path/to/File.cs`

<Code examples where appropriate>

#### 2. <Next Category>

...

#### N. Tests

<Description of added tests>
```

### Guidelines

- Start with context: what was done in previous PR, what this PR adds
- Group changes by logical categories (architecture, optimization, refactoring, tests, docs)
- Mention specific files and classes affected
- Include code snippets for API changes or new patterns
- Use bullet points for lists of changes
- Number main sections (1, 2, 3...)
- Explain WHY, not just WHAT

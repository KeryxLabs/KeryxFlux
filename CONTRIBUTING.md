# Contributing to KeryxFlux

Thank you for considering contributing to KeryxFlux! This document provides guidelines for contributing.

## Ways to Contribute

- Report bugs or suggest features via GitHub Issues
- Submit pull requests for bug fixes or features
- Improve documentation
- Share example dockets and plugins
- Write tutorials or blog posts

## Development Setup

### Prerequisites
- .NET 8.0 SDK
- Docker (for testing with RabbitMQ/Kafka)
- Git

### Local Development

1. Fork and clone the repository:
```bash
git clone https://github.com/YOUR_USERNAME/KeryxFlux.git
cd KeryxFlux
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build solution:
```bash
dotnet build
```

4. Run tests:
```bash
dotnet test
```

5. Run locally:
```bash
cd src/KeryxFlux.Host
dotnet run -- --mode main
```

## Code Guidelines

### Architecture
KeryxFlux follows Clean Architecture with Hexagonal (Ports & Adapters) pattern:

```
Domain ? Application ? Infrastructure ? Host
```

- **Domain**: Pure domain logic, no external dependencies
- **Application**: Use cases, orchestration (MediatR handlers)
- **Infrastructure**: External integrations (HTTP, RabbitMQ, Kafka, gRPC)
- **Contracts**: Plugin developer interfaces

### Coding Standards

- Follow existing code style (no explicit style guide, match surrounding code)
- Use `required` properties for mandatory fields
- Prefer `sealed` classes unless inheritance is needed
- Use nullable reference types (`#nullable enable`)
- Keep methods focused and short
- Avoid comments unless explaining complex logic (self-documenting code preferred)

### Testing

- Write tests for new features
- Use xUnit + Shouldly for assertions
- Use Moq for mocking dependencies
- Follow AAA pattern (Arrange, Act, Assert)
- Tests should be fast and deterministic

Example test structure:
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var input = CreateTestData();
    
    // Act
    var result = SystemUnderTest.Method(input);
    
    // Assert
    result.ShouldBe(expected);
}
```

## Pull Request Process

1. Create a feature branch:
```bash
git checkout -b feature/my-feature
```

2. Make your changes with clear, atomic commits:
```bash
git commit -m "Add feature: brief description"
```

3. Push to your fork:
```bash
git push origin feature/my-feature
```

4. Open a Pull Request with:
   - Clear description of changes
   - Link to related issue (if applicable)
   - Test results
   - Breaking changes (if any)

5. Wait for review and address feedback

### PR Checklist

- [ ] Code builds successfully
- [ ] All tests pass
- [ ] New tests added for new functionality
- [ ] Documentation updated (if needed)
- [ ] No breaking changes (or clearly documented)

## Adding New Protocols

To add a new protocol (like we did with Kafka, gRPC):

1. Add receiver service interface in `Domain/Ports/`
2. Implement receiver in `Infrastructure/MessageBrokers/NewProtocol/`
3. Implement sender in `Infrastructure/MessageBrokers/NewProtocol/`
4. Update `DocketOrchestrationService` to register/unregister
5. Update `ProcessMessageCommandHandler` to populate headers
6. Add example docket in `dockets/examples/`
7. Add tests in `tests/KeryxFlux.Domain.Tests/Orchestration/`

Follow the existing pattern (RabbitMQ, Kafka, gRPC are all structured identically).

## Adding New Plugin Interfaces

To add a new plugin interface type:

1. Define interface in `Contracts/`
2. Create supporting types (like `ModelStep`, `ModelInvocationPlan`)
3. Create handler in `Application/Handlers/`
4. Update routing in `ProcessMessageCommandHandler`
5. Create example plugin in `plugins/`
6. Add tests
7. Update documentation

## Documentation Updates

- Update README.md for major features
- Add example dockets for new capabilities
- Update plugin guides for new interfaces
- Keep examples current

## Release Process

(Maintainer only)

1. Update version in project files
2. Update CHANGELOG
3. Tag release: `git tag v1.0.0`
4. Push tags: `git push origin v1.0.0`
5. Create GitHub release with release notes

## Questions?

Open a GitHub Issue for questions or discussions.

## License

By contributing, you agree that your contributions will be licensed under the Apache License 2.0.

# Verification Notes

## OpenSpec validation

### Command

```bash
openspec validate add-versioned-artifact-json-contracts --strict
```

### Output

```text
Change 'add-versioned-artifact-json-contracts' is valid
```

## Targeted agent contract tests (planner/executor/fix planner + validation gates)

### Command

```bash
dotnet test tests/nirmata.Agents.Tests/nirmata.Agents.Tests.csproj --filter "FullyQualifiedName~ArtifactContractValidatorTests|FullyQualifiedName~TaskExecutorScopeEnforcementTests|FullyQualifiedName~VerifierHandlerRoutingTests|FullyQualifiedName~AtomicGitCommitterHandlerTests|FullyQualifiedName~FixPlannerTests"
```

### Output (summary)

```text
Test summary: total: 69, failed: 0, succeeded: 69, skipped: 0, duration: 1.9s
Build succeeded in 6.6s
```

## Targeted schema contract tests (task-plan + command-proposal)

### Command

```bash
dotnet test tests/nirmata.Aos.Tests/nirmata.Aos.Tests.csproj --filter "FullyQualifiedName~SchemaContractComplianceTests|FullyQualifiedName~ContractValidationTests"
```

### Output (summary)

```text
Test summary: total: 9, failed: 0, succeeded: 9, skipped: 0, duration: 1.0s
Build succeeded in 1.9s
```

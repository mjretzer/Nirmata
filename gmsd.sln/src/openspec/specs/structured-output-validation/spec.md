# structured-output-validation Specification

## Purpose
TBD - created by archiving change finalize-llm-provider. Update Purpose after archive.
## Requirements
### Requirement: Structured output validation is reliable for planners
The system SHALL ensure structured output validation works reliably for all planner workflows.

The validation MUST:
- Enforce JSON Schema validation with strict mode for critical outputs
- Provide clear error messages when validation fails
- Handle schema validation edge cases gracefully
- Maintain acceptable performance for validation operations

#### Scenario: Planner output passes schema validation
- **GIVEN** a planner workflow with a structured output schema
- **WHEN** the LLM returns valid JSON matching the schema
- **THEN** the structured output validation passes without errors
- **AND** the validated output is returned to the planner
- **AND** validation timing is under 50ms for typical schemas

#### Scenario: Invalid planner output triggers helpful error
- **GIVEN** a planner workflow with strict structured output validation
- **WHEN** the LLM returns JSON that doesn't match the required schema
- **THEN** an `LlmProviderException` is thrown with validation details
- **AND** the error message indicates which schema properties failed validation
- **AND** the error includes the actual vs expected structure information

### Requirement: Schema validation handles edge cases robustly
The system SHALL handle structured output validation edge cases without breaking workflows.

The validation MUST:
- Handle empty or null responses appropriately
- Validate against additional properties constraints
- Handle circular reference detection in schemas
- Provide meaningful error messages for complex validation failures

#### Scenario: Empty LLM response is handled gracefully
- **GIVEN** a structured output request is made to the LLM
- **WHEN** the LLM returns an empty or null response
- **THEN** an `LlmProviderException` is thrown indicating empty response
- **AND** the error message includes the schema name that was expected
- **AND** the error is marked as non-retryable if appropriate

#### Scenario: Additional properties are validated correctly
- **GIVEN** a schema with `additionalProperties: false` constraint
- **WHEN** the LLM returns JSON with extra properties not in the schema
- **THEN** validation fails with a clear error message
- **AND** the error indicates which additional properties are not allowed
- **AND** the error suggests removing the extra properties

### Requirement: Validation performance is optimized
The system SHALL optimize structured output validation performance to minimize impact on workflow execution.

The optimization MUST:
- Cache compiled schemas for repeated validation
- Use efficient JSON parsing and validation libraries
- Minimize memory allocation during validation
- Provide performance metrics for monitoring

#### Scenario: Schema compilation is cached for repeated use
- **GIVEN** multiple planner outputs use the same structured output schema
- **WHEN** validation is performed on each output
- **THEN** schema compilation is cached after first use
- **AND** subsequent validations use the cached compiled schema
- **AND** validation performance improves by at least 50% for repeated schemas

#### Scenario: Large schema validation completes efficiently
- **GIVEN** a complex structured output schema with many properties
- **WHEN** validation is performed on a large JSON response
- **THEN** validation completes within acceptable time limits
- **AND** memory usage remains within reasonable bounds
- **AND** validation timing is logged for performance monitoring

### Requirement: Validation errors provide actionable feedback
The system SHALL provide actionable error messages when structured output validation fails.

The error messages MUST:
- Indicate specific schema validation failures
- Include location information for validation errors
- Suggest corrective actions when possible
- Preserve original LLM output for debugging

#### Scenario: Validation error includes specific location
- **GIVEN** a JSON response fails validation due to a property type mismatch
- **WHEN** the validation error is generated
- **THEN** the error message includes the JSON path to the failing property
- **AND** the error indicates the expected vs actual types
- **AND** the error suggests the correct type for the property

#### Scenario: Validation error preserves original output
- **GIVEN** a structured output validation fails
- **WHEN** the error is reported
- **THEN** the original LLM output is included in error context
- **AND** the output is available for debugging and manual inspection
- **AND** sensitive information is redacted if necessary

### Requirement: Schema caching improves validation performance
The system SHALL cache compiled schemas to improve repeated validation performance.

The caching MUST:
- Cache schemas by schema ID or hash
- Reuse cached schemas for identical validation requests
- Invalidate cache when schema definitions change
- Provide cache statistics for monitoring

#### Scenario: Repeated schema validation uses cache
- **GIVEN** the same structured output schema is used multiple times
- **WHEN** validation is performed on different outputs
- **THEN** the schema is compiled once and cached
- **AND** subsequent validations use the cached compiled schema
- **AND** validation performance improves by at least 50% for cached schemas
- **AND** cache hit rate is logged for monitoring


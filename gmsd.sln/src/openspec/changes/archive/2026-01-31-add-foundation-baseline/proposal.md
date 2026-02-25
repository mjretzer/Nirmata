# Change: Foundation baseline for API, Services, Data, DTO, Common

## Why
Feature delivery needs a stable, testable baseline with consistent boundaries, repeatable data setup, and provable runtime behavior. The repository already has a basic API/Services/Data/DTO foundation, so this proposal codifies and extends it without renaming or collapsing existing layers.

## What Changes
- Define capability specs for common primitives, API baseline, data baseline, services baseline, DTO/validation, and quality gates using the current nomenclature and concern separation.
- Establish a working thin-slice endpoint that proves end-to-end flow: API → service → data → SQLite → mapping → response, aligned with existing Project/Step models.
- Standardize error handling, validation, logging, and health checks while preserving current layer boundaries.
- Require unit and integration test harnesses plus CI build/test automation.
- Add missing solution projects from the GMSD structure baseline (Web, AOS, Agents, Windows Service, Windows Service API) without altering existing project names.

## Impact
- Affected specs: `solution-structure`, `common-primitives`, `api-foundation`, `data-foundation`, `services-foundation`, `dto-validation`, `quality-gates`
- Affected code: `Gmsd.Web`, `Gmsd.Aos`, `Gmsd.Agents`, `Gmsd.Windows.Service`, `Gmsd.Windows.Service.Api`, `Gmsd.Api`, `Gmsd.Services`, `Gmsd.Data`, `Gmsd.Data.Dto`, `Gmsd.Common`, solution build/test pipeline

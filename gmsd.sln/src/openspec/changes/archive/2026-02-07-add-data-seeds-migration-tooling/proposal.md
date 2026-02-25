# Change: Add Data Seeds and Migration Tooling

## Why
Fresh database installations need baseline seed data for product entities (Projects) to enable immediate usability. Without seed data, developers must manually create initial records to test features. Additionally, migration tooling should be repeatable and support both forward migrations and rollback scenarios.

## What Changes
- Add EF Core seed data configuration for the `Project` entity
- Create `InitialData.sql` reference file for seed data
- Add migration tooling documentation and helper scripts
- Ensure migrations apply cleanly from baseline to current

## Impact
- Affected specs: `data-foundation`
- Affected code: `Gmsd.Data/Migrations/Seeds/`
- Affected migrations: baseline migration extended with seed data

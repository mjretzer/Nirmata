# nirmata.Api — Developer Setup

REST API for domain/workspace data. This is the **domain API** that the frontend connects to for workspace CRUD, spec/state reads, task data, and run history.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- `dotnet-ef` tool (for migrations): `dotnet tool install --global dotnet-ef`

## Database Setup (SQLite)

The API uses SQLite. The database file lives at `src/nirmata.Data/sqllitedb/nirmata.db` and is created automatically when migrations run.

### Apply migrations

Run from the `src/nirmata.Data` directory:

```bash
cd src/nirmata.Data
dotnet ef database update
```

This creates the SQLite file and applies all migrations in order (schema, seed data, and workspace tables).

### Connection string

The default connection string is configured in `src/nirmata.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source='../nirmata.Data/sqllitedb/nirmata.db'"
  }
}
```

The path is relative to the API's working directory (`src/nirmata.Api/`), so it resolves correctly when you run with `dotnet run` from that directory or via the IDE. Override it in `appsettings.Development.json` or via an environment variable if needed:

```bash
ConnectionStrings__DefaultConnection="Data Source=/absolute/path/to/nirmata.db"
```

## Running the API

```bash
cd src/nirmata.Api
dotnet run
```

The API starts on:
- **HTTP**: `http://localhost:5221`
- **HTTPS**: `https://localhost:7138`

Swagger UI is available at `http://localhost:5221/swagger`.

## Endpoints

| Group | Prefix | Description |
|-------|--------|-------------|
| Health | `/health` | Simple and detailed health checks |
| Workspaces | `/v1/workspaces` | Register, list, update, delete workspaces |
| Workspace spec | `/v1/workspaces/{id}/spec/...` | Milestones, phases, tasks, project spec |
| Workspace files | `/v1/workspaces/{id}/files/{*path}` | Browse files within a registered workspace root |
| Projects | `/v1/projects` | Project CRUD (legacy) |

See the HTTP test file at `nirmata.Api.http` for example requests.

## Frontend Integration

The React frontend (`nirmata.frontend/`) targets this API via the `VITE_DOMAIN_URL` environment variable. Add it to `nirmata.frontend/.env.local`:

```
VITE_DOMAIN_URL=http://localhost:5221
```

See `documents/architecture/data-flow.md` for the full frontend routing rules.

## Adding Migrations

After changing EF Core entities in `nirmata.Data`:

```bash
cd src/nirmata.Data
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

See `src/nirmata.Data/Migrations/README.md` for the full migration workflow.

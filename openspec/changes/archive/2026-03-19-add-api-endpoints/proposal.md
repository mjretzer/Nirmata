# Change: Add API Endpoints

## Why
The frontend currently relies on mock data (`mockData.ts`, `mockHostData.ts`, etc.) and a "swap-point" hook layer (`useAosData.ts`). To enable real functionality, we need to implement the authoritative API endpoints on the backend that the frontend expects. This proposal defines the contract for these APIs based on the current frontend data shapes.

## What Changes
- Defines the **Daemon API** surface (Command execution, Health, Host Profile).
- Defines the **Domain Data API** surface (Workspaces, Tasks, Runs, Issues, etc.).
- Establishes the JSON schemas for requests and responses.

## Impact
- **Affected Specs**: New specs for `daemon-api` and `domain-api`.
- **Affected Code**: 
  - `nirmata.frontend` (eventual swap in `useAosData.ts`).
  - `nirmata.Windows.Service.Api` (implementation of Daemon API).
  - `nirmata.Api` (implementation of Domain Data API).

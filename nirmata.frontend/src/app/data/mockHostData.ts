// Shared type definitions for Host / Daemon / Codebase / Diagnostics pages.
// Runtime mock values have been removed — all data is now sourced from the backend via useAosData.ts hooks.

// ── Host Console ──────────────────────────────────────────────────────

export type ServiceStatus = "running" | "stopped" | "restarting";

export interface ApiSurface {
  name: string;
  path: string;
  ok: boolean;
  latencyMs?: number;
  reason?: string;
}

export interface HostLogLine {
  id: number;
  ts: string;
  level: "info" | "warn" | "error";
  msg: string;
}

// ── Diagnostics ───────────────────────────────────────────────────────

export interface DiagLogEntry {
  label: string;
  lines: number;
  warnings: number;
  errors: number;
  path: string;
}

export interface DiagArtifactEntry {
  name: string;
  size: string;
  type: string;
  path: string;
}

export interface DiagLockEntry {
  id: string;
  scope: string;
  owner: string;
  acquired: string;
  stale: boolean;
}

export interface DiagCacheEntry {
  label: string;
  size: string;
  path: string;
  stale: boolean;
}

// ── Codebase ──────────────────────────────────────────────────────────

export interface CodebaseArtifact {
  id: string;
  name: string;
  type: "intel" | "cache" | "pack";
  description: string;
  status: "ready" | "stale" | "missing" | "error";
  lastUpdated: string;
  size: string;
  path: string;
}

export interface LanguageBreakdown {
  name: string;
  pct: number;
  color: string;
}

export interface StackEntry {
  name: string;
  category: string;
  color: string;
}

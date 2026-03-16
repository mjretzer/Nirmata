/**
 * Mock data for Host / Daemon / Codebase / Diagnostics pages.
 *
 * These are infrastructure-level types that will eventually come from
 * the daemon API. Keeping them here means pages never define their own
 * mock data, and the swap to real APIs happens in useAosData.ts alone.
 */

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

export const mockHostLogs: HostLogLine[] = [
  { id: 1,  ts: "08:14:02", level: "info",  msg: "AOS Engine v2.4.0-alpha starting…" },
  { id: 2,  ts: "08:14:03", level: "info",  msg: "Loading workspace config from .aos/config.json" },
  { id: 3,  ts: "08:14:03", level: "info",  msg: "Agent Manager API listening on :5000" },
  { id: 4,  ts: "08:14:04", level: "info",  msg: "Commands surface ready" },
  { id: 5,  ts: "08:14:04", level: "info",  msg: "Runs surface ready" },
  { id: 6,  ts: "08:14:04", level: "info",  msg: "Health surface ready" },
  { id: 7,  ts: "08:14:05", level: "info",  msg: "Service surface ready" },
  { id: 8,  ts: "08:14:05", level: "info",  msg: "All surfaces operational — engine ready" },
  { id: 9,  ts: "08:21:17", level: "warn",  msg: "Context budget reached 80% on run-042" },
  { id: 10, ts: "08:45:33", level: "info",  msg: "Run run-043 dispatched" },
  { id: 11, ts: "09:02:11", level: "info",  msg: "Run run-043 completed — verification passed" },
  { id: 12, ts: "10:18:44", level: "error", msg: "Lint check failed: 2 errors in src/auth.ts" },
  { id: 13, ts: "10:18:45", level: "warn",  msg: "Run run-044 entering needs-fix state" },
  { id: 14, ts: "12:33:09", level: "info",  msg: "Heartbeat OK — uptime 4h 19m" },
];

export const mockApiSurfaces: ApiSurface[] = [
  { name: "Commands", path: "/api/commands",  ok: true,  latencyMs: 4  },
  { name: "Runs",     path: "/api/runs",      ok: true,  latencyMs: 6  },
  { name: "Service",  path: "/api/service",   ok: true,  latencyMs: 3  },
  { name: "Health",   path: "/api/health",    ok: true,  latencyMs: 2  },
];

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

export const mockDiagLogs: DiagLogEntry[] = [
  { label: "Build log",   lines: 342, warnings: 0, errors: 0, path: ".aos/evidence/run-20260306/build.log" },
  { label: "Test log",    lines: 118, warnings: 2, errors: 0, path: ".aos/evidence/run-20260306/test.log"  },
  { label: "Lint log",    lines: 64,  warnings: 0, errors: 2, path: ".aos/evidence/run-20260306/lint.log"  },
  { label: "Engine log",  lines: 891, warnings: 5, errors: 1, path: ".aos/evidence/run-20260306/engine.log"},
];

export const mockDiagArtifacts: DiagArtifactEntry[] = [
  { name: "spec-snapshot.json",  size: "14 KB", type: "json",   path: ".aos/evidence/run-20260306/spec-snapshot.json"  },
  { name: "state-diff.json",     size: "3 KB",  type: "json",   path: ".aos/evidence/run-20260306/state-diff.json"     },
  { name: "test-results.xml",    size: "28 KB", type: "xml",    path: ".aos/evidence/run-20260306/test-results.xml"    },
  { name: "coverage.html",       size: "82 KB", type: "html",   path: ".aos/evidence/run-20260306/coverage.html"       },
  { name: "lint-report.json",    size: "7 KB",  type: "json",   path: ".aos/evidence/run-20260306/lint-report.json"    },
  { name: "build-manifest.json", size: "2 KB",  type: "json",   path: ".aos/evidence/run-20260306/build-manifest.json" },
  { name: "handoff.json",        size: "1 KB",  type: "json",   path: ".aos/state/handoff.json"                        },
];

export const mockDiagLocks: DiagLockEntry[] = [];  // no active locks — healthy state

export const mockDiagCacheEntries: DiagCacheEntry[] = [
  { label: "Context cache",   size: "14 MB", path: ".aos/cache/context/",   stale: false },
  { label: "Schema cache",    size: "4 MB",  path: ".aos/cache/schemas/",   stale: false },
  { label: "Provider cache",  size: "6 MB",  path: ".aos/cache/providers/", stale: true  },
];

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

export const mockCodebaseArtifacts: CodebaseArtifact[] = [
  { id: "map.json", name: "map.json", type: "intel", description: "Project structure & statistics", status: "ready", lastUpdated: "2 mins ago", size: "12KB", path: ".aos/codebase/map.json" },
  { id: "stack.json", name: "stack.json", type: "intel", description: "Tech stack & dependencies", status: "ready", lastUpdated: "2 mins ago", size: "2KB", path: ".aos/codebase/stack.json" },
  { id: "architecture.json", name: "architecture.json", type: "intel", description: "Design patterns & layers", status: "ready", lastUpdated: "2 mins ago", size: "4KB", path: ".aos/codebase/architecture.json" },
  { id: "structure.json", name: "structure.json", type: "intel", description: "File system organization", status: "ready", lastUpdated: "2 mins ago", size: "8KB", path: ".aos/codebase/structure.json" },
  { id: "symbols.json", name: "symbols.json", type: "intel", description: "Exported symbol index", status: "ready", lastUpdated: "2 mins ago", size: "45KB", path: ".aos/codebase/symbols.json" },
  { id: "file-graph.json", name: "file-graph.json", type: "intel", description: "Dependency graph", status: "ready", lastUpdated: "2 mins ago", size: "120KB", path: ".aos/codebase/file-graph.json" },
  { id: "conventions.json", name: "conventions.json", type: "intel", description: "Linting & style rules", status: "stale", lastUpdated: "2 days ago", size: "1KB", path: ".aos/codebase/conventions.json" },
  { id: "testing.json", name: "testing.json", type: "intel", description: "Test coverage report", status: "missing", lastUpdated: "-", size: "-", path: ".aos/codebase/testing.json" },
  { id: "integrations.json", name: "integrations.json", type: "intel", description: "External API connections", status: "ready", lastUpdated: "2 mins ago", size: "3KB", path: ".aos/codebase/integrations.json" },
  { id: "concerns.json", name: "concerns.json", type: "intel", description: "Risk & debt analysis", status: "missing", lastUpdated: "-", size: "-", path: ".aos/codebase/concerns.json" },
  { id: "context-pack-full", name: "full-repo-context.pack", type: "pack", description: "Complete context pack", status: "ready", lastUpdated: "1 hour ago", size: "1.2MB", path: ".aos/context/full-repo-context.pack" },
];

export const mockLanguages: LanguageBreakdown[] = [
  { name: "TypeScript", pct: 68.4, color: "#3178c6" },
  { name: "CSS",        pct: 12.1, color: "#563d7c" },
  { name: "HTML",       pct:  8.3, color: "#e34c26" },
  { name: "JavaScript", pct:  6.7, color: "#f1e05a" },
  { name: "JSON",       pct:  3.2, color: "#6b7280" },
  { name: "Other",      pct:  1.3, color: "#374151" },
];

export const mockStack: StackEntry[] = [
  { name: "React 18",       category: "Framework",     color: "text-cyan-400 border-cyan-400/20 bg-cyan-400/5" },
  { name: "TypeScript 5",   category: "Language",      color: "text-blue-400 border-blue-400/20 bg-blue-400/5" },
  { name: "Vite 6",         category: "Bundler",       color: "text-purple-400 border-purple-400/20 bg-purple-400/5" },
  { name: "Tailwind CSS 4", category: "Styling",       color: "text-sky-400 border-sky-400/20 bg-sky-400/5" },
  { name: "React Router 7", category: "Routing",       color: "text-orange-400 border-orange-400/20 bg-orange-400/5" },
  { name: "Radix UI",       category: "Components",    color: "text-indigo-400 border-indigo-400/20 bg-indigo-400/5" },
  { name: "Recharts",       category: "Visualization", color: "text-green-400 border-green-400/20 bg-green-400/5" },
  { name: "Sonner",         category: "Notifications", color: "text-yellow-400 border-yellow-400/20 bg-yellow-400/5" },
];

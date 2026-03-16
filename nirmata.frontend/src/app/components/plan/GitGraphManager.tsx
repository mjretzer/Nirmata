import { useState, useMemo, useCallback, useRef, useEffect } from "react";
import {
  GitBranch,
  GitMerge,
  GitCommit as GitCommitIcon,
  Search,
  Filter,
  Tag,
  FileCode,
  RotateCcw,
  AlertTriangle,
  Shield,
  User,
  Clock,
  ArrowUpRight,
  Diff,
  X,
  ZoomIn,
} from "lucide-react";
import { Button } from "../ui/button";
import { cn } from "../ui/utils";
import { copyToClipboard } from "../../utils/clipboard";
import { useTaskHighlight } from "./TaskHighlightContext";
import { useTasks } from "../../hooks/useAosData";

// ─── Types ────────────────────────────────────────────────────────

// Debug overlays — activity evidence attached to existing commits, NOT separate nodes
interface DebugOverlay {
  kind: "uat" | "repro" | "trace" | "bisect" | "perf";
  label: string;
  status: "pass" | "fail" | "info";
}

interface Commit {
  sha: string;
  shortSha: string;
  subject: string;
  author: string;
  date: string;
  relDate: string;
  parents: string[];
  lane: number;
  isMerge: boolean;
  gpgStatus: "good" | "none" | "bad";
  refs: Ref[];
  changedFiles: ChangedFile[];
  /** Debug activity overlays — evidence of debugging, not extra commits */
  debugOverlays?: DebugOverlay[];
  /** True if this commit is temporary debug instrumentation (dbg/* branch) */
  isDebugInstrumentation?: boolean;
}

interface Ref {
  name: string;
  type: "local" | "remote" | "tag" | "head";
  upstream?: { ahead: number; behind: number };
  isCurrent?: boolean;
}

interface ChangedFile {
  path: string;
  status: "modified" | "added" | "deleted" | "renamed";
  additions: number;
  deletions: number;
}

// (Lane colors are defined inline in the SVG graph section as COLORS[])

// ─── Graph display mode ───────────────────────────────────────────

type GraphMode = "simple" | "branch";
type ZoomLevel = "milestone" | "phase" | "task";

// ─── Mock DAG Data ────────────────────────────────────────────────

// **Branch mode** — full gitflow: tsk/* branches + merge-back commits
// **Simple mode** — flat: tasks committed directly on ph/* lane, no tsk/* branches

function generateBranchDAG(): Commit[] {
  // Newest-first order (top of log = most recent)
  const commits: Commit[] = [
    // ── TSK-000012 in-progress (HEAD) ──────────────────────
    {
      sha: "4ae6f8c2d1b3a5e7f9c8d2a4b6e8f0c1d3a5b7e9",
      shortSha: "4ae6f8c",
      subject: "feat(auth): implement OAuth login flow [TSK-000012]",
      author: "Executor-Alpha",
      date: "2026-03-02T14:22:00Z",
      relDate: "2h ago",
      parents: ["39d5e7b3"],
      lane: 2,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "tsk/TSK-000012", type: "local", isCurrent: true, upstream: { ahead: 1, behind: 0 } },
        { name: "HEAD", type: "head" },
      ],
      changedFiles: [
        { path: "src/auth/oauth.ts", status: "added", additions: 124, deletions: 0 },
        { path: "src/auth/providers.ts", status: "added", additions: 67, deletions: 0 },
        { path: "src/auth/callback.ts", status: "added", additions: 43, deletions: 0 },
      ],
      debugOverlays: [
        { kind: "repro", label: "Repro ✓", status: "pass" },
        { kind: "trace", label: "Trace captured", status: "info" },
      ],
    },
    // ── dbg/* temporary instrumentation (to be reverted) ──
    {
      sha: "5bf7e9d3c2a4b6e8f0d1c3a5b7e9f1d3a5c7e9b2",
      shortSha: "5bf7e9d",
      subject: "dbg: add verbose OAuth trace logging",
      author: "Executor-Alpha",
      date: "2026-03-02T13:45:00Z",
      relDate: "3h ago",
      parents: ["39d5e7b3"],
      lane: 2,
      isMerge: false,
      gpgStatus: "none",
      refs: [
        { name: "dbg/ISS-0042-oauth-trace", type: "local" },
      ],
      changedFiles: [
        { path: "src/auth/oauth.ts", status: "modified", additions: 18, deletions: 0 },
        { path: "src/auth/debug-trace.ts", status: "added", additions: 34, deletions: 0 },
      ],
      isDebugInstrumentation: true,
      debugOverlays: [
        { kind: "trace", label: "Trace active", status: "info" },
      ],
    },
    // ── PH-0002 branch point ──────────────────────────────
    {
      sha: "39d5e7b3a1c4f6e8d2b5a7c9e1f3d5b7a9c2e4f6",
      shortSha: "39d5e7b",
      subject: "plan: scaffold PH-0002 phase spec [User Authentication]",
      author: "aos-agent",
      date: "2026-03-02T11:05:00Z",
      relDate: "5h ago",
      parents: ["28c4f6a1"],
      lane: 1,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "ph/PH-0002", type: "local", upstream: { ahead: 1, behind: 0 } },
      ],
      changedFiles: [
        { path: ".aos/spec/phases/PH-0002/phase.json", status: "added", additions: 52, deletions: 0 },
        { path: ".aos/spec/phases/PH-0002/tasks.json", status: "added", additions: 34, deletions: 0 },
      ],
    },
    // ── PH-0001 merged into MS-0001 ──────────────────────
    {
      sha: "28c4f6a1e3d5b7f9a2c4e6d8b1a3c5e7f9d2b4a6",
      shortSha: "28c4f6a",
      subject: "Merge ph/PH-0001 into ms/MS-0001",
      author: "aos-agent",
      date: "2026-03-02T10:58:00Z",
      relDate: "5h ago",
      parents: ["b3c7a9e1", "17b3e5f2"],
      lane: 0,
      isMerge: true,
      gpgStatus: "good",
      refs: [
        { name: "ms/MS-0001", type: "local", upstream: { ahead: 3, behind: 0 } },
        { name: "origin/ms/MS-0001", type: "remote" },
        { name: "v0.1.0-alpha", type: "tag" },
      ],
      changedFiles: [],
    },
    // ── TSK-000009 merged into PH-0001 ───────────────────
    {
      sha: "17b3e5f2a9c1d3e5b7f9a2c4d6e8f1b3a5c7e9d2",
      shortSha: "17b3e5f",
      subject: "Merge tsk/TSK-000009 into ph/PH-0001",
      author: "aos-agent",
      date: "2026-03-02T10:45:00Z",
      relDate: "6h ago",
      parents: ["e2f8b6d4", "f4a1d9c7"],
      lane: 1,
      isMerge: true,
      gpgStatus: "good",
      refs: [],
      changedFiles: [],
    },
    // ── TSK-000009 task commit ────────────────────────────
    {
      sha: "f4a1d9c7e3b5a7f9c1d3e5b8a2c4f6d8e1b3a5c7",
      shortSha: "f4a1d9c",
      subject: "feat(ci): configure CI/CD pipeline [TSK-000009]",
      author: "Executor-Beta",
      date: "2026-03-02T09:30:00Z",
      relDate: "7h ago",
      parents: ["e2f8b6d4"],
      lane: 2,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "tsk/TSK-000009", type: "local" },
      ],
      changedFiles: [
        { path: ".github/workflows/ci.yml", status: "added", additions: 86, deletions: 0 },
        { path: ".github/workflows/deploy.yml", status: "added", additions: 54, deletions: 0 },
        { path: "scripts/build.sh", status: "added", additions: 22, deletions: 0 },
      ],
      debugOverlays: [
        { kind: "uat", label: "UAT ✓", status: "pass" },
        { kind: "perf", label: "Perf 12ms", status: "pass" },
      ],
    },
    // ── TSK-000008 merged into PH-0001 ───────────────────
    {
      sha: "e2f8b6d4a1c3e5f7b9d2a4c6e8f1b3d5a7c9e2f4",
      shortSha: "e2f8b6d",
      subject: "Merge tsk/TSK-000008 into ph/PH-0001",
      author: "aos-agent",
      date: "2026-03-01T23:15:00Z",
      relDate: "17h ago",
      parents: ["c5d2f4b8", "d7e9c1a3"],
      lane: 1,
      isMerge: true,
      gpgStatus: "good",
      refs: [],
      changedFiles: [],
    },
    // ── TSK-000008 task commit ────────────────────────────
    {
      sha: "d7e9c1a3b5f7e9d2a4c6b8f1a3c5e7d9b2a4c6e8",
      shortSha: "d7e9c1a",
      subject: "feat(core): set up Vite project structure [TSK-000008]",
      author: "Executor-Alpha",
      date: "2026-03-01T21:40:00Z",
      relDate: "18h ago",
      parents: ["c5d2f4b8"],
      lane: 2,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "tsk/TSK-000008", type: "local" },
      ],
      changedFiles: [
        { path: "vite.config.ts", status: "added", additions: 32, deletions: 0 },
        { path: "tsconfig.json", status: "added", additions: 28, deletions: 0 },
        { path: "src/index.ts", status: "added", additions: 12, deletions: 0 },
        { path: "src/app/App.tsx", status: "added", additions: 18, deletions: 0 },
      ],
      debugOverlays: [
        { kind: "bisect", label: "Bisect step 3", status: "info" },
        { kind: "uat", label: "UAT ✓", status: "pass" },
      ],
    },
    // ── PH-0001 branch point ──────────────────────────────
    {
      sha: "c5d2f4b8a9e1c3d5f7b9a2c4e6d8f1a3b5c7e9d2",
      shortSha: "c5d2f4b",
      subject: "plan: scaffold PH-0001 phase spec [Plan Page UI]",
      author: "aos-agent",
      date: "2026-03-01T20:10:00Z",
      relDate: "20h ago",
      parents: ["b3c7a9e1"],
      lane: 1,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "ph/PH-0001", type: "local" },
      ],
      changedFiles: [
        { path: ".aos/spec/phases/PH-0001/phase.json", status: "added", additions: 48, deletions: 0 },
        { path: ".aos/spec/phases/PH-0001/tasks.json", status: "added", additions: 28, deletions: 0 },
      ],
    },
    // ── MS-0001 branch point ──────────────────────────────
    {
      sha: "b3c7a9e1d5f2a4c6e8b1d3a5c7f9e2b4d6a8c1e3",
      shortSha: "b3c7a9e",
      subject: "chore: initialize .aos spec directory [MS-0001]",
      author: "aos-agent",
      date: "2026-03-01T18:30:00Z",
      relDate: "22h ago",
      parents: ["a1f0e8d2"],
      lane: 0,
      isMerge: false,
      gpgStatus: "good",
      refs: [],
      changedFiles: [
        { path: ".aos/spec/roadmap.json", status: "added", additions: 95, deletions: 0 },
        { path: ".aos/spec/project.json", status: "added", additions: 42, deletions: 0 },
        { path: ".aos/state/cursor.json", status: "added", additions: 8, deletions: 0 },
      ],
    },
    // ── Initial commit (root) ─────────────────────────────
    {
      sha: "a1f0e8d2b4c6a8e1d3f5b7c9a2e4d6f8b1c3a5e7",
      shortSha: "a1f0e8d",
      subject: "Initial commit: project bootstrap",
      author: "aos-agent",
      date: "2026-03-01T16:00:00Z",
      relDate: "1d ago",
      parents: [],
      lane: 0,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "main", type: "local", upstream: { ahead: 0, behind: 0 } },
        { name: "origin/main", type: "remote" },
      ],
      changedFiles: [
        { path: "package.json", status: "added", additions: 35, deletions: 0 },
        { path: "README.md", status: "added", additions: 20, deletions: 0 },
        { path: ".gitignore", status: "added", additions: 12, deletions: 0 },
      ],
    },
  ];
  return commits;
}

// ── Simple mode: tasks commit directly on the phase lane ──────────
// No tsk/* branches, no per-task merge commits.
// Produces a clean 2-lane graph: Milestone | Phase (tasks inline).
function generateSimpleDAG(): Commit[] {
  return [
    // ── TSK-000012 in-progress (HEAD) — on phase lane ─────
    {
      sha: "4ae6f8c2d1b3a5e7f9c8d2a4b6e8f0c1d3a5b7e9",
      shortSha: "4ae6f8c",
      subject: "feat(auth): implement OAuth login flow [TSK-000012]",
      author: "Executor-Alpha",
      date: "2026-03-02T14:22:00Z",
      relDate: "2h ago",
      parents: ["39d5e7b3"],
      lane: 1,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "ph/PH-0002", type: "local", isCurrent: true, upstream: { ahead: 2, behind: 0 } },
        { name: "HEAD", type: "head" },
      ],
      changedFiles: [
        { path: "src/auth/oauth.ts", status: "added", additions: 124, deletions: 0 },
        { path: "src/auth/providers.ts", status: "added", additions: 67, deletions: 0 },
        { path: "src/auth/callback.ts", status: "added", additions: 43, deletions: 0 },
      ],
      debugOverlays: [
        { kind: "repro", label: "Repro ✓", status: "pass" },
        { kind: "trace", label: "Trace captured", status: "info" },
      ],
    },
    // ── PH-0002 scaffold ──────────────────────────────────
    {
      sha: "39d5e7b3a1c4f6e8d2b5a7c9e1f3d5b7a9c2e4f6",
      shortSha: "39d5e7b",
      subject: "plan: scaffold PH-0002 phase spec [User Authentication]",
      author: "aos-agent",
      date: "2026-03-02T11:05:00Z",
      relDate: "5h ago",
      parents: ["28c4f6a1"],
      lane: 1,
      isMerge: false,
      gpgStatus: "good",
      refs: [],
      changedFiles: [
        { path: ".aos/spec/phases/PH-0002/phase.json", status: "added", additions: 52, deletions: 0 },
        { path: ".aos/spec/phases/PH-0002/tasks.json", status: "added", additions: 34, deletions: 0 },
      ],
    },
    // ── PH-0001 merged into MS-0001 (phase→ms merge kept) ─
    {
      sha: "28c4f6a1e3d5b7f9a2c4e6d8b1a3c5e7f9d2b4a6",
      shortSha: "28c4f6a",
      subject: "Merge ph/PH-0001 into ms/MS-0001",
      author: "aos-agent",
      date: "2026-03-02T10:58:00Z",
      relDate: "5h ago",
      parents: ["b3c7a9e1", "f4a1d9c7"],
      lane: 0,
      isMerge: true,
      gpgStatus: "good",
      refs: [
        { name: "ms/MS-0001", type: "local", upstream: { ahead: 3, behind: 0 } },
        { name: "origin/ms/MS-0001", type: "remote" },
        { name: "v0.1.0-alpha", type: "tag" },
      ],
      changedFiles: [],
    },
    // ── TSK-000009 — directly on PH-0001 lane ────────────
    {
      sha: "f4a1d9c7e3b5a7f9c1d3e5b8a2c4f6d8e1b3a5c7",
      shortSha: "f4a1d9c",
      subject: "feat(ci): configure CI/CD pipeline [TSK-000009]",
      author: "Executor-Beta",
      date: "2026-03-02T09:30:00Z",
      relDate: "7h ago",
      parents: ["d7e9c1a3"],
      lane: 1,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "ph/PH-0001", type: "local" },
      ],
      changedFiles: [
        { path: ".github/workflows/ci.yml", status: "added", additions: 86, deletions: 0 },
        { path: ".github/workflows/deploy.yml", status: "added", additions: 54, deletions: 0 },
        { path: "scripts/build.sh", status: "added", additions: 22, deletions: 0 },
      ],
      debugOverlays: [
        { kind: "uat", label: "UAT ✓", status: "pass" },
        { kind: "perf", label: "Perf 12ms", status: "pass" },
      ],
    },
    // ── TSK-000008 — directly on PH-0001 lane ────────────
    {
      sha: "d7e9c1a3b5f7e9d2a4c6b8f1a3c5e7d9b2a4c6e8",
      shortSha: "d7e9c1a",
      subject: "feat(core): set up Vite project structure [TSK-000008]",
      author: "Executor-Alpha",
      date: "2026-03-01T21:40:00Z",
      relDate: "18h ago",
      parents: ["c5d2f4b8"],
      lane: 1,
      isMerge: false,
      gpgStatus: "good",
      refs: [],
      changedFiles: [
        { path: "vite.config.ts", status: "added", additions: 32, deletions: 0 },
        { path: "tsconfig.json", status: "added", additions: 28, deletions: 0 },
        { path: "src/index.ts", status: "added", additions: 12, deletions: 0 },
        { path: "src/app/App.tsx", status: "added", additions: 18, deletions: 0 },
      ],
      debugOverlays: [
        { kind: "bisect", label: "Bisect step 3", status: "info" },
        { kind: "uat", label: "UAT ✓", status: "pass" },
      ],
    },
    // ── PH-0001 scaffold ──────────────────────────────────
    {
      sha: "c5d2f4b8a9e1c3d5f7b9a2c4e6d8f1a3b5c7e9d2",
      shortSha: "c5d2f4b",
      subject: "plan: scaffold PH-0001 phase spec [Plan Page UI]",
      author: "aos-agent",
      date: "2026-03-01T20:10:00Z",
      relDate: "20h ago",
      parents: ["b3c7a9e1"],
      lane: 1,
      isMerge: false,
      gpgStatus: "good",
      refs: [],
      changedFiles: [
        { path: ".aos/spec/phases/PH-0001/phase.json", status: "added", additions: 48, deletions: 0 },
        { path: ".aos/spec/phases/PH-0001/tasks.json", status: "added", additions: 28, deletions: 0 },
      ],
    },
    // ── MS-0001 init ──────────────────────────────────────
    {
      sha: "b3c7a9e1d5f2a4c6e8b1d3a5c7f9e2b4d6a8c1e3",
      shortSha: "b3c7a9e",
      subject: "chore: initialize .aos spec directory [MS-0001]",
      author: "aos-agent",
      date: "2026-03-01T18:30:00Z",
      relDate: "22h ago",
      parents: ["a1f0e8d2"],
      lane: 0,
      isMerge: false,
      gpgStatus: "good",
      refs: [],
      changedFiles: [
        { path: ".aos/spec/roadmap.json", status: "added", additions: 95, deletions: 0 },
        { path: ".aos/spec/project.json", status: "added", additions: 42, deletions: 0 },
        { path: ".aos/state/cursor.json", status: "added", additions: 8, deletions: 0 },
      ],
    },
    // ── Initial commit (root) ─────────────────────────────
    {
      sha: "a1f0e8d2b4c6a8e1d3f5b7c9a2e4d6f8b1c3a5e7",
      shortSha: "a1f0e8d",
      subject: "Initial commit: project bootstrap",
      author: "aos-agent",
      date: "2026-03-01T16:00:00Z",
      relDate: "1d ago",
      parents: [],
      lane: 0,
      isMerge: false,
      gpgStatus: "good",
      refs: [
        { name: "main", type: "local", upstream: { ahead: 0, behind: 0 } },
        { name: "origin/main", type: "remote" },
      ],
      changedFiles: [
        { path: "package.json", status: "added", additions: 35, deletions: 0 },
        { path: "README.md", status: "added", additions: 20, deletions: 0 },
        { path: ".gitignore", status: "added", additions: 12, deletions: 0 },
      ],
    },
  ];
}

// Mock working tree status — reflects TSK-000012 in-progress
const mockWorkingTree = {
  staged: [
    { path: "src/auth/session.ts", status: "added" as const },
  ],
  unstaged: [
    { path: "src/auth/providers.ts", status: "modified" as const },
    { path: "src/auth/middleware.ts", status: "added" as const },
  ],
  untracked: ["src/auth/oauth.test.ts"],
};

const BRANCHES_SIMPLE = [
  "main",
  "ms/MS-0001",
  "ph/PH-0001",
  "ph/PH-0002",
];

const BRANCHES_BRANCH = [
  "main",
  "ms/MS-0001",
  "ph/PH-0001",
  "ph/PH-0002",
  "tsk/TSK-000008",
  "tsk/TSK-000009",
  "tsk/TSK-000012",
  "dbg/ISS-0042-oauth-trace",
];

// ─── Sub-components ───────────────────────────────────────────────

function RefBadge({ gitRef }: { gitRef: Ref }) {
  if (gitRef.type === "head") {
    return (
      <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
        HEAD
      </span>
    );
  }
  if (gitRef.type === "tag") {
    return (
      <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono bg-amber-500/10 text-amber-400 border border-amber-500/20">
        <Tag className="h-2.5 w-2.5" />{gitRef.name}
      </span>
    );
  }
  if (gitRef.type === "remote") {
    return (
      <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono bg-rose-500/10 text-rose-400/80 border border-rose-500/20">
        {gitRef.name}
      </span>
    );
  }
  return (
    <span className={cn(
      "inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono border",
      gitRef.isCurrent
        ? "bg-emerald-500/15 text-emerald-400 border-emerald-500/30 font-bold"
        : "bg-blue-500/10 text-blue-400/80 border-blue-500/20"
    )}>
      <GitBranch className="h-2.5 w-2.5" />
      {gitRef.name}
      {gitRef.upstream && (
        <span className="text-muted-foreground/60 ml-0.5">
          {gitRef.upstream.ahead > 0 && <span className="text-emerald-500/70">{gitRef.upstream.ahead}↑</span>}
          {gitRef.upstream.behind > 0 && <span className="text-rose-400/70 ml-0.5">{gitRef.upstream.behind}↓</span>}
          {gitRef.upstream.ahead === 0 && gitRef.upstream.behind === 0 && <span className="text-muted-foreground/40">✓</span>}
        </span>
      )}
    </span>
  );
}

function FileStatusIcon({ status }: { status: string }) {
  if (status === "added") return <span className="text-emerald-400 font-mono text-[10px] font-bold">A</span>;
  if (status === "modified") return <span className="text-amber-400 font-mono text-[10px] font-bold">M</span>;
  if (status === "deleted") return <span className="text-red-400 font-mono text-[10px] font-bold">D</span>;
  if (status === "renamed") return <span className="text-blue-400 font-mono text-[10px] font-bold">R</span>;
  return null;
}

// ─── Main Component ───────────────────────────────────────────────

export function GitGraphManager() {
  const { tasks: allTasks } = useTasks();
  const [graphMode, setGraphMode] = useState<GraphMode>("simple");
  const [zoomLevel, setZoomLevel] = useState<ZoomLevel>("task");
  const commits = useMemo(
    () => (graphMode === "simple" ? generateSimpleDAG() : generateBranchDAG()) ?? [],
    [graphMode]
  );
  const { highlightedTaskId, highlightSource, highlightFromGraph } = useTaskHighlight();
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const commitRowRefs = useRef<Map<string, SVGGElement>>(new Map());

  // Extract task ID from commit subject or branch refs: [TSK-XXXXXX] or tsk/TSK-XXXXXX
  const extractTaskId = useCallback((commit: Commit): string | null => {
    // Check subject for [TSK-XXXXXX]
    const subjMatch = commit.subject.match(/\[TSK-(\d+)\]/);
    if (subjMatch) return `TSK-${subjMatch[1]}`;
    // Check refs for tsk/TSK-XXXXXX
    for (const r of commit.refs) {
      const refMatch = r.name.match(/^tsk\/(TSK-\d+)$/);
      if (refMatch) return refMatch[1];
    }
    // Check merge subjects: Merge tsk/TSK-XXXXXX
    const mergeMatch = commit.subject.match(/tsk\/(TSK-\d+)/);
    if (mergeMatch) return mergeMatch[1];
    return null;
  }, []);

  // Build a map: commit SHA → task ID for all commits that reference a task
  const commitTaskMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const c of commits) {
      const tid = extractTaskId(c);
      if (tid) map.set(c.sha, tid);
    }
    return map;
  }, [commits, extractTaskId]);

  // Build a map: task ID → task status from tasks hook
  const taskStatusMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const t of allTasks) {
      map.set(t.id, t.status);
    }
    return map;
  }, [allTasks]);

  // State
  const [selectedSha, setSelectedSha] = useState<string | null>(null);
  const [compareA, setCompareA] = useState<string | null>(null);
  const [compareB, setCompareB] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [showMerges, setShowMerges] = useState(true);
  const [branchFilter, setBranchFilter] = useState<string>("all");
  const [showFilters, setShowFilters] = useState(false);
  const [showWorkTree, setShowWorkTree] = useState(false);
  const [copiedSha, setCopiedSha] = useState<string | null>(null);

  const selectedCommit = commits.find(c => c.sha === selectedSha) ?? null;
  const isCompareMode = compareA !== null;

  const filteredCommits = useMemo(() => {
    let result = commits;

    // Zoom-level filtering
    if (zoomLevel === "milestone") {
      // Only show milestone-lane commits (lane 0) and phase→milestone merges
      result = result.filter(c =>
        c.lane === 0 || (c.isMerge && c.subject.match(/into\s+ms\//i))
      );
    } else if (zoomLevel === "phase") {
      // Show milestone + phase lane commits, hide topic-lane task commits
      // unless they are the current HEAD or highlighted
      result = result.filter(c =>
        c.lane <= 1 ||
        (c.isMerge && c.lane === 1) ||
        c.refs.some(r => r.type === "head") ||
        commitTaskMap.get(c.sha) === highlightedTaskId
      );
    }
    // zoomLevel === "task" → show everything (default)

    if (!showMerges) result = result.filter(c => !c.isMerge);
    if (searchQuery) {
      const q = searchQuery.toLowerCase();
      result = result.filter(c =>
        c.subject.toLowerCase().includes(q) ||
        c.shortSha.includes(q) ||
        c.author.toLowerCase().includes(q)
      );
    }
    return result;
  }, [commits, showMerges, searchQuery, zoomLevel, commitTaskMap, highlightedTaskId]);

  // ── Scroll-into-view when roadmap highlights a task ──
  useEffect(() => {
    if (!highlightedTaskId || highlightSource !== "roadmap") return;
    // Find the commit SHA for the highlighted task
    let targetSha: string | null = null;
    for (const [sha, tid] of commitTaskMap.entries()) {
      if (tid === highlightedTaskId) { targetSha = sha; break; }
    }
    if (!targetSha) return;
    const gEl = commitRowRefs.current.get(targetSha);
    const container = scrollContainerRef.current;
    if (gEl && container) {
      requestAnimationFrame(() => {
        // Get the bounding box of the SVG <g> element relative to the SVG
        const bbox = gEl.getBoundingClientRect();
        const contRect = container.getBoundingClientRect();
        const targetCenter = bbox.top + bbox.height / 2;
        const containerCenter = contRect.top + contRect.height / 2;
        const scrollDelta = targetCenter - containerCenter;
        container.scrollBy({ top: scrollDelta, behavior: "smooth" });
      });
    }
  }, [highlightedTaskId, highlightSource, commitTaskMap]);

  const handleCopySha = useCallback((sha: string) => {
    copyToClipboard(sha);
    setCopiedSha(sha);
    setTimeout(() => setCopiedSha(null), 1500);
  }, []);

  const handleCommitClick = useCallback((sha: string) => {
    if (isCompareMode) {
      if (!compareB) {
        setCompareB(sha);
      } else {
        setCompareA(sha);
        setCompareB(null);
      }
    } else {
      setSelectedSha(prev => prev === sha ? null : sha);
    }
    // Also trigger task highlight from graph
    const taskId = commitTaskMap.get(sha) ?? null;
    highlightFromGraph(taskId);
  }, [isCompareMode, compareB, commitTaskMap, highlightFromGraph]);

  const compareRange = useMemo(() => {
    if (!compareA || !compareB) return null;
    const idxA = commits.findIndex(c => c.sha === compareA);
    const idxB = commits.findIndex(c => c.sha === compareB);
    if (idxA < 0 || idxB < 0) return null;
    const [start, end] = idxA < idxB ? [idxA, idxB] : [idxB, idxA];
    return {
      commits: commits.slice(start, end + 1),
      additions: commits.slice(start, end + 1).reduce((s, c) => s + c.changedFiles.reduce((a, f) => a + f.additions, 0), 0),
      deletions: commits.slice(start, end + 1).reduce((s, c) => s + c.changedFiles.reduce((a, f) => a + f.deletions, 0), 0),
    };
  }, [compareA, compareB, commits]);

  return (
    <div className="flex flex-col h-full">
      {/* Toolbar */}
      <div className="shrink-0 border-b border-border/20 px-3 py-2 space-y-2">
        {/* Search row */}
        <div className="flex items-center gap-2">
          <div className="flex-1 relative">
            <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3 w-3 text-muted-foreground/50" />
            <input
              type="text"
              placeholder="Search SHA, message, author..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full h-7 pl-7 pr-2 text-[11px] font-mono bg-muted/20 border border-border/30 rounded text-foreground placeholder:text-muted-foreground/40 focus:outline-none focus:border-emerald-500/40 focus:ring-1 focus:ring-emerald-500/20"
            />
          </div>
          <Button
            variant="ghost"
            size="sm"
            className={cn("h-7 w-7 p-0", showFilters && "bg-emerald-500/10 text-emerald-400")}
            onClick={() => setShowFilters(!showFilters)}
          >
            <Filter className="h-3 w-3" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className={cn("h-7 w-7 p-0", showWorkTree && "bg-amber-500/10 text-amber-400")}
            onClick={() => setShowWorkTree(!showWorkTree)}
            title="Working tree"
          >
            <FileCode className="h-3 w-3" />
          </Button>
        </div>

        {/* Mode toggle: Simple ↔ Branch */}
        <div className="flex items-center gap-1 px-0.5">
          <div className="flex items-center h-7 rounded-md bg-muted/20 border border-border/30 p-0.5">
            <button
              onClick={() => { setGraphMode("simple"); setSelectedSha(null); setCompareA(null); setCompareB(null); }}
              className={cn(
                "flex items-center gap-1 px-2.5 h-6 rounded text-[10px] font-mono transition-all",
                graphMode === "simple"
                  ? "bg-emerald-500/15 text-emerald-400 shadow-sm shadow-emerald-500/10"
                  : "text-muted-foreground/50 hover:text-muted-foreground/80"
              )}
            >
              <GitCommitIcon className="h-2.5 w-2.5" />
              Simple
            </button>
            <button
              onClick={() => { setGraphMode("branch"); setSelectedSha(null); setCompareA(null); setCompareB(null); }}
              className={cn(
                "flex items-center gap-1 px-2.5 h-6 rounded text-[10px] font-mono transition-all",
                graphMode === "branch"
                  ? "bg-purple-500/15 text-purple-400 shadow-sm shadow-purple-500/10"
                  : "text-muted-foreground/50 hover:text-muted-foreground/80"
              )}
            >
              <GitBranch className="h-2.5 w-2.5" />
              Branch
            </button>
          </div>
          <span className="text-[9px] font-mono text-muted-foreground/30 ml-1">
            {graphMode === "simple" ? "flat DAG — tasks commit directly on feature lane" : "gitflow — topic branches with merge-back commits"}
          </span>
        </div>

        {/* Zoom level: Milestone / Phase / Task */}
        

        {/* Filter row */}
        {showFilters && (
          <div className="flex items-center gap-2 flex-wrap">
            <select
              value={branchFilter}
              onChange={(e) => setBranchFilter(e.target.value)}
              className="h-6 px-2 text-[10px] font-mono bg-muted/20 border border-border/30 rounded text-foreground focus:outline-none focus:border-emerald-500/40 appearance-none cursor-pointer"
            >
              <option value="all">All branches</option>
              {(graphMode === "simple" ? BRANCHES_SIMPLE : BRANCHES_BRANCH).map(b => <option key={b} value={b}>{b}</option>)}
            </select>
            <button
              onClick={() => setShowMerges(!showMerges)}
              className={cn(
                "flex items-center gap-1 px-2 h-6 text-[10px] font-mono rounded border transition-colors",
                showMerges
                  ? "bg-purple-500/10 text-purple-400 border-purple-500/20"
                  : "bg-muted/20 text-muted-foreground/50 border-border/30"
              )}
            >
              <GitMerge className="h-2.5 w-2.5" />
              {showMerges ? "Merges shown" : "Merges hidden"}
            </button>
            {isCompareMode ? (
              <button
                onClick={() => { setCompareA(null); setCompareB(null); }}
                className="flex items-center gap-1 px-2 h-6 text-[10px] font-mono rounded border bg-rose-500/10 text-rose-400/50 hover:text-rose-400 hover:border-rose-500/40"
              >
                <X className="h-2.5 w-2.5" />
                Exit compare
              </button>
            ) : (
              <button
                onClick={() => { setCompareA(selectedSha); setCompareB(null); setSelectedSha(null); }}
                className={cn(
                  "flex items-center gap-1 px-2 h-6 text-[10px] font-mono rounded border transition-colors",
                  "bg-muted/20 text-muted-foreground/50 border-border/30 hover:text-foreground hover:border-border/60",
                  !selectedSha && "opacity-40 pointer-events-none"
                )}
              >
                <Diff className="h-2.5 w-2.5" />
                Compare from selected
              </button>
            )}
          </div>
        )}

        {/* Compare range banner */}
        {isCompareMode && (
          <div className="flex items-center gap-2 px-2 py-1.5 rounded bg-blue-500/5 border border-blue-500/20 text-[10px] font-mono">
            <Diff className="h-3 w-3 text-blue-400 shrink-0" />
            <span className="text-blue-400">
              {compareA ? commits.find(c => c.sha === compareA)?.shortSha : "..."}{" → "}
              {compareB ? commits.find(c => c.sha === compareB)?.shortSha : <span className="text-muted-foreground/60">click a commit</span>}
            </span>
            {compareRange && (
              <span className="ml-auto text-muted-foreground/60">
                {compareRange.commits.length} commits · <span className="text-emerald-400">+{compareRange.additions}</span> <span className="text-rose-400">-{compareRange.deletions}</span>
              </span>
            )}
          </div>
        )}
      </div>

      {/* Working tree (collapsible) */}
      {showWorkTree && (
        <div className="shrink-0 border-b border-border/20 px-3 py-2 space-y-1.5">
          <div className="flex items-center gap-2 text-[10px] font-mono text-amber-400/80">
            <AlertTriangle className="h-3 w-3" />
            <span>Working tree — {mockWorkingTree.staged.length} staged · {mockWorkingTree.unstaged.length} unstaged · {mockWorkingTree.untracked.length} untracked</span>
          </div>
          <div className="space-y-0.5">
            {mockWorkingTree.staged.map(f => (
              <div key={f.path} className="flex items-center gap-2 text-[10px] font-mono pl-2 py-0.5 rounded hover:bg-emerald-500/5">
                <span className="text-emerald-400 w-3 text-center">S</span>
                <FileStatusIcon status={f.status} />
                <span className="text-muted-foreground/80 truncate">{f.path}</span>
              </div>
            ))}
            {mockWorkingTree.unstaged.map(f => (
              <div key={f.path} className="flex items-center gap-2 text-[10px] font-mono pl-2 py-0.5 rounded hover:bg-amber-500/5">
                <span className="text-amber-400/60 w-3 text-center">U</span>
                <FileStatusIcon status={f.status} />
                <span className="text-muted-foreground/60 truncate">{f.path}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Vertical Git Graph (gitflow-style) */}
      <div className="flex-1 overflow-auto" ref={scrollContainerRef}>
        {filteredCommits.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-muted-foreground/40 text-xs font-mono">
            No commits match filter
          </div>
        ) : (() => {
          // filteredCommits is newest-first → top-to-bottom
          const dc = filteredCommits;
          const maxLane = Math.max(...dc.map(c => c.lane), 0);
          const totalLanes = maxLane + 1;

          const COLORS = ["#34d399", "#60a5fa", "#c084fc"];
          const DBG_COLOR = "#f59e0b";
          const LANE_W = 72;
          const ROW_H = 88;
          const PAD_L = 16;
          const PAD_T = 56;
          const PAD_B = 32;
          const NR = 8;

          // Detect if we need a dedicated debug lane on the far left
          const hasDebugLane = dc.some(c => c.isDebugInstrumentation);
          const DBG_LANE_OFFSET = hasDebugLane ? LANE_W : 0;

          const GRAPH_W = PAD_L + DBG_LANE_OFFSET + totalLanes * LANE_W + 14;
          const INFO_X = GRAPH_W + 10;

          const svgW = 580 + DBG_LANE_OFFSET;
          const svgH = PAD_T + dc.length * ROW_H + PAD_B;

          // Debug lane x-center (leftmost)
          const debugLaneX = PAD_L + LANE_W / 2;
          // Normal lanes shifted right by debug lane offset
          const laneX = (lane: number) => PAD_L + DBG_LANE_OFFSET + lane * LANE_W + LANE_W / 2;
          // Effective x for a commit: debug commits go to debug lane
          const commitX = (c: Commit) => c.isDebugInstrumentation ? debugLaneX : laneX(c.lane);
          const rowY = (ri: number) => PAD_T + ri * ROW_H + ROW_H / 2;
          // Flip index so initial commit (last in dc) appears at row 0 (top)
          const flipY = (idx: number) => dc.length - 1 - idx;

          // ── Classify commits ──
          const extractPhaseId = (subj: string): string | null => {
            const m = subj.match(/(?:ph\/|scaffold\s+)(PH-\d{4})/i) || subj.match(/(PH-\d{4})/);
            return m ? m[1] : null;
          };

          // Merge flow label: "TSK-000009 → PH-0001" or "PH-0001 → MS-0001"
          const getMergeFlowLabel = (subj: string): string | null => {
            const tskToPh = subj.match(/Merge tsk\/(TSK-\d+)\s+into\s+ph\/(PH-\d+)/i);
            if (tskToPh) return `${tskToPh[1]} → ${tskToPh[2]}`;
            const phToMs = subj.match(/Merge ph\/(PH-\d+)\s+into\s+ms\/(MS-\d+)/i);
            if (phToMs) return `${phToMs[1]} → ${phToMs[2]}`;
            return null;
          };

          // Is this a phase scaffold (start) commit?
          const isPhaseScaffoldCommit = (co: Commit) =>
            !co.isMerge && co.subject.toLowerCase().includes("scaffold") && extractPhaseId(co.subject) !== null;

          // Is this a system/orchestrator commit? (aos-agent, not a task, not a merge)
          const isSystemCommitFn = (co: Commit) =>
            !co.isMerge && co.parents.length > 0 && co.author === "aos-agent";

          // Build edges: child → parent
          const edges: { childIdx: number; parentIdx: number }[] = [];
          dc.forEach((commit, ci) => {
            for (const pSha of commit.parents) {
              const pi = dc.findIndex(co => co.sha.startsWith(pSha));
              if (pi >= 0) edges.push({ childIdx: ci, parentIdx: pi });
            }
          });

          return (
            <svg width={svgW} height={svgH} className="block">
              <defs>
                {/* Arrowhead marker for cross-lane merge edges */}
                <marker id="arrow-merge" markerWidth="8" markerHeight="6" refX="7" refY="3" orient="auto">
                  <path d="M0,0 L8,3 L0,6" fill="none" stroke="#aaa" strokeWidth="1" />
                </marker>
              </defs>

              {/* ── Lane column header bar ── */}
              <rect x={0} y={0} width={svgW} height={44} fill="#0A0A0A" />
              <line x1={0} y1={44} x2={svgW} y2={44} stroke="#444" strokeWidth={0.75} opacity={0.5} />

              {/* Debug lane header (far left, amber) */}
              {hasDebugLane && (() => {
                const dLbl = "debug";
                const dLblW = dLbl.length * 7.2 + 16;
                return (
                  <g key="lbl-debug">
                    <rect x={debugLaneX - dLblW / 2} y={12} width={dLblW} height={20} rx={4}
                      fill={DBG_COLOR} opacity={0.08} stroke={DBG_COLOR} strokeWidth={0.75} strokeOpacity={0.3} />
                    <text x={debugLaneX} y={22} textAnchor="middle" dominantBaseline="middle"
                      fill={DBG_COLOR} fontSize={11} fontFamily="ui-monospace,monospace" opacity={0.85} fontWeight="600">
                      {dLbl}
                    </text>
                  </g>
                );
              })()}

              {/* Normal lane headers */}
              {Array.from({ length: totalLanes }).map((_, l) => {
                const x = laneX(l);
                const lc = COLORS[l % COLORS.length];
                const GIT_LANE = ["main", "feature", "topic"];
                const lbl = GIT_LANE[l] ?? `lane ${l}`;
                const lblW = lbl.length * 7.2 + 16;
                return (
                  <g key={`lbl-${l}`}>
                    <rect x={x - lblW / 2} y={12} width={lblW} height={20} rx={4}
                      fill={lc} opacity={0.08} stroke={lc} strokeWidth={0.75} strokeOpacity={0.3} />
                    <text x={x} y={22} textAnchor="middle" dominantBaseline="middle"
                      fill={lc} fontSize={11} fontFamily="ui-monospace,monospace" opacity={0.85} fontWeight="600">
                      {lbl}
                    </text>
                  </g>
                );
              })}

              {/* ── Vertical lane guide lines ── */}
              {/* Debug lane guide (amber, dashed) */}
              {hasDebugLane && (() => {
                const dbgIndices = dc.map((cm, ci) => cm.isDebugInstrumentation ? ci : -1).filter(v => v >= 0);
                if (dbgIndices.length === 0) return null;
                const firstVis = Math.min(...dbgIndices.map(idx => flipY(idx)));
                const lastVis = Math.max(...dbgIndices.map(idx => flipY(idx)));
                return (
                  <line key="guide-debug"
                    x1={debugLaneX} y1={rowY(firstVis) - NR - 2}
                    x2={debugLaneX} y2={rowY(lastVis) + NR + 2}
                    stroke={DBG_COLOR} strokeWidth={2} opacity={0.12}
                    strokeDasharray="6 4"
                  />
                );
              })()}
              {/* Normal lane guides */}
              {Array.from({ length: totalLanes }).map((_, l) => {
                const x = laneX(l);
                const lc = COLORS[l % COLORS.length];
                const indices = dc.map((cm, ci) => cm.lane === l && !cm.isDebugInstrumentation ? ci : -1).filter(v => v >= 0);
                if (indices.length === 0) return null;
                const firstVis = Math.min(...indices.map(idx => flipY(idx)));
                const lastVis = Math.max(...indices.map(idx => flipY(idx)));
                return (
                  <line key={`guide-${l}`}
                    x1={x} y1={rowY(firstVis) - NR - 2}
                    x2={x} y2={rowY(lastVis) + NR + 2}
                    stroke={lc} strokeWidth={2} opacity={0.15}
                  />
                );
              })}

              {/* ── Edges ── */}
              {edges.map((e, ei) => {
                const cx1 = commitX(dc[e.childIdx]);
                const cy1 = rowY(flipY(e.childIdx));
                const cx2 = commitX(dc[e.parentIdx]);
                const cy2 = rowY(flipY(e.parentIdx));
                const childIsDbg = dc[e.childIdx].isDebugInstrumentation;
                const parentIsDbg = dc[e.parentIdx].isDebugInstrumentation;
                const childLane = dc[e.childIdx].lane;
                const parentLane = dc[e.parentIdx].lane;
                const ec = childIsDbg ? DBG_COLOR : COLORS[childLane % COLORS.length];
                const isCmp = compareA === dc[e.childIdx].sha || compareA === dc[e.parentIdx].sha;
                const isMergeEdge = dc[e.childIdx].isMerge && childLane !== parentLane;
                const isDbgEdge = childIsDbg || parentIsDbg;
                const col = isCmp ? "#60a5fa" : isDbgEdge ? DBG_COLOR : ec;
                const op = isCmp ? 0.7 : isDbgEdge ? 0.35 : isMergeEdge ? 0.5 : 0.4;
                const dashArr = isDbgEdge ? "6 3" : "none";

                const topY = Math.min(cy1, cy2);
                const botY = Math.max(cy1, cy2);
                const topX = cy1 < cy2 ? cx1 : cx2;
                const botX = cy1 < cy2 ? cx2 : cx1;

                if (cx1 === cx2) {
                  return (
                    <line key={`e-${ei}`}
                      x1={cx1} y1={topY + NR + 1} x2={cx2} y2={botY - NR - 1}
                      stroke={col} strokeWidth={2.5} opacity={op}
                      strokeDasharray={dashArr}
                    />
                  );
                } else {
                  const dy = botY - topY;
                  return (
                    <path key={`e-${ei}`}
                      d={`M${topX},${topY + NR + 1} C${topX},${topY + dy * 0.45} ${botX},${botY - dy * 0.45} ${botX},${botY - NR - 1}`}
                      fill="none" stroke={col} strokeWidth={2.5} opacity={op}
                      strokeDasharray={dashArr}
                      markerEnd={isMergeEdge ? "url(#arrow-merge)" : undefined}
                    />
                  );
                }
              })}

              {/* ── Nodes + decorations + info ── */}
              {dc.map((commit, i) => {
                const ncx = commitX(commit);
                const ncy = rowY(flipY(i));
                const nc = commit.isDebugInstrumentation ? DBG_COLOR : COLORS[commit.lane % COLORS.length];
                const isRoot = commit.parents.length === 0;
                const isSel = selectedSha === commit.sha;
                const isCA = compareA === commit.sha;
                const isCB = compareB === commit.sha;
                const inRange = compareRange?.commits.some(cr => cr.sha === commit.sha);
                const tags = commit.refs.filter(r => r.type === "tag");
                const hasHead = commit.refs.some(r => r.type === "head");
                const branchRefs = commit.refs.filter(r => r.type === "local" || r.type === "remote");

                // Task + status classification
                const commitTskId = commitTaskMap.get(commit.sha);
                const tskStatus = commitTskId ? taskStatusMap.get(commitTskId) ?? null : null;
                const isTaskCommit = !!commitTskId && !commit.isMerge;
                const isScaffold = isPhaseScaffoldCommit(commit);
                const isSysCommit = isSystemCommitFn(commit) && !isScaffold && !commitTskId;
                const scaffoldPhId = isScaffold ? extractPhaseId(commit.subject) : null;
                const mergeFlow = commit.isMerge ? getMergeFlowLabel(commit.subject) : null;

                // Task highlight from roadmap
                const isTaskHL = highlightedTaskId !== null
                  && commitTskId === highlightedTaskId
                  && highlightSource === "roadmap";

                // Status booleans
                const tDone = tskStatus === "completed";
                const tInProg = tskStatus === "in-progress";
                const tFailed = tskStatus === "failed";
                const tPlanned = tskStatus === "planned";

                // Node color overrides for task status
                const nStroke = isTaskHL ? "#34d399"
                  : tDone ? "#34d399"
                  : tFailed ? "#f87171"
                  : tInProg ? "#c084fc"
                  : nc;
                const nFill = isTaskHL ? "#34d399"
                  : tDone ? "#34d399"
                  : tFailed ? "#f87171"
                  : tInProg ? "#c084fc"
                  : nc;

                return (
                  <g key={commit.sha} className="cursor-pointer" onClick={() => handleCommitClick(commit.sha)}
                    ref={(el: SVGGElement | null) => {
                      if (el) commitRowRefs.current.set(commit.sha, el);
                      else commitRowRefs.current.delete(commit.sha);
                    }}
                  >
                    {/* Row separator line */}
                    <line x1={INFO_X} y1={ncy + ROW_H / 2} x2={svgW - 8} y2={ncy + ROW_H / 2}
                      stroke="#ffffff" strokeWidth={0.5} opacity={0.03} />
                    {/* Row hover highlight */}
                    <rect x={0} y={ncy - ROW_H / 2} width={svgW} height={ROW_H}
                      fill="transparent" className="hover:fill-white/[0.02]" rx={2} />

                    {/* Task highlight from roadmap — green row glow */}
                    {isTaskHL && (
                      <>
                        <rect x={0} y={ncy - ROW_H / 2} width={svgW} height={ROW_H}
                          fill="#34d399" opacity={0.06} rx={2} />
                        <circle cx={ncx} cy={ncy} r={NR + 8}
                          fill="none" stroke="#34d399"
                          strokeWidth={2} opacity={0.5} />
                      </>
                    )}

                    {/* Selection / compare glow */}
                    {(isSel || isCA || isCB) && (
                      <>
                        <rect x={0} y={ncy - ROW_H / 2} width={svgW} height={ROW_H}
                          fill={isCA || isCB ? "#60a5fa" : nc} opacity={0.04} />
                        <circle cx={ncx} cy={ncy} r={NR + 5}
                          fill="none" stroke={isCA || isCB ? "#60a5fa" : nc}
                          strokeWidth={2} opacity={0.5}
                          strokeDasharray={isCA || isCB ? "4 2" : "none"} />
                      </>
                    )}
                    {inRange && !isCA && !isCB && (
                      <rect x={0} y={ncy - ROW_H / 2} width={svgW} height={ROW_H}
                        fill="#60a5fa" opacity={0.025} />
                    )}

                    {/* ── Commit node glyph ── */}
                    {isRoot ? (
                      /* Root commit (parentless): dashed-ring marker */
                      <>
                        <circle cx={ncx} cy={ncy} r={NR + 4}
                          fill="none" stroke={nc} strokeWidth={2.5} strokeDasharray="5 3" opacity={0.6} />
                        <circle cx={ncx} cy={ncy} r={NR}
                          fill={nc} opacity={0.25} stroke={nc} strokeWidth={2.5} />
                        <circle cx={ncx} cy={ncy} r={3} fill={nc} />
                      </>
                    ) : isTaskCommit ? (
                      /* Tagged commit (has task ref) — status-driven ring:
                         planned=hollow, done=filled, in-progress=pulse, failed=red */
                      <>
                        {/* In-progress: animated pulse ring */}
                        {tInProg && (
                          <circle cx={ncx} cy={ncy} r={NR + 5}
                            fill="none" stroke="#c084fc" strokeWidth={2} opacity={0.3}>
                            <animate attributeName="r" values={`${NR + 4};${NR + 9};${NR + 4}`} dur="2s" repeatCount="indefinite" />
                            <animate attributeName="opacity" values="0.35;0.08;0.35" dur="2s" repeatCount="indefinite" />
                          </circle>
                        )}
                        {/* Failed: red warning ring */}
                        {tFailed && (
                          <circle cx={ncx} cy={ncy} r={NR + 4}
                            fill="none" stroke="#f87171" strokeWidth={2} opacity={0.5} />
                        )}
                        {/* Done: green check ring */}
                        {tDone && (
                          <circle cx={ncx} cy={ncy} r={NR + 3}
                            fill="none" stroke="#34d399" strokeWidth={1.5} opacity={0.4} />
                        )}
                        {/* Main node circle */}
                        <circle cx={ncx} cy={ncy} r={NR}
                          fill={tDone ? nFill : (isTaskHL ? "#34d399" : (tPlanned ? "#0A0A0A" : "#0A0A0A"))}
                          fillOpacity={tDone ? 0.35 : (isTaskHL ? 0.2 : 1)}
                          stroke={nStroke} strokeWidth={2.5}
                          strokeDasharray={tPlanned ? "4 2" : "none"} />
                        {/* Inner dot (not for planned — keep hollow) */}
                        {!tPlanned && (
                          <circle cx={ncx} cy={ncy} r={3} fill={nFill} />
                        )}
                      </>
                    ) : commit.isMerge ? (
                      /* Merge commit (≥2 parents) — double-ring */
                      <>
                        <circle cx={ncx} cy={ncy} r={NR + 3}
                          fill="none" stroke={isTaskHL ? "#34d399" : nc} strokeWidth={1.5} opacity={isTaskHL ? 0.55 : 0.35} />
                        <circle cx={ncx} cy={ncy} r={NR}
                          fill="#0A0A0A" stroke={isTaskHL ? "#34d399" : nc} strokeWidth={2.5} />
                        <circle cx={ncx} cy={ncy} r={3} fill={isTaskHL ? "#34d399" : nc} />
                      </>
                    ) : commit.isDebugInstrumentation ? (
                      /* Throwaway commit on dbg/* — amber dashed, to be dropped */
                      <>
                        <circle cx={ncx} cy={ncy} r={NR + 4}
                          fill="none" stroke="#f59e0b" strokeWidth={2}
                          strokeDasharray="4 2" opacity={0.5} />
                        <circle cx={ncx} cy={ncy} r={NR}
                          fill="#0A0A0A" stroke="#f59e0b" strokeWidth={2.5}
                          strokeDasharray="4 2" />
                        <text x={ncx} y={ncy + 0.5} textAnchor="middle" dominantBaseline="middle"
                          fill="#f59e0b" fontSize={9} opacity={0.8}>
                          {"⚠"}
                        </text>
                      </>
                    ) : (
                      /* Ordinary commit (automated / scaffold / other) */
                      <>
                        <circle cx={ncx} cy={ncy} r={NR}
                          fill={isTaskHL ? "#34d399" : "#0A0A0A"}
                          fillOpacity={isTaskHL ? 0.2 : 1}
                          stroke={isTaskHL ? "#34d399" : nc} strokeWidth={2.5}
                          strokeDasharray={isSysCommit ? "4 2" : "none"} />
                        <circle cx={ncx} cy={ncy} r={3}
                          fill={isTaskHL ? "#34d399" : nc}
                          opacity={isSysCommit ? 0.6 : 1} />
                        {/* Automated commit: gear icon */}
                        {isSysCommit && (
                          <text x={ncx} y={ncy + 0.5} textAnchor="middle" dominantBaseline="middle"
                            fill={nc} fontSize={9} opacity={0.8}>
                            {"⚙"}
                          </text>
                        )}
                      </>
                    )}

                    {/* ── Commit info (right of graph area) ── */}
                    {/* Row 1: SHA + ref badges */}
                    {(() => {
                      const ROW1_Y = ncy - 20;
                      let bx = INFO_X;
                      const els: JSX.Element[] = [];

                      // SHA hash
                      els.push(
                        <text key="sha" x={bx} y={ROW1_Y}
                          dominantBaseline="middle" fill={nc} fontSize={11}
                          fontFamily="ui-monospace,monospace" opacity={0.9}>
                          {commit.shortSha}
                        </text>
                      );
                      bx += 56;

                      // HEAD badge
                      if (hasHead) {
                        els.push(
                          <g key="head">
                            <rect x={bx} y={ROW1_Y - 7} width={32} height={14} rx={3}
                              fill={nc} opacity={0.18} stroke={nc} strokeWidth={0.75} />
                            <text x={bx + 16} y={ROW1_Y} textAnchor="middle" dominantBaseline="middle"
                              fill={nc} fontSize={8.5} fontFamily="ui-monospace,monospace" fontWeight="bold">
                              HEAD
                            </text>
                          </g>
                        );
                        bx += 36;
                      }

                      // Tag badges — shortened
                      tags.forEach((t, ti) => {
                        const raw = t.name.replace(/^refs\/tags\//, "");
                        const tagLabel = raw.length > 10 ? raw.slice(0, 9) + "…" : raw;
                        const tw = tagLabel.length * 5.8 + 28;
                        els.push(
                          <g key={`tag-${ti}`}>
                            <rect x={bx} y={ROW1_Y - 7} width={tw} height={14} rx={3}
                              fill="#0A0A0A" stroke="#f59e0b" strokeWidth={0.75} opacity={0.85} />
                            <text x={bx + 6} y={ROW1_Y} dominantBaseline="middle"
                              fill="#fbbf24" fontSize={8.5} fontFamily="ui-monospace,monospace" opacity={0.9}>
                              {"🏷 " + tagLabel}
                            </text>
                          </g>
                        );
                        bx += tw + 6;
                      });

                      // Branch refs — strip common prefixes for brevity
                      branchRefs.forEach((br, bi) => {
                        const short = br.name
                          .replace(/^refs\/(?:heads|remotes)\//, "")
                          .replace(/^origin\//, "")
                          .replace(/^(?:ph|tsk|ms|dbg)\//, "");
                        const brLabel = short.length > 12 ? short.slice(0, 11) + "…" : short;
                        const bw = brLabel.length * 5.8 + 10;
                        const brColor = br.isCurrent ? nc : br.type === "remote" ? "#94a3b8" : "#60a5fa";
                        els.push(
                          <g key={`br-${bi}`}>
                            <rect x={bx} y={ROW1_Y - 7} width={bw} height={14} rx={3}
                              fill={brColor} opacity={0.1}
                              stroke={brColor} strokeWidth={0.75} />
                            <text x={bx + 5} y={ROW1_Y} dominantBaseline="middle"
                              fill={brColor} fontSize={8.5}
                              fontFamily="ui-monospace,monospace" opacity={0.85}>
                              {brLabel}
                            </text>
                          </g>
                        );
                        bx += bw + 4;
                      });

                      // Task ID badge — compact (TSK-000009 → T-9)
                      if (isTaskCommit && commitTskId) {
                        const tskShort = commitTskId.replace(/^TSK-0*/, "T-");
                        const tskW = tskShort.length * 6 + 10;
                        els.push(
                          <g key="tsk">
                            <rect x={bx} y={ROW1_Y - 7} width={tskW} height={14} rx={3}
                              fill={nFill} opacity={0.15} stroke={nFill} strokeWidth={0.75} />
                            <text x={bx + 5} y={ROW1_Y} dominantBaseline="middle"
                              fill={nFill} fontSize={8.5} fontFamily="ui-monospace,monospace" fontWeight="bold">
                              {tskShort}
                            </text>
                          </g>
                        );
                        bx += tskW + 3;
                        if (tDone) els.push(<text key="tsk-s" x={bx} y={ROW1_Y} dominantBaseline="middle" fill="#34d399" fontSize={9}>{"✓"}</text>);
                        if (tFailed) els.push(<text key="tsk-s" x={bx} y={ROW1_Y} dominantBaseline="middle" fill="#f87171" fontSize={9}>{"✗"}</text>);
                        if (tInProg) els.push(<text key="tsk-s" x={bx} y={ROW1_Y} dominantBaseline="middle" fill="#c084fc" fontSize={9}>{"◎"}</text>);
                      }

                      // Branch-point badge — compact (PH-0001 → P-1 ↱)
                      if (isScaffold && scaffoldPhId) {
                        const scShort = scaffoldPhId.replace(/^PH-0*/, "P-") + " ↱";
                        const scW = scShort.length * 5.8 + 10;
                        els.push(
                          <g key="scaffold">
                            <rect x={bx} y={ROW1_Y - 7} width={scW} height={14} rx={3}
                              fill="#60a5fa" opacity={0.1} stroke="#60a5fa" strokeWidth={0.75} />
                            <text x={bx + 5} y={ROW1_Y} dominantBaseline="middle"
                              fill="#60a5fa" fontSize={8.5} fontFamily="ui-monospace,monospace" fontWeight="bold">
                              {scShort}
                            </text>
                          </g>
                        );
                      }

                      // Merge summary — compact (TSK-000009→T-9, PH-0001→P-1)
                      if (commit.isMerge && mergeFlow) {
                        const mfShort = mergeFlow
                          .replace(/TSK-0*/g, "T-")
                          .replace(/PH-0*/g, "P-")
                          .replace(/MS-0*/g, "M-");
                        const mfW = mfShort.length * 5.5 + 10;
                        els.push(
                          <g key="merge-flow">
                            <rect x={bx} y={ROW1_Y - 7} width={mfW} height={14} rx={3}
                              fill={nc} opacity={0.08} stroke={nc} strokeWidth={0.5} />
                            <text x={bx + 5} y={ROW1_Y} dominantBaseline="middle"
                              fill={nc} fontSize={8.5} fontFamily="ui-monospace,monospace" fontStyle="italic" opacity={0.75}>
                              {mfShort}
                            </text>
                          </g>
                        );
                      }

                      return els;
                    })()}

                    {/* Row 2: Subject (commit message first line) */}
                    {isTaskCommit && commitTskId ? (
                      <text x={INFO_X} y={ncy + 1} dominantBaseline="middle"
                        fill="#eee" fontSize={11}
                        fontFamily="ui-monospace,monospace" opacity={0.85}>
                        {(() => {
                          const cleaned = commit.subject.replace(`[${commitTskId}]`, "").trim();
                          return cleaned.length > 44 ? cleaned.slice(0, 43) + "…" : cleaned;
                        })()}
                      </text>
                    ) : (
                      <text x={INFO_X} y={ncy + 1} dominantBaseline="middle"
                        fill={commit.isDebugInstrumentation ? "#f59e0b" : commit.isMerge ? "#999" : isSysCommit ? "#8b95a5" : "#eee"} fontSize={11}
                        fontFamily="ui-monospace,monospace"
                        opacity={commit.isDebugInstrumentation ? 0.75 : commit.isMerge ? 0.65 : isSysCommit ? 0.65 : 0.85}
                        fontStyle={commit.isDebugInstrumentation || commit.isMerge || isSysCommit ? "italic" : "normal"}>
                        {commit.subject.length > 44
                          ? commit.subject.slice(0, 43) + "…"
                          : commit.subject}
                      </text>
                    )}

                    {/* Row 3: Author · date · file count */}
                    <text x={INFO_X} y={ncy + 18} dominantBaseline="middle"
                      fill="#999" fontSize={9.5} fontFamily="ui-monospace,monospace" opacity={0.6}>
                      {isSysCommit ? "🤖 " : ""}{commit.author} · {commit.relDate}
                      {commit.changedFiles.length > 0 ? ` · ${commit.changedFiles.length} files` : ""}
                    </text>

                    {/* Row 4: CI / debug annotations */}
                    {commit.debugOverlays && commit.debugOverlays.length > 0 && (() => {
                      const DBG_Y = ncy + 33;
                      let dx = INFO_X;
                      const dbgColorMap: Record<DebugOverlay["status"], { fill: string; stroke: string; text: string }> = {
                        pass: { fill: "#34d399", stroke: "#34d399", text: "#34d399" },
                        fail: { fill: "#f87171", stroke: "#f87171", text: "#f87171" },
                        info: { fill: "#60a5fa", stroke: "#60a5fa", text: "#60a5fa" },
                      };
                      const dbgIconMap: Record<DebugOverlay["kind"], string> = {
                        uat: "⬡",
                        repro: "◉",
                        trace: "⏎",
                        bisect: "⌥",
                        perf: "⚡",
                      };
                      return commit.debugOverlays.map((ov, oi) => {
                        const c = dbgColorMap[ov.status];
                        const icon = dbgIconMap[ov.kind];
                        const lbl = `${icon} ${ov.label}`;
                        const w = lbl.length * 5.5 + 12;
                        const el = (
                          <g key={`dbg-${oi}`}>
                            <rect x={dx} y={DBG_Y - 7} width={w} height={14} rx={3}
                              fill={c.fill} opacity={0.08} stroke={c.stroke} strokeWidth={0.5} />
                            <text x={dx + 6} y={DBG_Y} dominantBaseline="middle"
                              fill={c.text} fontSize={8.5} fontFamily="ui-monospace,monospace" opacity={0.8}>
                              {lbl}
                            </text>
                          </g>
                        );
                        dx += w + 5;
                        return el;
                      });
                    })()}

                    {/* Throwaway debug branch — drop/squash before merge */}
                    {commit.isDebugInstrumentation && (
                      <g>
                        <rect x={INFO_X} y={ncy + 33 - 7} width={105} height={14} rx={3}
                          fill="#f59e0b" opacity={0.1} stroke="#f59e0b" strokeWidth={0.5} strokeDasharray="3 2" />
                        <text x={INFO_X + 6} y={ncy + 33} dominantBaseline="middle"
                          fill="#f59e0b" fontSize={8.5} fontFamily="ui-monospace,monospace" opacity={0.75} fontStyle="italic">
                          ⚠ drop before merge
                        </text>
                      </g>
                    )}

                    {/* ── Root commit marker ── */}
                    {isRoot && (
                      <g>
                        <line x1={ncx} y1={ncy - NR - 2} x2={ncx} y2={ncy - NR - 12}
                          stroke={nc} strokeWidth={1} opacity={0.5} />
                        <rect x={ncx - 18} y={ncy - NR - 26} width={36} height={14} rx={3}
                          fill="#0A0A0A" stroke={nc} strokeWidth={1}
                          strokeDasharray="4 2" opacity={0.75} />
                        <text x={ncx} y={ncy - NR - 17.5}
                          textAnchor="middle" dominantBaseline="middle"
                          fill={nc} fontSize={9} fontFamily="ui-monospace,monospace" opacity={0.9} fontWeight="bold">
                          root
                        </text>
                      </g>
                    )}
                  </g>
                );
              })}
            </svg>
          );
        })()}
      </div>

      {/* Detail panel (when commit selected) */}
      {selectedCommit && !isCompareMode && (
        <div className="shrink-0 border-t border-emerald-500/10 max-h-[45%] overflow-auto bg-[#080808]">
          <div className="px-4 py-2.5 border-b border-border/10 flex items-center justify-between sticky top-0 bg-[#080808]/95 backdrop-blur-sm z-10">
            <div className="flex items-center gap-2 flex-wrap">
              <GitCommitIcon className="h-3.5 w-3.5 text-emerald-500" />
              <span className="font-mono text-[11px] text-emerald-400">{selectedCommit.shortSha}</span>
              {selectedCommit.refs.map((r, ri) => <RefBadge key={ri} gitRef={r} />)}
            </div>
            <Button variant="ghost" size="sm" className="h-6 w-6 p-0 shrink-0" onClick={() => setSelectedSha(null)}>
              <X className="h-3 w-3" />
            </Button>
          </div>

          <div className="px-4 py-3 space-y-3">
            {/* Subject + meta */}
            <div>
              <p className="text-xs font-medium mb-1.5 break-words">{selectedCommit.subject}</p>
              <div className="flex items-center gap-3 text-[10px] text-muted-foreground/60 font-mono flex-wrap">
                <span><User className="h-2.5 w-2.5 inline mr-0.5" />{selectedCommit.author}</span>
                <span><Clock className="h-2.5 w-2.5 inline mr-0.5" />{selectedCommit.date.replace("T", " ").replace("Z", "")}</span>
                {selectedCommit.gpgStatus === "good" && <span className="text-emerald-500/60"><Shield className="h-2.5 w-2.5 inline mr-0.5" />Signed</span>}
              </div>
            </div>

            {/* Actions */}
            <div className="flex items-center gap-1.5 flex-wrap pt-1 border-t border-border/15">
              <Button variant="outline" size="sm" className="h-6 text-[10px] font-mono gap-1 border-border/30 text-muted-foreground/70 hover:text-foreground">
                <GitBranch className="h-2.5 w-2.5" /> Create branch
              </Button>
              <Button variant="outline" size="sm" className="h-6 text-[10px] font-mono gap-1 border-border/30 text-muted-foreground/70 hover:text-foreground">
                <ArrowUpRight className="h-2.5 w-2.5" /> Cherry-pick
              </Button>
              <Button variant="outline" size="sm" className="h-6 text-[10px] font-mono gap-1 border-border/30 text-muted-foreground/70 hover:text-foreground">
                <RotateCcw className="h-2.5 w-2.5" /> Revert
              </Button>
              <Button variant="outline" size="sm" className="h-6 text-[10px] font-mono gap-1 border-rose-500/20 text-rose-400/50 hover:text-rose-400 hover:border-rose-500/40">
                <AlertTriangle className="h-2.5 w-2.5" /> Reset...
              </Button>
            </div>

            {/* Parents */}
            {selectedCommit.parents.length > 0 && (
              <div>
                <div className="text-[9px] text-muted-foreground/40 uppercase tracking-wider font-medium mb-1">
                  {selectedCommit.parents.length > 1 ? "Parents (merge)" : "Parent"}
                </div>
                <div className="flex items-center gap-1.5 flex-wrap">
                  {selectedCommit.parents.map((p, pi) => (
                    <span key={pi} className="font-mono text-[10px] px-1.5 py-0.5 rounded bg-muted/20 border border-border/20 text-muted-foreground/70">
                      {p.substring(0, 7)}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Changed files */}
            {selectedCommit.changedFiles.length > 0 && (
              <div>
                <div className="text-[9px] text-muted-foreground/40 uppercase tracking-wider font-medium mb-1">
                  Changed files ({selectedCommit.changedFiles.length})
                </div>
                <div className="space-y-0.5">
                  {selectedCommit.changedFiles.map((f, fi) => (
                    <div key={fi} className="flex items-center gap-2 text-[10px] font-mono py-1 px-2 rounded hover:bg-accent/40 cursor-pointer group/file">
                      <FileStatusIcon status={f.status} />
                      <span className="truncate text-muted-foreground/80 group-hover/file:text-foreground transition-colors">{f.path}</span>
                      <span className="ml-auto flex items-center gap-1.5 shrink-0 text-[9px]">
                        <span className="text-emerald-400/70">+{f.additions}</span>
                        <span className="text-rose-400/70">-{f.deletions}</span>
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Debug activity overlays */}
            {selectedCommit.debugOverlays && selectedCommit.debugOverlays.length > 0 && (
              <div>
                <div className="text-[9px] text-muted-foreground/40 uppercase tracking-wider font-medium mb-1">
                  Debug activity
                </div>
                <div className="flex items-center gap-1.5 flex-wrap">
                  {selectedCommit.debugOverlays.map((ov, oi) => {
                    const statusCls = ov.status === "pass"
                      ? "bg-emerald-500/10 text-emerald-400 border-emerald-500/20"
                      : ov.status === "fail"
                      ? "bg-red-500/10 text-red-400 border-red-500/20"
                      : "bg-blue-500/10 text-blue-400 border-blue-500/20";
                    const icons: Record<string, string> = { uat: "⬡", repro: "◉", trace: "⏎", bisect: "⌥", perf: "⚡" };
                    return (
                      <span key={oi} className={cn(
                        "inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono border",
                        statusCls
                      )}>
                        <span>{icons[ov.kind] ?? "●"}</span>
                        {ov.label}
                      </span>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Debug instrumentation warning */}
            {selectedCommit.isDebugInstrumentation && (
              <div className="flex items-center gap-2 px-2 py-1.5 rounded bg-amber-500/5 border border-amber-500/20 border-dashed text-[10px] font-mono text-amber-400/70">
                <AlertTriangle className="h-3 w-3 shrink-0" />
                <span>Temporary debug instrumentation — drop before merge</span>
              </div>
            )}

          </div>
        </div>
      )}
    </div>
  );
}
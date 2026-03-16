/**
 * Verification State — derives UAT items and Fix Loop items
 * from workspace data for the Verification Hub editor.
 *
 * This is a pure derivation function — all data is passed in,
 * no direct mock imports. Wrap with useVerificationState() hook
 * for React component usage.
 */

import type {
  Issue,
  Run,
  Task,
  TaskPlan,
  Phase,
} from "../../hooks/useAosData";

// ─── Types ───────────────────────────────────────────────────────

export type UATStatus = "pass" | "fail" | "partial" | "unverified";
export type CheckResult = "pass" | "fail" | "pending";

export interface VerificationCheck {
  id: string;
  command: string;
  type: "automated" | "manual";
  result: CheckResult;
  observation?: string;
}

export interface UATItem {
  id: string;              // UAT-000012
  taskId: string;          // TSK-000012
  taskName: string;
  phaseId: string;
  phaseTitle: string;
  status: UATStatus;
  checks: VerificationCheck[];
  checksTotal: number;
  checksPassed: number;
  checksFailed: number;
  checksPending: number;
  acceptanceCriteria: string[];
  fileScope: string[];
  linkedIssueIds: string[];
  linkedIssues: Issue[];
  linkedRunIds: string[];
  linkedRuns: Run[];
  lastRun: { id: string; status: "pass" | "fail"; timeAgo: string } | null;
  taskStatus: Task["status"];
  planSteps: TaskPlan["steps"];
}

export type FixStatus = "open" | "triaging" | "planned" | "executing" | "resolved";

export interface FixItem {
  id: string;              // ISS-0001
  severity: Issue["severity"];
  description: string;
  issueStatus: Issue["status"];
  fixStatus: FixStatus;
  linkedUATIds: string[];
  linkedTaskIds: string[];
  linkedRunIds: string[];
  linkedRuns: Run[];
  impactedFiles: string[];
  impactedArea: string;
  repro: string[];
  expectedVsActual?: { expected: string; actual: string };
  history: NonNullable<Issue["history"]>;
  tags: string[];
  lastRun: { id: string; status: "pass" | "fail"; timeAgo: string } | null;
}

export interface VerificationContext {
  uatItems: UATItem[];
  fixItems: FixItem[];
  // Aggregate
  totalUAT: number;
  passCount: number;
  failCount: number;
  pendingCount: number;
  totalIssues: number;
  openIssues: number;
  totalRuns: number;
}

// ─── Derivation input ────────────────────────────────────────────

export interface VerificationDataInput {
  tasks: Task[];
  taskPlans: TaskPlan[];
  issues: Issue[];
  runs: Run[];
  phases: Phase[];
}

// ─── Helpers ─────────────────────────────────────────────────────

function timeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

function phaseTitle(phaseId: string, phases: Phase[]): string {
  return phases.find((p) => p.id === phaseId)?.title ?? phaseId;
}

function runsForTask(taskId: string, runs: Run[]): Run[] {
  return runs
    .filter((r) => r.taskId === taskId)
    .sort((a, b) => new Date(b.startTime).getTime() - new Date(a.startTime).getTime());
}

function lastRunSummary(runs: Run[]): UATItem["lastRun"] {
  const r = runs[0];
  if (!r) return null;
  return {
    id: r.id,
    status: r.status === "success" ? "pass" : "fail",
    timeAgo: timeAgo(r.endTime || r.startTime),
  };
}

// ─── Derivation ──────────────────────────────────────────────────

export function deriveVerificationState(data: VerificationDataInput): VerificationContext {
  const { tasks, taskPlans, issues, runs, phases } = data;

  const uatItems: UATItem[] = tasks.map((task) => {
    const plan = taskPlans.find((p) => p.taskId === task.id);
    const checks: VerificationCheck[] = (plan?.verification ?? []).map((v, i) => ({
      id: `chk-${task.id}-${i}`,
      command: v.command,
      type: v.type,
      result: v.passed === true ? "pass" as const : v.passed === false ? "fail" as const : "pending" as const,
    }));

    const passed = checks.filter((c) => c.result === "pass").length;
    const failed = checks.filter((c) => c.result === "fail").length;
    const pending = checks.filter((c) => c.result === "pending").length;

    const linkedIssues = issues.filter((i) => i.linkedTasks.includes(task.id));
    const linkedRuns = runsForTask(task.id, runs);

    let status: UATStatus = "unverified";
    if (checks.length > 0 && passed === checks.length) status = "pass";
    else if (failed > 0) status = "fail";
    else if (passed > 0) status = "partial";

    return {
      id: task.id.replace("TSK", "UAT"),
      taskId: task.id,
      taskName: task.name,
      phaseId: task.phaseId,
      phaseTitle: phaseTitle(task.phaseId, phases),
      status,
      checks,
      checksTotal: checks.length,
      checksPassed: passed,
      checksFailed: failed,
      checksPending: pending,
      acceptanceCriteria: plan?.definitionOfDone ?? [],
      fileScope: plan?.fileScope ?? [],
      linkedIssueIds: linkedIssues.map((i) => i.id),
      linkedIssues,
      linkedRunIds: linkedRuns.map((r) => r.id),
      linkedRuns,
      lastRun: lastRunSummary(linkedRuns),
      taskStatus: task.status,
      planSteps: plan?.steps ?? [],
    };
  });

  const fixItems: FixItem[] = issues.map((iss) => {
    const linkedTaskIds = iss.linkedTasks;
    const linkedUATIds = linkedTaskIds.map((t) => t.replace("TSK", "UAT"));
    const linkedRuns2 = linkedTaskIds.flatMap((t) => runsForTask(t, runs));
    const uniqueRuns = Array.from(new Map(linkedRuns2.map((r) => [r.id, r])).values());

    let fixStatus: FixStatus = "open";
    if (iss.status === "resolved") fixStatus = "resolved";
    else if (iss.status === "in-progress") fixStatus = "executing";
    else if (iss.history && iss.history.length > 1) fixStatus = "planned";
    else fixStatus = "triaging";

    return {
      id: iss.id,
      severity: iss.severity,
      description: iss.description,
      issueStatus: iss.status,
      fixStatus,
      linkedUATIds,
      linkedTaskIds,
      linkedRunIds: uniqueRuns.map((r) => r.id),
      linkedRuns: uniqueRuns,
      impactedFiles: iss.impactedFiles ?? [],
      impactedArea: iss.impactedArea ?? "",
      repro: iss.repro,
      expectedVsActual: iss.expectedVsActual,
      history: iss.history ?? [],
      tags: iss.tags ?? [],
      lastRun: lastRunSummary(uniqueRuns),
    };
  });

  const passCount = uatItems.filter((r) => r.status === "pass").length;
  const failCount = uatItems.filter((r) => r.status === "fail").length;
  const pendingCount = uatItems.filter((r) => r.status === "unverified" || r.status === "partial").length;

  return {
    uatItems,
    fixItems,
    totalUAT: uatItems.length,
    passCount,
    failCount,
    pendingCount,
    totalIssues: issues.length,
    openIssues: issues.filter((i) => i.status !== "resolved").length,
    totalRuns: runs.length,
  };
}
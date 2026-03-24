/**
 * Tests for the pure deriveVerificationState() derivation function.
 *
 * These validate the core UAT + Fix Loop state machine
 * without needing React or DOM — just data in, data out.
 */
import { describe, it, expect } from "vitest";
import {
  deriveVerificationState,
  type VerificationDataInput,
} from "../verificationState";
import type { Task, TaskPlan, Issue, Run, Phase } from "../../../hooks/useAosData";

// ── Fixtures ─────────────────────────────────────────────────────

const basePhase: Phase = {
  id: "PH-0001",
  milestoneId: "MS-0001",
  title: "Test Phase",
  summary: "Phase for testing",
  order: 1,
  status: "in-progress",
  brief: {
    goal: "Test",
    priorities: ["Correctness"],
    nonGoals: [],
    constraints: [],
    dependencies: [],
  },
  deliverables: [],
  acceptance: { criteria: ["Unit tests pass"], uatChecklist: [] },
  links: {
    roadmapPhaseRef: "",
    tasks: [],
    artifacts: [{ kind: "phase-file", path: ".aos/spec/phases/PH-0001/phase.json" }],
  },
  metadata: {
    tags: [],
    owner: "test",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
  },
};

const baseTask: Task = {
  id: "TSK-000001",
  phaseId: "PH-0001",
  milestone: "MS-0001",
  name: "Test task",
  status: "in-progress",
  assignedTo: "agent",
};

const passingPlan: TaskPlan = {
  taskId: "TSK-000001",
  fileScope: ["src/test.ts"],
  steps: [
    { order: 1, description: "Write test", done: true },
    { order: 2, description: "Run test", done: true },
  ],
  verification: [
    { command: "vitest run", type: "automated", passed: true },
    { command: "Manual check", type: "manual", passed: true },
  ],
  definitionOfDone: ["Tests pass"],
};

const failingPlan: TaskPlan = {
  taskId: "TSK-000001",
  fileScope: ["src/test.ts"],
  steps: [{ order: 1, description: "Write test", done: true }],
  verification: [
    { command: "vitest run", type: "automated", passed: false },
  ],
  definitionOfDone: ["Tests pass"],
};

const pendingPlan: TaskPlan = {
  taskId: "TSK-000001",
  fileScope: [],
  steps: [],
  verification: [
    { command: "vitest run", type: "automated" }, // passed is undefined → pending
  ],
  definitionOfDone: [],
};

const baseIssue: Issue = {
  id: "ISS-0001",
  severity: "medium",
  scope: "test",
  description: "Something broke",
  status: "open",
  repro: ["Step 1", "Step 2"],
  linkedTasks: ["TSK-000001"],
};

const baseRun: Run = {
  id: "RUN-001",
  taskId: "TSK-000001",
  command: "vitest run",
  status: "success",
  startTime: "2026-03-09T10:00:00Z",
  endTime: "2026-03-09T10:01:00Z",
  artifacts: [],
  logs: ["All tests passed"],
  changedFiles: [],
};

function makeInput(overrides: Partial<VerificationDataInput> = {}): VerificationDataInput {
  return {
    tasks: [baseTask],
    taskPlans: [passingPlan],
    issues: [],
    runs: [],
    phases: [basePhase],
    ...overrides,
  };
}

// ── Tests ────────────────────────────────────────────────────────

describe("deriveVerificationState", () => {
  it("returns empty context for no data", () => {
    const result = deriveVerificationState({
      tasks: [],
      taskPlans: [],
      issues: [],
      runs: [],
      phases: [],
    });
    expect(result.totalUAT).toBe(0);
    expect(result.uatItems).toEqual([]);
    expect(result.fixItems).toEqual([]);
  });

  it("marks UAT item as 'pass' when all checks pass", () => {
    const result = deriveVerificationState(makeInput());
    expect(result.uatItems).toHaveLength(1);
    expect(result.uatItems[0].status).toBe("pass");
    expect(result.uatItems[0].checksPassed).toBe(2);
    expect(result.uatItems[0].checksFailed).toBe(0);
    expect(result.passCount).toBe(1);
  });

  it("marks UAT item as 'fail' when any check fails", () => {
    const result = deriveVerificationState(
      makeInput({ taskPlans: [failingPlan] })
    );
    expect(result.uatItems[0].status).toBe("fail");
    expect(result.failCount).toBe(1);
  });

  it("marks UAT item as 'unverified' when checks are pending", () => {
    const result = deriveVerificationState(
      makeInput({ taskPlans: [pendingPlan] })
    );
    // No passed, no failed → "unverified" (pending counts toward unverified)
    expect(result.uatItems[0].status).toBe("unverified");
    expect(result.uatItems[0].checksPending).toBe(1);
    expect(result.pendingCount).toBe(1);
  });

  it("marks UAT item as 'partial' when some checks pass", () => {
    const partialPlan: TaskPlan = {
      ...passingPlan,
      verification: [
        { command: "vitest run", type: "automated", passed: true },
        { command: "Manual check", type: "manual" }, // pending
      ],
    };
    const result = deriveVerificationState(
      makeInput({ taskPlans: [partialPlan] })
    );
    expect(result.uatItems[0].status).toBe("partial");
  });

  it("derives fix items from issues", () => {
    const result = deriveVerificationState(
      makeInput({ issues: [baseIssue] })
    );
    expect(result.fixItems).toHaveLength(1);
    expect(result.fixItems[0].id).toBe("ISS-0001");
    expect(result.fixItems[0].linkedTaskIds).toContain("TSK-000001");
    expect(result.totalIssues).toBe(1);
    expect(result.openIssues).toBe(1);
  });

  it("links runs to UAT items", () => {
    const result = deriveVerificationState(
      makeInput({ runs: [baseRun] })
    );
    expect(result.uatItems[0].linkedRunIds).toContain("RUN-001");
    expect(result.uatItems[0].lastRun).not.toBeNull();
    expect(result.uatItems[0].lastRun?.status).toBe("pass");
    expect(result.totalRuns).toBe(1);
  });

  it("resolves phase titles for UAT items", () => {
    const result = deriveVerificationState(makeInput());
    expect(result.uatItems[0].phaseTitle).toBe("Test Phase");
  });

  it("maps fix status from issue status", () => {
    const resolved: Issue = { ...baseIssue, status: "resolved" };
    const inProgress: Issue = { ...baseIssue, id: "ISS-0002", status: "in-progress" };
    const result = deriveVerificationState(
      makeInput({ issues: [resolved, inProgress] })
    );
    expect(result.fixItems.find((f) => f.id === "ISS-0001")?.fixStatus).toBe("resolved");
    expect(result.fixItems.find((f) => f.id === "ISS-0002")?.fixStatus).toBe("executing");
  });
});

// ── Severity & Status preservation (task 4.3) ────────────────────

describe("deriveVerificationState — issue severity and status propagation", () => {
  it("preserves severity=critical on fix item", () => {
    const issue: Issue = { ...baseIssue, id: "ISS-C", severity: "critical" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].severity).toBe("critical");
  });

  it("preserves severity=high on fix item", () => {
    const issue: Issue = { ...baseIssue, id: "ISS-H", severity: "high" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].severity).toBe("high");
  });

  it("preserves severity=medium on fix item", () => {
    const issue: Issue = { ...baseIssue, id: "ISS-M", severity: "medium" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].severity).toBe("medium");
  });

  it("preserves severity=low on fix item", () => {
    const issue: Issue = { ...baseIssue, id: "ISS-L", severity: "low" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].severity).toBe("low");
  });

  it("preserves issueStatus=open on fix item", () => {
    const issue: Issue = { ...baseIssue, status: "open" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].issueStatus).toBe("open");
  });

  it("preserves issueStatus=in-progress on fix item", () => {
    const issue: Issue = { ...baseIssue, status: "in-progress" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].issueStatus).toBe("in-progress");
  });

  it("preserves issueStatus=resolved on fix item", () => {
    const issue: Issue = { ...baseIssue, status: "resolved" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].issueStatus).toBe("resolved");
  });

  it("maps open issue with no history to fixStatus=triaging", () => {
    const issue: Issue = { ...baseIssue, status: "open" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].fixStatus).toBe("triaging");
  });

  it("maps open issue with >1 history entries to fixStatus=planned", () => {
    const issue: Issue = {
      ...baseIssue,
      status: "open",
      history: [
        { timestamp: "2026-03-01T00:00:00Z", event: "created" },
        { timestamp: "2026-03-02T00:00:00Z", event: "triaged" },
      ],
    };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].fixStatus).toBe("planned");
  });

  it("maps in-progress issue to fixStatus=executing", () => {
    const issue: Issue = { ...baseIssue, status: "in-progress" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].fixStatus).toBe("executing");
  });

  it("maps resolved issue to fixStatus=resolved", () => {
    const issue: Issue = { ...baseIssue, status: "resolved" };
    const { fixItems } = deriveVerificationState(makeInput({ issues: [issue] }));
    expect(fixItems[0].fixStatus).toBe("resolved");
  });

  it("multiple issues with different severity and status all appear correctly", () => {
    const issues: Issue[] = [
      { ...baseIssue, id: "ISS-A", severity: "critical", status: "open" },
      { ...baseIssue, id: "ISS-B", severity: "high", status: "in-progress" },
      { ...baseIssue, id: "ISS-C", severity: "medium", status: "resolved" },
      { ...baseIssue, id: "ISS-D", severity: "low", status: "open" },
    ];
    const { fixItems, totalIssues, openIssues } = deriveVerificationState(
      makeInput({ issues })
    );
    expect(fixItems).toHaveLength(4);
    expect(totalIssues).toBe(4);
    expect(openIssues).toBe(3); // in-progress + open + open; resolved is excluded

    const a = fixItems.find((f) => f.id === "ISS-A")!;
    expect(a.severity).toBe("critical");
    expect(a.issueStatus).toBe("open");
    expect(a.fixStatus).toBe("triaging");

    const b = fixItems.find((f) => f.id === "ISS-B")!;
    expect(b.severity).toBe("high");
    expect(b.issueStatus).toBe("in-progress");
    expect(b.fixStatus).toBe("executing");

    const c = fixItems.find((f) => f.id === "ISS-C")!;
    expect(c.severity).toBe("medium");
    expect(c.issueStatus).toBe("resolved");
    expect(c.fixStatus).toBe("resolved");

    const d = fixItems.find((f) => f.id === "ISS-D")!;
    expect(d.severity).toBe("low");
    expect(d.issueStatus).toBe("open");
  });

  it("openIssues count excludes resolved issues", () => {
    const issues: Issue[] = [
      { ...baseIssue, id: "ISS-1", status: "open" },
      { ...baseIssue, id: "ISS-2", status: "in-progress" },
      { ...baseIssue, id: "ISS-3", status: "resolved" },
    ];
    const { openIssues } = deriveVerificationState(makeInput({ issues }));
    expect(openIssues).toBe(2);
  });
});

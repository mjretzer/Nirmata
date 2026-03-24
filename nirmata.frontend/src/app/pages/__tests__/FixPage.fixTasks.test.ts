/**
 * Tests for the FixPage fix-task derivation (task 4.4).
 *
 * Validates that getFixFileContent("plan.json", issues) correctly derives
 * activeFixes from open issues and excludes resolved ones — without requiring
 * a DOM or React render.
 */
import { describe, it, expect } from "vitest";
import { getFixFileContent } from "../FixPage";

// ── Fixtures ──────────────────────────────────────────────────────

type IssueLike = { id: string; severity: string; linkedTasks: string[]; status: string };

const openIssue: IssueLike = {
  id: "ISS-0001",
  severity: "high",
  linkedTasks: ["TSK-000001"],
  status: "open",
};

const inProgressIssue: IssueLike = {
  id: "ISS-0002",
  severity: "medium",
  linkedTasks: ["TSK-000002", "TSK-000003"],
  status: "in-progress",
};

const resolvedIssue: IssueLike = {
  id: "ISS-0003",
  severity: "low",
  linkedTasks: ["TSK-000004"],
  status: "resolved",
};

// ── Helpers ───────────────────────────────────────────────────────

function parsePlan(issues: IssueLike[]) {
  const raw = getFixFileContent("plan.json", issues);
  expect(raw).not.toBeNull();
  return JSON.parse(raw!) as {
    $schema: string;
    activeFixes: { issueId: string; severity: string; linkedTasks: string[]; status: string }[];
    generatedAt: string;
  };
}

// ── Tests ─────────────────────────────────────────────────────────

describe("getFixFileContent — plan.json derivation", () => {
  it("returns non-null content for plan.json", () => {
    const result = getFixFileContent("plan.json", []);
    expect(result).not.toBeNull();
  });

  it("returns null for unknown paths", () => {
    expect(getFixFileContent("unknown.json", [])).toBeNull();
  });

  it("includes open issues in activeFixes", () => {
    const { activeFixes } = parsePlan([openIssue]);
    expect(activeFixes).toHaveLength(1);
    expect(activeFixes[0].issueId).toBe("ISS-0001");
  });

  it("includes in-progress issues in activeFixes", () => {
    const { activeFixes } = parsePlan([inProgressIssue]);
    expect(activeFixes).toHaveLength(1);
    expect(activeFixes[0].issueId).toBe("ISS-0002");
  });

  it("excludes resolved issues from activeFixes", () => {
    const { activeFixes } = parsePlan([resolvedIssue]);
    expect(activeFixes).toHaveLength(0);
  });

  it("includes both open and in-progress but not resolved", () => {
    const { activeFixes } = parsePlan([openIssue, inProgressIssue, resolvedIssue]);
    expect(activeFixes).toHaveLength(2);
    const ids = activeFixes.map((f) => f.issueId);
    expect(ids).toContain("ISS-0001");
    expect(ids).toContain("ISS-0002");
    expect(ids).not.toContain("ISS-0003");
  });

  it("preserves severity on each active fix", () => {
    const { activeFixes } = parsePlan([openIssue, inProgressIssue]);
    const fix1 = activeFixes.find((f) => f.issueId === "ISS-0001")!;
    const fix2 = activeFixes.find((f) => f.issueId === "ISS-0002")!;
    expect(fix1.severity).toBe("high");
    expect(fix2.severity).toBe("medium");
  });

  it("preserves linkedTasks on each active fix", () => {
    const { activeFixes } = parsePlan([inProgressIssue]);
    expect(activeFixes[0].linkedTasks).toEqual(["TSK-000002", "TSK-000003"]);
  });

  it("preserves status on each active fix", () => {
    const { activeFixes } = parsePlan([openIssue, inProgressIssue]);
    const fix1 = activeFixes.find((f) => f.issueId === "ISS-0001")!;
    const fix2 = activeFixes.find((f) => f.issueId === "ISS-0002")!;
    expect(fix1.status).toBe("open");
    expect(fix2.status).toBe("in-progress");
  });

  it("produces empty activeFixes when all issues are resolved", () => {
    const { activeFixes } = parsePlan([resolvedIssue]);
    expect(activeFixes).toHaveLength(0);
  });

  it("produces empty activeFixes when issues list is empty", () => {
    const { activeFixes } = parsePlan([]);
    expect(activeFixes).toHaveLength(0);
  });

  it("includes $schema and generatedAt in output", () => {
    const plan = parsePlan([openIssue]);
    expect(plan.$schema).toBe("../../schemas/fix.schema.json");
    expect(typeof plan.generatedAt).toBe("string");
    expect(new Date(plan.generatedAt).getTime()).not.toBeNaN();
  });

  it("handles multiple open issues with single linked task each", () => {
    const issues: IssueLike[] = [
      { id: "ISS-A", severity: "critical", linkedTasks: ["TSK-000010"], status: "open" },
      { id: "ISS-B", severity: "low", linkedTasks: ["TSK-000011"], status: "open" },
    ];
    const { activeFixes } = parsePlan(issues);
    expect(activeFixes).toHaveLength(2);
    expect(activeFixes.map((f) => f.issueId).sort()).toEqual(["ISS-A", "ISS-B"]);
  });

  it("non-plan.json paths return content or null without throwing", () => {
    expect(() => getFixFileContent("patch.diff", [])).not.toThrow();
    expect(() => getFixFileContent("apply.log", [])).not.toThrow();
  });
});

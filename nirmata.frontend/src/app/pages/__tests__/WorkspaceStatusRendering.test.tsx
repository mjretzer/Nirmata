/**
 * Tests for workspace list/status rendering — git-required readiness rules.
 *
 * Covers:
 *  1. WorkspaceStatusBadge renders the correct label for every known status value.
 *  2. WorkspaceLauncherPage recent-workspace list shows the badge and repo root for each
 *     workspace, with the correct status label derived from git/AOS readiness.
 *
 * Spec reference:
 *  - "initialized" (git + .aos present) → healthy → "Healthy"
 *  - "not-initialized" (missing git or .aos) → needs-init → "Needs Init"
 *  - "missing" (path gone) → missing-path → "Missing Path"
 *  - "inaccessible" → invalid → "Invalid"
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { WorkspaceStatusBadge } from "../../components/WorkspaceStatusBadge";
import { WorkspaceLauncherPage } from "../WorkspaceLauncherPage";
import type { WorkspaceSummary } from "../../hooks/useAosData";

// ── Module mocks ─────────────────────────────────────────────────────────

vi.mock("../../hooks/useAosData", () => ({
  useWorkspaces: vi.fn(),
  useRegisterWorkspace: vi.fn(),
  useBootstrapWorkspace: vi.fn(),
}));

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() },
}));

const mockNavigate = vi.fn();
vi.mock("react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router")>();
  return { ...actual, useNavigate: () => mockNavigate };
});

// ── Imports that resolve after mock hoisting ──────────────────────────────

import {
  useWorkspaces,
  useRegisterWorkspace,
  useBootstrapWorkspace,
} from "../../hooks/useAosData";

// ── Helpers ───────────────────────────────────────────────────────────────

const matchMediaStub = () => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: vi.fn().mockImplementation(() => ({
      matches: false,
      media: "",
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
};

/**
 * Workspace badges and project names appear in the "Recent Workspaces" section
 * on the main page body (always visible by default, no dialog required).
 * Target that list via its aria-label to avoid duplicates with the Open dialog.
 */
const getRecentList = () => screen.getByRole("list", { name: /Recent workspaces/i });

function makeWorkspace(
  overrides: Partial<WorkspaceSummary> & { id: string; projectName: string; status: WorkspaceSummary["status"] }
): WorkspaceSummary {
  return {
    repoRoot: "/home/user/projects/test",
    alias: null,
    lastOpened: new Date().toISOString(),
    lastScanned: new Date().toISOString(),
    pinned: false,
    isGitRepo: false,
    hasAosDir: false,
    hasProjectSpec: false,
    hasRoadmap: false,
    hasTaskPlans: false,
    hasHandoff: false,
    cursor: { milestone: "", phase: "", task: null },
    lastRun: { id: "", status: "success", timestamp: "" },
    validation: {
      schemas: "invalid",
      spec: "invalid",
      state: "invalid",
      evidence: "invalid",
      codebase: "invalid",
    },
    lastValidationAt: "",
    openIssuesCount: 0,
    openTodosCount: 0,
    ...overrides,
  };
}

const renderLauncher = () =>
  render(
    <MemoryRouter>
      <WorkspaceLauncherPage />
    </MemoryRouter>
  );

// ── Suite 1 — WorkspaceStatusBadge ───────────────────────────────────────

describe("WorkspaceStatusBadge", () => {
  it("renders 'Healthy' for the healthy status", () => {
    render(<WorkspaceStatusBadge status="healthy" />);
    expect(screen.getByText("Healthy")).toBeInTheDocument();
  });

  it("renders 'Needs Init' for the needs-init status (no git or .aos)", () => {
    render(<WorkspaceStatusBadge status="needs-init" />);
    expect(screen.getByText("Needs Init")).toBeInTheDocument();
  });

  it("renders 'Missing Path' for the missing-path status", () => {
    render(<WorkspaceStatusBadge status="missing-path" />);
    expect(screen.getByText("Missing Path")).toBeInTheDocument();
  });

  it("renders 'Invalid' for the invalid status", () => {
    render(<WorkspaceStatusBadge status="invalid" />);
    expect(screen.getByText("Invalid")).toBeInTheDocument();
  });

  it("renders 'Repair Needed' for the repair-needed status", () => {
    render(<WorkspaceStatusBadge status="repair-needed" />);
    expect(screen.getByText("Repair Needed")).toBeInTheDocument();
  });
});

// ── Suite 2 — Workspace list status rendering in WorkspaceLauncherPage ───

describe("WorkspaceLauncherPage — workspace list status rendering", () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    matchMediaStub();

    vi.mocked(useRegisterWorkspace).mockReturnValue({
      register: vi.fn(),
      isRegistering: false,
    });
    vi.mocked(useBootstrapWorkspace).mockReturnValue({
      bootstrap: vi.fn(),
      isBootstrapping: false,
    });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("shows 'Healthy' badge for a fully initialized workspace (git + .aos present)", () => {
    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [
        makeWorkspace({ id: "ws-1", projectName: "my-app", status: "healthy", repoRoot: "/home/user/my-app" }),
      ],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

    renderLauncher();

    const list = getRecentList();
    expect(list).toHaveTextContent("Healthy");
  });

  it("shows 'Needs Init' badge for a workspace missing git or .aos", () => {
    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [
        makeWorkspace({ id: "ws-2", projectName: "no-git-app", status: "needs-init", repoRoot: "/home/user/no-git-app" }),
      ],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

    renderLauncher();

    const list = getRecentList();
    expect(list).toHaveTextContent("Needs Init");
  });

  it("shows 'Missing Path' badge for a workspace whose root no longer exists", () => {
    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [
        makeWorkspace({ id: "ws-3", projectName: "gone-app", status: "missing-path", repoRoot: "/gone/path" }),
      ],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

    renderLauncher();

    const list = getRecentList();
    expect(list).toHaveTextContent("Missing Path");
  });

  it("shows 'Invalid' badge for an inaccessible workspace", () => {
    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [
        makeWorkspace({ id: "ws-4", projectName: "locked-app", status: "invalid", repoRoot: "/locked/path" }),
      ],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

    renderLauncher();

    const list = getRecentList();
    expect(list).toHaveTextContent("Invalid");
  });

  it("renders one badge per workspace in a mixed-status list", () => {
    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [
        makeWorkspace({ id: "ws-a", projectName: "alpha", status: "healthy" }),
        makeWorkspace({ id: "ws-b", projectName: "beta", status: "needs-init" }),
        makeWorkspace({ id: "ws-c", projectName: "gamma", status: "missing-path" }),
      ],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

    renderLauncher();

    const list = getRecentList();
    expect(list).toHaveTextContent("Healthy");
    expect(list).toHaveTextContent("Needs Init");
    expect(list).toHaveTextContent("Missing Path");
  });

  it("shows the repo root path for each workspace in the list", () => {
    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [
        makeWorkspace({ id: "ws-1", projectName: "my-app", status: "healthy", repoRoot: "/home/user/my-app" }),
      ],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

    renderLauncher();

    const list = getRecentList();
    expect(list).toHaveTextContent("/home/user/my-app");
  });

  it("shows the empty-state message when there are no saved workspaces", () => {
    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

    renderLauncher();

    expect(screen.getByText(/No recent workspaces/i)).toBeInTheDocument();
  });

  it("navigates to the workspace when its Open button is clicked", () => {
    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [
        makeWorkspace({ id: "ws-1", projectName: "my-app", status: "healthy" }),
      ],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

    renderLauncher();

    fireEvent.click(screen.getByRole("button", { name: /Open workspace my-app/i }));

    expect(mockNavigate).toHaveBeenCalledWith("/ws/ws-1");
  });
});

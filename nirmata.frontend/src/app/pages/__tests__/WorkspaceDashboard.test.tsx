import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { WorkspaceDashboard } from "../WorkspaceDashboard";

vi.mock("../../hooks/useAosData", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../hooks/useAosData")>();
  return {
    ...actual,
    useWorkspaces: vi.fn(),
    useWorkspace: vi.fn(),
    useCodebaseIntel: vi.fn(),
    useOrchestratorState: vi.fn(),
    useRegisterWorkspace: vi.fn(),
    useBootstrapWorkspace: vi.fn(),
  };
});

vi.mock("../../components/workspace-config-panel", () => ({
  WorkspaceConfigPanel: () => <div data-testid="workspace-config-panel" />,
}));

vi.mock("../../components/WorkspaceStatusBadge", () => ({
  WorkspaceStatusBadge: ({ status }: { status: string }) => <span>{status}</span>,
}));

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() },
}));

import {
  useWorkspaces,
  useWorkspace,
  useCodebaseIntel,
  useOrchestratorState,
  useRegisterWorkspace,
  useBootstrapWorkspace,
} from "../../hooks/useAosData";

const mockUseWorkspaces = vi.mocked(useWorkspaces);
const mockUseWorkspace = vi.mocked(useWorkspace);
const mockUseCodebaseIntel = vi.mocked(useCodebaseIntel);
const mockUseOrchestratorState = vi.mocked(useOrchestratorState);
const mockUseRegisterWorkspace = vi.mocked(useRegisterWorkspace);
const mockUseBootstrapWorkspace = vi.mocked(useBootstrapWorkspace);

function renderRoute(path: string) {
  const router = createMemoryRouter(
    [
      {
        path: "/ws/:workspaceId",
        element: <WorkspaceDashboard />,
      },
    ],
    { initialEntries: [path] }
  );

  return render(<RouterProvider router={router} />);
}

describe("WorkspaceDashboard", () => {
  beforeEach(() => {
    mockUseWorkspaces.mockReturnValue({
      workspaces: [],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    } as any);

    mockUseWorkspace.mockReturnValue({
      workspace: {
        repoRoot: "C:\\Repos\\fresh-workspace",
        projectName: "fresh-workspace",
        hasAosDir: true,
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
      },
      isLoading: false,
      notFound: false,
      bootstrapDiagnostic: null,
    } as any);

    mockUseCodebaseIntel.mockReturnValue({ artifacts: [], languages: [], stack: [], isLoading: false } as any);
    mockUseOrchestratorState.mockReturnValue({
      runnableGate: {
        taskId: "",
        taskName: "",
        phaseId: "",
        phaseTitle: "",
        runnable: true,
        checks: [],
        recommendedAction: "execute-plan",
      },
      blockedGate: {
        taskId: "",
        taskName: "",
        phaseId: "",
        phaseTitle: "",
        runnable: false,
        checks: [],
        recommendedAction: "",
      },
      gateKindMeta: {},
      timelineTemplate: [],
      isLoading: false,
    } as any);
    mockUseRegisterWorkspace.mockReturnValue({ register: vi.fn(), isRegistering: false } as any);
    mockUseBootstrapWorkspace.mockReturnValue({ bootstrap: vi.fn(), isBootstrapping: false } as any);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("renders the newly created workspace from the direct workspace lookup when the list is stale", () => {
    renderRoute("/ws/11111111-1111-1111-1111-111111111111");

    expect(screen.getByRole("heading", { name: /fresh-workspace/i })).toBeInTheDocument();
    expect(screen.getByText(/C:\\Repos\\fresh-workspace/i)).toBeInTheDocument();
    expect(screen.queryByText(/Workspace not found/i)).not.toBeInTheDocument();
  });
});

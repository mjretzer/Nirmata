import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { PlanPage } from "../PlanPage";

vi.mock("../../hooks/useAosData", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../hooks/useAosData")>();
  return {
    ...actual,
    usePhases: vi.fn(),
    useFileSystem: vi.fn(),
  };
});

vi.mock("../../components/plan/RoadmapTimeline", () => ({
  RoadmapTimeline: () => <div data-testid="roadmap-timeline" />,
}));

vi.mock("../../components/plan/PhaseTaskList", () => ({
  PhaseTaskList: ({ phase }: { phase: { id: string } }) => (
    <div data-testid="phase-task-list" data-phase-id={phase.id} />
  ),
}));

vi.mock("../../components/viewers/DefaultFileViewer", () => ({
  DefaultFileViewer: ({ path }: { path: string }) => (
    <div data-testid="default-file-viewer" data-path={path} />
  ),
}));

import { usePhases, useFileSystem } from "../../hooks/useAosData";

const mockUsePhases = vi.mocked(usePhases);
const mockUseFileSystem = vi.mocked(useFileSystem);

const workspaceId = "c56a4180-65aa-42ec-a945-5fd21dec0538";

function renderPlanRoute(path: string) {
  const router = createMemoryRouter(
    [
      { path: "ws/:workspaceId/files/.aos/spec", element: <PlanPage /> },
      { path: "ws/:workspaceId/files/.aos/spec/*", element: <PlanPage /> },
    ],
    { initialEntries: [path] }
  );
  return render(<RouterProvider router={router} />);
}

const knownPhase = {
  id: "PH-0001",
  title: "Foundation",
  status: "in-progress",
  order: 1,
  milestoneId: "MS-0001",
  summary: "Phase 1 summary",
  brief: { priorities: [], constraints: [] },
  deliverables: [],
  links: { artifacts: [] },
  acceptance: { criteria: [] },
  metadata: { updatedAt: "2026-01-01", owner: "team" },
};

const existingFileNode = {
  id: ".aos/spec/tasks/TSK-000001/plan.json",
  name: "plan.json",
  type: "file" as const,
  path: ".aos/spec/tasks/TSK-000001/plan.json",
};

describe("PlanPage", () => {
  beforeEach(() => {
    mockUsePhases.mockReturnValue({
      phases: [knownPhase],
      isLoading: false,
    } as any);

    mockUseFileSystem.mockReturnValue({
      node: null,
      fileSystem: [],
      findNode: vi.fn(),
      isLoading: false,
    } as any);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  // 3.1 Root roadmap lens
  it("renders roadmap timeline for the .aos/spec root path", () => {
    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec`);

    expect(screen.getByTestId("roadmap-timeline")).toBeInTheDocument();
    expect(screen.queryByTestId("phase-task-list")).not.toBeInTheDocument();
  });

  // 3.1 Known phase directory lens
  it("renders phase task list for a known phase directory path", () => {
    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/phases/PH-0001`);

    expect(screen.getByTestId("phase-task-list")).toBeInTheDocument();
    expect(screen.getByTestId("phase-task-list").getAttribute("data-phase-id")).toBe("PH-0001");
    expect(screen.queryByTestId("roadmap-timeline")).not.toBeInTheDocument();
  });

  // 3.1 Unknown phase directory → missing-artifact state
  it("renders missing-artifact state for an unknown phase directory path", () => {
    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/phases/PH-9999`);

    expect(screen.getByText("Phase Not Found")).toBeInTheDocument();
    expect(screen.queryByTestId("phase-task-list")).not.toBeInTheDocument();
    expect(screen.queryByTestId("roadmap-timeline")).not.toBeInTheDocument();
  });

  it("includes the requested path in the missing-artifact state for unknown phase", () => {
    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/phases/PH-9999`);

    expect(screen.getAllByText(/phases\/PH-9999/).length).toBeGreaterThan(0);
  });

  // 3.1 Phase file artifact uses artifact viewer, not phase directory lens
  it("uses artifact viewer for phase.json file path, not the phase directory lens", () => {
    mockUseFileSystem.mockReturnValue({
      node: { id: ".aos/spec/phases/PH-0001/phase.json", name: "phase.json", type: "file" as const, path: ".aos/spec/phases/PH-0001/phase.json" },
      fileSystem: [],
      findNode: vi.fn(),
      isLoading: false,
    } as any);

    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/phases/PH-0001/phase.json`);

    expect(screen.getByTestId("default-file-viewer")).toBeInTheDocument();
    expect(screen.queryByTestId("phase-task-list")).not.toBeInTheDocument();
  });

  // 3.2 Missing artifact routes do not render synthetic JSON fallback
  it("does not render synthetic JSON for a missing plan.json artifact path", () => {
    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/tasks/TSK-000001/plan.json`);

    expect(screen.queryByTestId("default-file-viewer")).not.toBeInTheDocument();
    expect(screen.getByText("Artifact Not Found")).toBeInTheDocument();
  });

  it("does not render synthetic JSON for a missing roadmap.json path", () => {
    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/roadmap.json`);

    expect(screen.queryByTestId("default-file-viewer")).not.toBeInTheDocument();
    expect(screen.getByText("Artifact Not Found")).toBeInTheDocument();
  });

  it("does not render synthetic JSON for a missing milestones/index.json path", () => {
    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/milestones/index.json`);

    expect(screen.queryByTestId("default-file-viewer")).not.toBeInTheDocument();
    expect(screen.getByText("Artifact Not Found")).toBeInTheDocument();
  });

  // Existing artifact uses workspace-backed viewer
  it("renders DefaultFileViewer with workspace-backed node for an existing artifact", () => {
    mockUseFileSystem.mockReturnValue({
      node: existingFileNode,
      fileSystem: [],
      findNode: vi.fn(),
      isLoading: false,
    } as any);

    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/tasks/TSK-000001/plan.json`);

    expect(screen.getByTestId("default-file-viewer")).toBeInTheDocument();
    expect(screen.getByTestId("default-file-viewer").getAttribute("data-path")).toBe(
      ".aos/spec/tasks/TSK-000001/plan.json"
    );
    expect(screen.queryByText("Artifact Not Found")).not.toBeInTheDocument();
  });

  // 3.3 Loading state — file fetch in progress
  it("renders loading state while file artifact data is being fetched", () => {
    mockUseFileSystem.mockReturnValue({
      node: null,
      fileSystem: [],
      findNode: vi.fn(),
      isLoading: true,
    } as any);

    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/tasks/TSK-000001/plan.json`);

    expect(screen.getByText(/Loading tasks\/TSK-000001\/plan\.json/)).toBeInTheDocument();
    expect(screen.queryByTestId("default-file-viewer")).not.toBeInTheDocument();
    expect(screen.queryByText("Artifact Not Found")).not.toBeInTheDocument();
  });

  // 3.3 Loading state — phase data in progress for phase directory
  it("renders loading state while phase data is still loading for a phase directory path", () => {
    mockUsePhases.mockReturnValue({
      phases: [],
      isLoading: true,
    } as any);

    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/phases/PH-0001`);

    expect(screen.getByText(/Loading phases\/PH-0001/)).toBeInTheDocument();
    expect(screen.queryByTestId("phase-task-list")).not.toBeInTheDocument();
    expect(screen.queryByText("Phase Not Found")).not.toBeInTheDocument();
  });

  // 3.3 Error / missing-artifact state — workspace fetch returns null
  it("renders artifact-not-found state when workspace file request fails", () => {
    mockUseFileSystem.mockReturnValue({
      node: null,
      fileSystem: [],
      findNode: vi.fn(),
      isLoading: false,
    } as any);

    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/tasks/TSK-000001/plan.json`);

    expect(screen.getByText("Artifact Not Found")).toBeInTheDocument();
    expect(screen.queryByTestId("default-file-viewer")).not.toBeInTheDocument();
  });

  // 3.3 Missing-artifact state includes path context
  it("includes the requested path in the missing-artifact state", () => {
    renderPlanRoute(`/ws/${workspaceId}/files/.aos/spec/tasks/TSK-000001/plan.json`);

    expect(screen.getAllByText(/tasks\/TSK-000001\/plan\.json/).length).toBeGreaterThan(0);
  });
});

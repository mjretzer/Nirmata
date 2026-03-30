/**
 * Tests for SettingsWorkspacePage (WorkspaceTab) — bootstrap-gated root path save flow.
 *
 * Covers: bootstrap diagnostic banner, save-root-path success/failure paths,
 * disabled states, and toast messaging.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { SettingsWorkspacePage } from "../SettingsPage";

// ── Module mocks ─────────────────────────────────────────────────────────

vi.mock("../../hooks/useAosData", () => ({
  useWorkspace: vi.fn(),
  useWorkspaceInit: vi.fn(),
  useBootstrapWorkspace: vi.fn(),
  useWorkspaces: vi.fn(),
  isGuidWorkspaceId: vi.fn().mockReturnValue(false),
}));

vi.mock("../../context/WorkspaceContext", () => ({
  useWorkspaceContext: vi.fn().mockReturnValue({
    activeWorkspaceId: null,
    setWorkspaceBootstrapError: vi.fn(),
    engineStatus: "idle",
    setEngineStatus: vi.fn(),
    daemonConnected: false,
    setDaemonConnected: vi.fn(),
    daemonConnectionState: "connecting",
    daemonPollingActive: false,
    reconnect: vi.fn(),
    gitState: { branch: "", remoteUrl: "", hasUncommitted: false, hasUnpushed: false },
    setGitState: vi.fn(),
  }),
}));

vi.mock("../../utils/apiClient", () => ({
  domainClient: {
    updateWorkspace: vi.fn().mockResolvedValue({
      id: "test-id",
      name: "test",
      path: "/test/workspace",
      status: "initialized",
      lastModified: new Date().toISOString(),
    }),
    bootstrapWorkspace: vi.fn(),
  },
}));

vi.mock("sonner", () => ({
  toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() },
}));

const mockNavigate = vi.fn();
vi.mock("react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router")>();
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    // Supply a root path via location state so the save button is initially visible
    useLocation: () => ({
      state: { rootPath: "/test/workspace" },
      pathname: "/ws/test-ws/settings/workspace",
    }),
    useParams: () => ({ workspaceId: undefined }),
    useOutletContext: () => ({ workspaceId: undefined }),
  };
});

// ── Imports that resolve after mock hoisting ──────────────────────────────

import {
  useWorkspace,
  useWorkspaceInit,
  useBootstrapWorkspace,
  useWorkspaces,
} from "../../hooks/useAosData";
import { toast } from "sonner";

// ── Shared test data ──────────────────────────────────────────────────────

const baseWorkspace = {
  repoRoot: "/test/workspace",
  projectName: "test",
  hasAosDir: false,
  hasProjectSpec: false,
  hasRoadmap: false,
  hasTaskPlans: false,
  hasHandoff: false,
  cursor: { milestone: "", phase: "", task: null as string | null },
  lastRun: { id: "", status: "success" as const, timestamp: "" },
  validation: {
    schemas: "invalid" as const,
    spec: "invalid" as const,
    state: "invalid" as const,
    evidence: "invalid" as const,
    codebase: "invalid" as const,
  },
  lastValidationAt: "",
  openIssuesCount: 0,
  openTodosCount: 0,
};

const mockBootstrap = vi.fn();

const renderPage = () =>
  render(
    <MemoryRouter>
      <SettingsWorkspacePage />
    </MemoryRouter>
  );

// ── Suite ─────────────────────────────────────────────────────────────────

describe("SettingsWorkspacePage — root-path bootstrap gating", () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    mockBootstrap.mockReset();

    vi.mocked(useWorkspace).mockReturnValue({
      workspace: baseWorkspace,
      isLoading: false,
      bootstrapDiagnostic: null,
    });

    vi.mocked(useWorkspaceInit).mockReturnValue({
      init: vi.fn().mockResolvedValue({ ok: false, aosDir: "" }),
      validate: vi.fn().mockResolvedValue({
        schemas: "invalid",
        spec: "invalid",
        state: "invalid",
        evidence: "invalid",
        codebase: "invalid",
      }),
      isIniting: false,
      isValidating: false,
      initResult: null,
      validationResult: null,
    });

    vi.mocked(useBootstrapWorkspace).mockReturnValue({
      bootstrap: mockBootstrap,
      isBootstrapping: false,
    });

    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [],
      isLoading: false,
      errorDiagnostic: null,
      refresh: vi.fn(),
    });

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
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  // ── Bootstrap diagnostic banner ─────────────────────────────────────────

  describe("bootstrap diagnostic banner", () => {
    it("shows the error banner when useWorkspace returns a bootstrapDiagnostic", () => {
      vi.mocked(useWorkspace).mockReturnValue({
        workspace: baseWorkspace,
        isLoading: false,
        bootstrapDiagnostic: "Cannot connect to domain API — check that nirmata.Api is running.",
      });

      renderPage();

      expect(screen.getByText("Workspace bootstrap failed")).toBeInTheDocument();
      expect(
        screen.getByText(
          /Cannot connect to domain API/i
        )
      ).toBeInTheDocument();
    });

    it("does not render the banner when bootstrapDiagnostic is null", () => {
      renderPage();
      expect(screen.queryByText("Workspace bootstrap failed")).not.toBeInTheDocument();
    });
  });

  // ── Save Root Path — core flow ──────────────────────────────────────────

  describe("save root path — bootstrap calls", () => {
    it("calls bootstrapWorkspace with the current root path on save", async () => {
      mockBootstrap.mockResolvedValue({
        success: true,
        gitRepositoryCreated: true,
        aosScaffoldCreated: true,
      });

      renderPage();

      const saveBtn = screen.getByRole("button", { name: /Save Root/i });
      fireEvent.click(saveBtn);

      await waitFor(() =>
        expect(mockBootstrap).toHaveBeenCalledWith("/test/workspace")
      );
    });

    it("shows 'Git repository created.' description on successful bootstrap with new git repo", async () => {
      mockBootstrap.mockResolvedValue({
        success: true,
        gitRepositoryCreated: true,
        aosScaffoldCreated: true,
      });

      renderPage();
      fireEvent.click(screen.getByRole("button", { name: /Save Root/i }));

      await waitFor(() =>
        expect(toast.success).toHaveBeenCalledWith(
          expect.stringContaining("/test/workspace"),
          { description: "Git repository created." }
        )
      );
    });

    it("shows 'Existing git repository found.' description when git was already present", async () => {
      mockBootstrap.mockResolvedValue({
        success: true,
        gitRepositoryCreated: false,
        aosScaffoldCreated: false,
      });

      renderPage();
      fireEvent.click(screen.getByRole("button", { name: /Save Root/i }));

      await waitFor(() =>
        expect(toast.success).toHaveBeenCalledWith(
          expect.stringContaining("/test/workspace"),
          { description: "Existing git repository found." }
        )
      );
    });
  });

  // ── Bootstrap failure paths ─────────────────────────────────────────────

  describe("save root path — bootstrap failure", () => {
    it("shows an error toast when bootstrap returns success: false", async () => {
      mockBootstrap.mockResolvedValue({
        success: false,
        gitRepositoryCreated: false,
        aosScaffoldCreated: false,
        error: "git executable not found",
      });

      renderPage();
      fireEvent.click(screen.getByRole("button", { name: /Save Root/i }));

      await waitFor(() =>
        expect(toast.error).toHaveBeenCalledWith(
          "Workspace initialization failed",
          { description: "git executable not found" }
        )
      );
    });

    it("does not show a success toast when bootstrap fails", async () => {
      mockBootstrap.mockResolvedValue({
        success: false,
        gitRepositoryCreated: false,
        aosScaffoldCreated: false,
        error: "permission denied",
      });

      renderPage();
      fireEvent.click(screen.getByRole("button", { name: /Save Root/i }));

      await waitFor(() => expect(toast.error).toHaveBeenCalled());
      expect(toast.success).not.toHaveBeenCalled();
    });

    it("does not navigate when bootstrap fails", async () => {
      mockBootstrap.mockResolvedValue({
        success: false,
        gitRepositoryCreated: false,
        aosScaffoldCreated: false,
      });

      renderPage();
      fireEvent.click(screen.getByRole("button", { name: /Save Root/i }));

      await waitFor(() => expect(toast.error).toHaveBeenCalled());
      expect(mockNavigate).not.toHaveBeenCalled();
    });
  });

  // ── Busy / disabled states ──────────────────────────────────────────────

  describe("save button disabled states", () => {
    it("shows Initializing… and disables Save Root while bootstrapping", () => {
      vi.mocked(useBootstrapWorkspace).mockReturnValue({
        bootstrap: mockBootstrap,
        isBootstrapping: true,
      });

      renderPage();

      // Button text changes to "Initializing…" while busy
      expect(screen.getByText("Initializing…")).toBeInTheDocument();

      // The save button (now labelled "Initializing…") is disabled
      const saveBtn = screen.getByRole("button", { name: /Initializing/i });
      expect(saveBtn).toBeDisabled();
    });
  });

  // ── Path validation ─────────────────────────────────────────────────────

  describe("path validation", () => {
    it("disables Save Root when the root path is cleared to an invalid value", async () => {
      renderPage();

      // Change the path input to an invalid (relative) path
      const input = screen.getByRole("textbox", { name: /Repository root/i });
      fireEvent.change(input, { target: { value: "relative/path" } });

      await waitFor(() => {
        const saveBtn = screen.getByRole("button", { name: /Save Root/i });
        expect(saveBtn).toBeDisabled();
      });
    });
  });
});

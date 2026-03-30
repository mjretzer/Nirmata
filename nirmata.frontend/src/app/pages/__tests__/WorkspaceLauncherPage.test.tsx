/**
 * Tests for WorkspaceLauncherPage — bootstrap-gated workspace creation flow.
 *
 * Covers the "Init New Project" inline form: bootstrap success/failure paths,
 * input validation, navigation, and toast messaging.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { WorkspaceLauncherPage } from "../WorkspaceLauncherPage";

// ── Module mocks ─────────────────────────────────────────────────────────

vi.mock("../../hooks/useAosData", () => ({
  useWorkspaces: vi.fn(),
  useRegisterWorkspace: vi.fn(),
  useBootstrapWorkspace: vi.fn(),
  useGitHubWorkspaceBootstrap: vi.fn(),
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
  useGitHubWorkspaceBootstrap,
} from "../../hooks/useAosData";
import { toast } from "sonner";

// ── Helpers ───────────────────────────────────────────────────────────────

const mockBootstrap = vi.fn();
const mockRegister = vi.fn();
const mockRefresh = vi.fn();
const mockGitHubStart = vi.fn();

const renderPage = (initialEntries?: string[]) =>
  render(
    <MemoryRouter initialEntries={initialEntries ?? ["/"]}>
      <WorkspaceLauncherPage />
    </MemoryRouter>
  );

const openInitForm = () => {
  fireEvent.click(screen.getByRole("button", { name: /Initialize a new project/i }));
};

const fillInitForm = (name: string, path: string) => {
  fireEvent.change(screen.getByLabelText("Project name"), { target: { value: name } });
  fireEvent.change(screen.getByLabelText("Root path"), { target: { value: path } });
};

const submitInitForm = () => {
  fireEvent.click(screen.getByRole("button", { name: /Continue with Git/i }));
};

// ── Suite ─────────────────────────────────────────────────────────────────

describe("WorkspaceLauncherPage — Init New Project bootstrap flow", () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    mockBootstrap.mockReset();
    mockRegister.mockReset();
    mockRefresh.mockReset();
    mockGitHubStart.mockReset();

    vi.mocked(useWorkspaces).mockReturnValue({
      workspaces: [],
      isLoading: false,
      errorDiagnostic: null,
      refresh: mockRefresh,
    });
    vi.mocked(useRegisterWorkspace).mockReturnValue({
      register: mockRegister,
      isRegistering: false,
    });
    vi.mocked(useBootstrapWorkspace).mockReturnValue({
      bootstrap: mockBootstrap,
      isBootstrapping: false,
    });
    vi.mocked(useGitHubWorkspaceBootstrap).mockReturnValue({
      start: mockGitHubStart,
      isStarting: false,
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

  // ── Form visibility ─────────────────────────────────────────────────────

  describe("form visibility", () => {
    it("renders the Init New Project trigger button", () => {
      renderPage();
      expect(
        screen.getByRole("button", { name: /Initialize a new project/i })
      ).toBeInTheDocument();
    });

    it("shows project name and root path fields after clicking the trigger", () => {
      renderPage();
      openInitForm();
      expect(screen.getByLabelText("Project name")).toBeInTheDocument();
      expect(screen.getByLabelText("Root path")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Continue with Git/i })).toBeInTheDocument();
    });

    it("does not show a local create-workspace action", () => {
      renderPage();
      openInitForm();
      expect(screen.queryByRole("button", { name: /Create Workspace/i })).not.toBeInTheDocument();
    });
  });

  // ── GitHub-connected init ───────────────────────────────────────────────

  describe("GitHub-connected init", () => {
    it("starts the GitHub OAuth flow and redirects to the authorize URL", async () => {
      mockGitHubStart.mockResolvedValue({
        authorizeUrl: "https://github.com/login/oauth/authorize?state=abc",
      });

      renderPage();
      openInitForm();
      fillInitForm("my-app", "/home/user/my-app");
      fireEvent.click(screen.getByRole("button", { name: /Continue with Git/i }));

      await waitFor(() =>
        expect(mockGitHubStart).toHaveBeenCalledWith({
          path: "/home/user/my-app",
          name: "my-app",
          repositoryName: "my-app",
          isPrivate: true,
        })
      );
    });

    it("shows GitHub success feedback when callback state is present", () => {
      renderPage([
        "/?githubBootstrap=success&account=octo-cat&repository=my-app",
      ]);

      openInitForm();

      expect(screen.getByRole("status")).toHaveTextContent(
        "GitHub connection complete"
      );
      expect(screen.getByText(/Account:/i)).toHaveTextContent("octo-cat");
      expect(screen.getByText(/Repo:/i)).toHaveTextContent("my-app");
    });

    it("does not redirect when GitHub bootstrap start returns null", async () => {
      mockGitHubStart.mockResolvedValue(null);

      renderPage();
      openInitForm();
      fillInitForm("my-app", "/home/user/my-app");
      fireEvent.click(screen.getByRole("button", { name: /Continue with Git/i }));

      await waitFor(() => expect(mockGitHubStart).toHaveBeenCalled());
    });

    it("renders GitHub callback failure feedback from the query string", () => {
      renderPage(["/?githubBootstrap=error&message=GitHub%20authorization%20was%20denied"]);

      openInitForm();

      expect(screen.getByRole("alert")).toHaveTextContent(
        "GitHub authorization was denied"
      );
    });
  });

  // ── Bootstrap failure paths ─────────────────────────────────────────────

  describe("GitHub validation and failure", () => {
    it("does not call GitHub bootstrap when required fields are invalid", () => {
      renderPage();
      openInitForm();
      fillInitForm("", "relative/path/here");
      submitInitForm();

      expect(mockGitHubStart).not.toHaveBeenCalled();
      expect(screen.getByText(/absolute path/i)).toBeInTheDocument();
      expect(screen.getByText(/Invalid project name/i)).toBeInTheDocument();
    });

    it("does not navigate when GitHub bootstrap start returns null", async () => {
      mockGitHubStart.mockResolvedValue(null);

      renderPage();
      openInitForm();
      fillInitForm("my-app", "/home/user/my-app");
      submitInitForm();

      await waitFor(() => expect(mockGitHubStart).toHaveBeenCalled());
      expect(mockNavigate).not.toHaveBeenCalled();
    });
  });

  // ── Input validation ────────────────────────────────────────────────────

  describe("input validation", () => {
    it("shows a validation error and does not call bootstrap when path is empty", () => {
      renderPage();
      openInitForm();
      fillInitForm("my-app", "");
      submitInitForm();

      expect(mockGitHubStart).not.toHaveBeenCalled();
      expect(screen.getByText("Path is required")).toBeInTheDocument();
    });

    it("shows a validation error for a relative path", () => {
      renderPage();
      openInitForm();
      fillInitForm("my-app", "relative/path/here");

      // Error appears immediately on change
      expect(screen.getByText(/absolute path/i)).toBeInTheDocument();
    });

    it("does not call GitHub bootstrap when path validation error is present on submit", () => {
      renderPage();
      openInitForm();
      fillInitForm("my-app", "not-absolute");
      submitInitForm();

      expect(mockGitHubStart).not.toHaveBeenCalled();
    });
  });

  // ── Busy state ──────────────────────────────────────────────────────────

  describe("busy state", () => {
    it("shows Connecting… label while GitHub authorization is in progress", () => {
      vi.mocked(useGitHubWorkspaceBootstrap).mockReturnValue({
        start: mockGitHubStart,
        isStarting: true,
      });

      renderPage();
      openInitForm();

      expect(screen.getByText("Connecting…")).toBeInTheDocument();
    });

    it("disables the Git button while busy", () => {
      vi.mocked(useGitHubWorkspaceBootstrap).mockReturnValue({
        start: mockGitHubStart,
        isStarting: true,
      });

      renderPage();
      openInitForm();

      const btn = screen.getByRole("button", { name: /Continue with Git/i });
      expect(btn).toBeDisabled();
    });
  });
});

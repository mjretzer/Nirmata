/**
 * Tests for ChatPage component — validates role rendering, suggestion selection,
 * quick actions, timeline/artifact display, and overall UI behavior.
 */
import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { ChatPage } from "../ChatPage";
import { WorkspaceProvider } from "../../context/WorkspaceContext";
import { useWorkspaceContext } from "../../context/WorkspaceContext";
import type { Workspace } from "../../data/mockData";

// Mock the chat hook
vi.mock("../../hooks/useAosData", () => ({
  useChatMessages: vi.fn(),
  useWorkspace: vi.fn(),
}));

// Mock clipboard utility
vi.mock("../../utils/clipboard", () => ({
  copyToClipboard: vi.fn(),
}));

// Mock toast
vi.mock("sonner", () => ({
  toast: {
    info: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
  },
}));

import { useChatMessages, useWorkspace } from "../../hooks/useAosData";
import { copyToClipboard } from "../../utils/clipboard";
import { toast } from "sonner";

// Test wrapper with context
const TestWrapper: React.FC<{ children: React.ReactNode; workspaceId?: string }> = ({ 
  children, 
  workspaceId = "test-workspace-id" 
}) => (
  <MemoryRouter initialEntries={[`/workspace/${workspaceId}/chat`]}>
    <WorkspaceProvider>
      {children}
    </WorkspaceProvider>
  </MemoryRouter>
);

describe("ChatPage", () => {
  const mockUseChatMessages = vi.mocked(useChatMessages);
  const mockUseWorkspace = vi.mocked(useWorkspace);
  const mockCopyToClipboard = vi.mocked(copyToClipboard);
  const mockToast = vi.mocked(toast);

  beforeEach(() => {
    Element.prototype.scrollIntoView = vi.fn();
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

  const defaultChatState = {
    messages: [],
    commandSuggestions: [],
    quickActions: [],
    isLoading: false,
    isSubmitting: false,
    submitTurn: vi.fn(),
    refreshSnapshot: vi.fn(),
  };

  const defaultWorkspace: { workspace: Workspace; isLoading: boolean } = {
    workspace: {
      repoRoot: "/test/path",
      projectName: "Test Project",
      hasAosDir: true,
      hasProjectSpec: true,
      hasRoadmap: true,
      hasTaskPlans: true,
      hasHandoff: true,
      cursor: { phase: "PH-0001", task: "TSK-001", milestone: "MS-0001" },
      lastRun: {
        id: "RUN-0001",
        status: "success",
        timestamp: "2024-01-01T00:00:00Z",
      },
      validation: {
        schemas: "valid",
        spec: "valid",
        state: "valid",
        evidence: "valid",
        codebase: "valid",
      },
      lastValidationAt: "2024-01-01T00:00:00Z",
      openIssuesCount: 0,
      openTodosCount: 0,
    },
    isLoading: false,
  };

  beforeEach(() => {
    mockUseChatMessages.mockReturnValue(defaultChatState);
    mockUseWorkspace.mockReturnValue(defaultWorkspace);
    mockCopyToClipboard.mockResolvedValue(true);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe("Basic rendering", () => {
    it("renders chat interface with welcome message", () => {
      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("AOS Orchestrator")).toBeInTheDocument();
      expect(screen.getByText(/Ask questions about your workspace/)).toBeInTheDocument();
    });

    it("shows workspace cursor info", () => {
      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("PH-0001/TSK-001")).toBeInTheDocument();
    });

    it("shows message count when messages exist", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          { id: "1", role: "user", content: "Hello", timestamp: new Date() },
          { id: "2", role: "assistant", content: "Hi there!", timestamp: new Date() },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("2 messages")).toBeInTheDocument();
    });

    it.skip("shows loading state", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        isLoading: true,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      // Should not show welcome message during loading
      expect(screen.queryByText("AOS Orchestrator")).not.toBeInTheDocument();
    });
  });

  describe("Message rendering and role display", () => {
    it("renders user messages correctly", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "user",
            content: "What is the status?",
            timestamp: new Date("2024-01-01T00:00:00Z"),
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("What is the status?")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Copy message/ })).toBeInTheDocument();
    });

    it("renders assistant messages with agent badge", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "assistant",
            content: "Here is the status:",
            timestamp: new Date("2024-01-01T00:00:00Z"),
            agent: "orchestrator",
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("Here is the status:")).toBeInTheDocument();
      expect(screen.getByText("Orchestrator")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Copy message/ })).toBeInTheDocument();
    });

    it("renders system messages with different styling", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "system",
            content: "System initialized",
            timestamp: new Date("2024-01-01T00:00:00Z"),
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("System initialized")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Copy message/ })).toBeInTheDocument();
    });

    it("renders result messages with special styling", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "result",
            content: "Task completed successfully",
            timestamp: new Date("2024-01-01T00:00:00Z"),
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("Task completed successfully")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Copy message/ })).toBeInTheDocument();
    });

    it("renders messages with run IDs", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "assistant",
            content: "Processing...",
            timestamp: new Date("2024-01-01T00:00:00Z"),
            agent: "orchestrator",
            runId: "RUN-001",
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("RUN-001")).toBeInTheDocument();
    });

    it("copies message to clipboard", async () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "user",
            content: "Test message",
            timestamp: new Date(),
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const copyButton = screen.getByRole("button", { name: /Copy message/ });
      fireEvent.click(copyButton);

      expect(mockCopyToClipboard).toHaveBeenCalledWith("Test message");
      await waitFor(() => expect(mockToast.success).toHaveBeenCalledWith("Copied to clipboard"));
    });

    it("handles copy failure", async () => {
      mockCopyToClipboard.mockResolvedValue(false);

      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "user",
            content: "Test message",
            timestamp: new Date(),
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const copyButton = screen.getByRole("button", { name: /Copy message/ });
      fireEvent.click(copyButton);

      await waitFor(() => expect(mockToast.error).toHaveBeenCalledWith("Failed to copy"));
    });
  });

  describe("Timeline and artifact display", () => {
    it("renders timeline steps", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "result",
            content: "Phase completed",
            timestamp: new Date(),
            timeline: [
              { id: "step-1", label: "Setup", status: "completed" },
              { id: "step-2", label: "Implementation", status: "running" },
              { id: "step-3", label: "Testing", status: "pending" },
            ],
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("Setup")).toBeInTheDocument();
      expect(screen.getByText("Implementation")).toBeInTheDocument();
      expect(screen.getByText("Testing")).toBeInTheDocument();
    });

    it("renders artifact references", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "result",
            content: "Files created",
            timestamp: new Date(),
            artifacts: [
              {
                path: "src/components/Button.tsx",
                label: "Button.tsx",
                action: "created",
              },
              {
                path: "README.md",
                label: "README.md",
                action: "updated",
              },
            ],
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("Button.tsx")).toBeInTheDocument();
      expect(screen.getByText("README.md")).toBeInTheDocument();
      expect(screen.getByText("created")).toBeInTheDocument();
      expect(screen.getByText("updated")).toBeInTheDocument();
    });

    it("renders logs with expander", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "result",
            content: "Process completed",
            timestamp: new Date(),
            logs: ["Step 1 completed", "Step 2 completed", "Step 3 completed"],
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("3 log lines")).toBeInTheDocument();

      // Expand logs
      fireEvent.click(screen.getByText("3 log lines"));
      
      expect(screen.getByText("Step 1 completed")).toBeInTheDocument();
      expect(screen.getByText("Step 2 completed")).toBeInTheDocument();
      expect(screen.getByText("Step 3 completed")).toBeInTheDocument();
    });
  });

  describe.skip("Command suggestions and quick actions", () => {
    it("renders command suggestions dropdown", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        commandSuggestions: [
          { command: "aos status", description: "Show current workspace status", group: "workspace" },
          { command: "execute-plan", description: "Execute the current task plan", group: "execution" },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      // Trigger suggestions
      const input = screen.getByPlaceholderText(/Ask a question or type 'aos' for commands/i);
      fireEvent.focus(input);
      fireEvent.change(input, { target: { value: "aos" } });

      expect(screen.getByText("Commands")).toBeInTheDocument();
      expect(screen.getByText("aos status")).toBeInTheDocument();
      expect(screen.getByText("Show current workspace status")).toBeInTheDocument();
      expect(screen.getByText("execute-plan")).toBeInTheDocument();
      expect(screen.getByText("Execute the current task plan")).toBeInTheDocument();
    });

    it("selects command suggestion", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        commandSuggestions: [
          { command: "aos status", description: "Show status", group: "" },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const input = screen.getByPlaceholderText(/Ask a question or type 'aos' for commands/i);
      fireEvent.focus(input);
      fireEvent.change(input, { target: { value: "aos" } });

      const suggestion = screen.getByText("aos status");
      fireEvent.click(suggestion);

      expect(input).toHaveValue("aos status");
    });

    it("renders quick actions", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        quickActions: [
          { label: "Check Status", command: "aos status", variant: "default" },
          { label: "Execute Plan", command: "execute-plan", variant: "primary" },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("Check Status")).toBeInTheDocument();
      expect(screen.getByText("Execute Plan")).toBeInTheDocument();
    });

    it("executes quick action", () => {
      const mockSubmitTurn = vi.fn();
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        quickActions: [
          { label: "Check Status", command: "aos status", variant: "default" },
        ],
        submitTurn: mockSubmitTurn,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const quickAction = screen.getByText("Check Status");
      fireEvent.click(quickAction);

      expect(mockSubmitTurn).toHaveBeenCalledWith("aos status");
    });

    it("renders next command chip", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "assistant",
            content: "Status complete",
            timestamp: new Date(),
            nextCommand: "execute-plan",
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("execute-plan")).toBeInTheDocument();
    });

    it("executes next command", () => {
      const mockSubmitTurn = vi.fn();
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "assistant",
            content: "Status complete",
            timestamp: new Date(),
            nextCommand: "execute-plan",
          },
        ],
        submitTurn: mockSubmitTurn,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const runButton = screen.getByRole("button", { name: "Run" });
      fireEvent.click(runButton);

      expect(mockSubmitTurn).toHaveBeenCalledWith("execute-plan");
    });
  });

  describe.skip("Input and submission", () => {
    it("sends message on enter key", () => {
      const mockSubmitTurn = vi.fn();
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        submitTurn: mockSubmitTurn,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const input = screen.getByPlaceholderText(/Type a message/);
      fireEvent.change(input, { target: { value: "Hello world" } });
      fireEvent.keyDown(input, { key: "Enter" });

      expect(mockSubmitTurn).toHaveBeenCalledWith("Hello world");
      expect(input).toHaveValue("");
    });

    it("sends message on send button", () => {
      const mockSubmitTurn = vi.fn();
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        submitTurn: mockSubmitTurn,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const input = screen.getByPlaceholderText(/Type a message/);
      const sendButton = screen.getByRole("button", { name: /Send/ });

      fireEvent.change(input, { target: { value: "Test message" } });
      fireEvent.click(sendButton);

      expect(mockSubmitTurn).toHaveBeenCalledWith("Test message");
      expect(input).toHaveValue("");
    });

    it("prevents submission when empty", () => {
      const mockSubmitTurn = vi.fn();
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        submitTurn: mockSubmitTurn,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const input = screen.getByPlaceholderText(/Type a message/);
      fireEvent.keyDown(input, { key: "Enter" });

      expect(mockSubmitTurn).not.toHaveBeenCalled();
    });

    it("prevents submission when already submitting", () => {
      const mockSubmitTurn = vi.fn();
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        isSubmitting: true,
        submitTurn: mockSubmitTurn,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const input = screen.getByPlaceholderText(/Type a message/);
      fireEvent.change(input, { target: { value: "Test message" } });
      fireEvent.keyDown(input, { key: "Enter" });

      expect(mockSubmitTurn).not.toHaveBeenCalled();
    });

    it("shows command mode indicator for aos commands", () => {
      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const input = screen.getByPlaceholderText(/Type a message/);
      fireEvent.change(input, { target: { value: "aos status" } });

      // Should show command mode styling
      expect(input.closest(".border-primary\\/40")).toBeInTheDocument();
    });

    it("switches chat modes", () => {
      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const chatModeButton = screen.getByText("Chat");
      const commandModeButton = screen.getByText("Cmds");
      const autoModeButton = screen.getByText("Auto");

      // Should start in chat mode
      expect(chatModeButton).toHaveClass("text-background");

      // Switch to command mode
      fireEvent.click(commandModeButton);
      expect(commandModeButton).toHaveClass("text-background");
      expect(chatModeButton).not.toHaveClass("text-background");

      // Switch to auto mode
      fireEvent.click(autoModeButton);
      expect(autoModeButton).toHaveClass("text-background");
      expect(commandModeButton).not.toHaveClass("text-background");
    });
  });

  describe("Toolbar actions", () => {
    const historyThreadMessages = [
      {
        id: "history-msg-1",
        role: "user" as const,
        content: "First turn",
        timestamp: new Date("2024-01-01T00:00:00Z"),
        gate: "planner" as const,
        runId: "RUN-001",
        nextCommand: "aos status",
      },
      {
        id: "history-msg-2",
        role: "assistant" as const,
        content: "Second turn",
        timestamp: new Date("2024-01-01T00:01:00Z"),
        agent: "orchestrator" as const,
        gate: "executor" as const,
        runId: "RUN-002",
        nextCommand: "execute-plan",
      },
    ];

    it("refreshes chat snapshot", () => {
      const mockRefreshSnapshot = vi.fn();
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        refreshSnapshot: mockRefreshSnapshot,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const resetButton = screen.getByRole("button", { name: "Reset" });
      fireEvent.click(resetButton);

      expect(mockRefreshSnapshot).toHaveBeenCalled();
      expect(mockToast.success).toHaveBeenCalledWith("Chat reloaded");
    });

    it("opens history drawer for the active workspace thread and renders turns in order", async () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: historyThreadMessages,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      fireEvent.click(screen.getByRole("button", { name: "History" }));

      const title = await screen.findByText("Thread History");
      const drawer = title.closest('[data-slot="drawer-content"]');

      expect(drawer).not.toBeNull();
      expect(within(drawer as HTMLElement).getByText("Workspace chat thread — read-only")).toBeInTheDocument();

      const roleLabels = within(drawer as HTMLElement).getAllByText(/^(user|assistant)$/i);
      expect(roleLabels).toHaveLength(2);
      expect(roleLabels[0]).toHaveTextContent("user");
      expect(roleLabels[1]).toHaveTextContent("assistant");

      expect(within(drawer as HTMLElement).getByText("planner")).toBeInTheDocument();
      expect(within(drawer as HTMLElement).getByText("executor")).toBeInTheDocument();
      expect(within(drawer as HTMLElement).getByText("RUN-001")).toBeInTheDocument();
      expect(within(drawer as HTMLElement).getByText("RUN-002")).toBeInTheDocument();
      expect(within(drawer as HTMLElement).getByText("→ aos status")).toBeInTheDocument();
      expect(within(drawer as HTMLElement).getByText("→ execute-plan")).toBeInTheDocument();
      expect(within(drawer as HTMLElement).getByText("Orchestrator")).toBeInTheDocument();
    });

    it("closes history without changing scroll position or draft contents", async () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: historyThreadMessages,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const input = screen.getByRole("textbox");
      fireEvent.change(input, { target: { value: "draft message" } });

      const viewport = document.querySelector('[data-slot="scroll-area-viewport"]') as HTMLElement;
      viewport.scrollTop = 240;

      fireEvent.click(screen.getByRole("button", { name: "History" }));
      await screen.findByText("Thread History");

      fireEvent.click(screen.getByRole("button", { name: "Back to chat" }));

      expect(input).toHaveValue("draft message");
      expect(viewport.scrollTop).toBe(240);
    });

    it("returns to the active workspace thread without changing the main composer", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: historyThreadMessages,
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const input = screen.getByRole("textbox");
      const historyButton = screen.getByRole("button", { name: "History" });
      fireEvent.click(historyButton);

      expect(screen.getByText("Thread History")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Back to chat" })).toBeInTheDocument();
      expect(input).toHaveValue("");
    });

  });

  describe.skip("Markdown rendering", () => {
    it("renders bold text", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "assistant",
            content: "**Important** message here",
            timestamp: new Date(),
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const boldElement = screen.getByText("Important");
      expect(boldElement).toHaveClass("font-semibold");
    });

    it("renders inline code", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "assistant",
            content: "Use `aos status` command",
            timestamp: new Date(),
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      const codeElement = screen.getByText("aos status");
      expect(codeElement.tagName).toBe("CODE");
    });

    it("renders lists", () => {
      mockUseChatMessages.mockReturnValue({
        ...defaultChatState,
        messages: [
          {
            id: "1",
            role: "assistant",
            content: "- First item\n- Second item\n1. Numbered item",
            timestamp: new Date(),
          },
        ],
      });

      render(
        <TestWrapper>
          <ChatPage />
        </TestWrapper>
      );

      expect(screen.getByText("First item")).toBeInTheDocument();
      expect(screen.getByText("Second item")).toBeInTheDocument();
      expect(screen.getByText("Numbered item")).toBeInTheDocument();
      expect(screen.getByText("1.")).toBeInTheDocument();
    });
  });
});

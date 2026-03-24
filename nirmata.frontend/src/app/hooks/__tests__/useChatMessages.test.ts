/**
 * Tests for useChatMessages hook — validates chat API integration,
 * message mapping, role rendering, suggestion selection, quick actions,
 * and timeline/artifact display.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { useChatMessages } from "../useAosData";

// Mock the API client
vi.mock("../../utils/apiClient", () => ({
  domainClient: {
    getChatSnapshot: vi.fn(),
    postChatTurn: vi.fn(),
  },
}));

// Mock toast
vi.mock("sonner", () => ({
  toast: {
    error: vi.fn(),
  },
}));

import { domainClient } from "../../utils/apiClient";

describe("useChatMessages", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  describe("API integration and mapping", () => {
    it("returns empty state when no workspaceId provided", () => {
      const { result } = renderHook(() => useChatMessages(undefined));
      
      expect(result.current.isLoading).toBe(false);
      expect(result.current.messages).toEqual([]);
      expect(result.current.commandSuggestions).toEqual([]);
      expect(result.current.quickActions).toEqual([]);
      expect(result.current.isSubmitting).toBe(false);
    });

    it("loads chat snapshot on mount", async () => {
      const mockSnapshot = {
        messages: [
          {
            role: "assistant",
            content: "Hello! How can I help you?",
            timestamp: "2024-01-01T00:00:00Z",
            agentId: "orchestrator",
            gate: null,
            artifacts: [],
            timeline: null,
            nextCommand: "aos status",
            runId: null,
            logs: [],
          },
        ],
        commandSuggestions: [
          {
            command: "aos status",
            description: "Show current workspace status",
          },
        ],
        quickActions: [
          {
            label: "Check Status",
            command: "aos status",
            icon: "info",
          },
        ],
      };

      vi.mocked(domainClient.getChatSnapshot).mockResolvedValue(mockSnapshot);

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      expect(result.current.isLoading).toBe(true);
      
      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(domainClient.getChatSnapshot).toHaveBeenCalledWith("test-workspace-id");
      expect(result.current.messages).toHaveLength(1);
      expect(result.current.messages[0]).toMatchObject({
        role: "assistant",
        content: "Hello! How can I help you?",
        agent: "orchestrator",
        nextCommand: "aos status",
      });
      expect(result.current.commandSuggestions).toHaveLength(1);
      expect(result.current.commandSuggestions[0]).toMatchObject({
        command: "aos status",
        description: "Show current workspace status",
        group: "",
      });
      expect(result.current.quickActions).toHaveLength(1);
      expect(result.current.quickActions[0]).toMatchObject({
        label: "Check Status",
        command: "aos status",
        variant: "default",
      });
    });

    it("handles API errors gracefully", async () => {
      vi.mocked(domainClient.getChatSnapshot).mockRejectedValue(new Error("API Error"));

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.messages).toEqual([]);
      expect(result.current.commandSuggestions).toEqual([]);
      expect(result.current.quickActions).toEqual([]);
    });
  });

  describe("Message submission and role rendering", () => {
    it("submits a chat turn and updates messages", async () => {
      const mockSnapshot = {
        messages: [],
        commandSuggestions: [],
        quickActions: [],
      };

      const mockResponse = {
        role: "assistant",
        content: "Command received: `aos status`",
        timestamp: "2024-01-01T00:01:00Z",
        agentId: "orchestrator",
        gate: {
          runnable: true,
          taskId: "TSK-001",
          taskTitle: "Test task",
          checks: [],
          recommendedAction: "execute-plan",
        },
        artifacts: [],
        timeline: {
          steps: [
            {
              id: "step-1",
              label: "Run status check",
              status: "completed",
            },
          ],
        },
        nextCommand: "execute-plan",
        runId: "RUN-001",
        logs: ["Status check completed"],
      };

      vi.mocked(domainClient.getChatSnapshot).mockResolvedValue(mockSnapshot);
      vi.mocked(domainClient.postChatTurn).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      // Submit a user message
      await act(async () => {
        await result.current.submitTurn("aos status");
      });

      expect(domainClient.postChatTurn).toHaveBeenCalledWith("test-workspace-id", "aos status");
      
      // Should have user message + assistant response
      expect(result.current.messages).toHaveLength(2);
      
      const userMessage = result.current.messages.find(m => m.role === "user");
      expect(userMessage).toMatchObject({
        role: "user",
        content: "aos status",
        command: "aos status",
      });

      const assistantMessage = result.current.messages.find(m => m.role === "assistant");
      expect(assistantMessage).toMatchObject({
        role: "assistant",
        content: "Command received: `aos status`",
        agent: "orchestrator",
        nextCommand: "execute-plan",
        runId: "RUN-001",
      });
      expect(assistantMessage?.timeline).toHaveLength(1);
      expect(assistantMessage?.logs).toEqual(["Status check completed"]);
    });

    it("shows streaming indicator during submission", async () => {
      const mockSnapshot = {
        messages: [],
        commandSuggestions: [],
        quickActions: [],
      };

      const mockResponse = {
        role: "assistant",
        content: "Response",
        timestamp: "2024-01-01T00:01:00Z",
        agentId: "orchestrator",
        gate: null,
        artifacts: [],
        timeline: null,
        nextCommand: null,
        runId: null,
        logs: [],
      };

      vi.mocked(domainClient.getChatSnapshot).mockResolvedValue(mockSnapshot);
      vi.mocked(domainClient.postChatTurn).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(mockResponse), 100))
      );

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      // Start submission
      let submissionPromise: Promise<void>;
      act(() => {
        submissionPromise = result.current.submitTurn("test message");
      });

      // Should show submitting state and streaming placeholder
      expect(result.current.isSubmitting).toBe(true);
      expect(result.current.messages).toHaveLength(2); // user + streaming placeholder
      
      const streamingMessage = result.current.messages.find(m => m.streaming);
      expect(streamingMessage).toMatchObject({
        role: "assistant",
        streaming: true,
        agent: "orchestrator",
      });

      // Wait for completion
      await act(async () => {
        await submissionPromise!;
      });

      expect(result.current.isSubmitting).toBe(false);
      expect(result.current.messages).toHaveLength(2); // user + response
      expect(result.current.messages.some(m => m.streaming)).toBe(false);
    });

    it("handles submission errors", async () => {
      const mockSnapshot = {
        messages: [],
        commandSuggestions: [],
        quickActions: [],
      };

      vi.mocked(domainClient.getChatSnapshot).mockResolvedValue(mockSnapshot);
      vi.mocked(domainClient.postChatTurn).mockRejectedValue(new Error("Submission failed"));

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      await act(async () => {
        await result.current.submitTurn("test message");
      });

      // Should show error toast and remove streaming placeholder
      expect(result.current.isSubmitting).toBe(false);
      expect(result.current.messages).toHaveLength(1); // only user message
      expect(result.current.messages.some(m => m.streaming)).toBe(false);
    });

    it("prevents duplicate submissions", async () => {
      const mockSnapshot = {
        messages: [],
        commandSuggestions: [],
        quickActions: [],
      };

      vi.mocked(domainClient.getChatSnapshot).mockResolvedValue(mockSnapshot);
      vi.mocked(domainClient.postChatTurn).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve({}), 100))
      );

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      // Start first submission
      act(() => {
        result.current.submitTurn("first message");
      });

      expect(result.current.isSubmitting).toBe(true);

      // Try second submission - should be ignored
      act(() => {
        result.current.submitTurn("second message");
      });

      // Should only call API once
      expect(domainClient.postChatTurn).toHaveBeenCalledTimes(1);
      expect(domainClient.postChatTurn).toHaveBeenCalledWith("test-workspace-id", "first message");
    });
  });

  describe("Timeline and artifact display", () => {
    it("maps timeline steps correctly", async () => {
      const mockSnapshot = {
        messages: [],
        commandSuggestions: [],
        quickActions: [],
      };

      const mockResponse = {
        role: "assistant",
        content: "Task completed",
        timestamp: "2024-01-01T00:01:00Z",
        agentId: "orchestrator",
        gate: null,
        artifacts: [],
        timeline: {
          steps: [
            {
              id: "PH-0001",
              label: "Project Setup",
              status: "completed",
            },
            {
              id: "PH-0002",
              label: "Implementation",
              status: "active",
            },
            {
              id: "PH-0003",
              label: "Testing",
              status: "pending",
            },
          ],
        },
        nextCommand: null,
        runId: null,
        logs: [],
      };

      vi.mocked(domainClient.getChatSnapshot).mockResolvedValue(mockSnapshot);
      vi.mocked(domainClient.postChatTurn).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      await act(async () => {
        await result.current.submitTurn("test message");
      });

      const message = result.current.messages.find(m => m.role === "assistant");
      expect(message?.timeline).toHaveLength(3);
      expect(message?.timeline).toEqual([
        {
          id: "PH-0001",
          label: "Project Setup",
          status: "completed",
        },
        {
          id: "PH-0002",
          label: "Implementation",
          status: "active",
        },
        {
          id: "PH-0003",
          label: "Testing",
          status: "pending",
        },
      ]);
    });

    it("maps artifact references correctly", async () => {
      const mockSnapshot = {
        messages: [],
        commandSuggestions: [],
        quickActions: [],
      };

      const mockResponse = {
        role: "assistant",
        content: "Files created",
        timestamp: "2024-01-01T00:01:00Z",
        agentId: "orchestrator",
        gate: null,
        artifacts: [
          "src/components/Button.tsx",
          "src/utils/helpers.ts",
          "README.md",
        ],
        timeline: null,
        nextCommand: null,
        runId: null,
        logs: [],
      };

      vi.mocked(domainClient.getChatSnapshot).mockResolvedValue(mockSnapshot);
      vi.mocked(domainClient.postChatTurn).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      await act(async () => {
        await result.current.submitTurn("test message");
      });

      const message = result.current.messages.find(m => m.role === "assistant");
      expect(message?.artifacts).toHaveLength(3);
      expect(message?.artifacts).toEqual([
        {
          path: "src/components/Button.tsx",
          label: "Button.tsx",
          action: "referenced",
        },
        {
          path: "src/utils/helpers.ts",
          label: "helpers.ts",
          action: "referenced",
        },
        {
          path: "README.md",
          label: "README.md",
          action: "referenced",
        },
      ]);
    });

    it("handles missing timeline and artifacts", async () => {
      const mockSnapshot = {
        messages: [],
        commandSuggestions: [],
        quickActions: [],
      };

      const mockResponse = {
        role: "assistant",
        content: "Simple response",
        timestamp: "2024-01-01T00:01:00Z",
        agentId: "orchestrator",
        gate: null,
        artifacts: [],
        timeline: null,
        nextCommand: null,
        runId: null,
        logs: [],
      };

      vi.mocked(domainClient.getChatSnapshot).mockResolvedValue(mockSnapshot);
      vi.mocked(domainClient.postChatTurn).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      await act(async () => {
        await result.current.submitTurn("test message");
      });

      const message = result.current.messages.find(m => m.role === "assistant");
      expect(message?.artifacts).toBeUndefined();
      expect(message?.timeline).toBeUndefined();
    });
  });

  describe("Suggestion selection and quick actions", () => {
    it("refreshes suggestions after successful turn", async () => {
      const initialSnapshot = {
        messages: [],
        commandSuggestions: [
          { command: "aos status", description: "Show status" },
        ],
        quickActions: [
          { label: "Status", command: "aos status", icon: "info" },
        ],
      };

      const updatedSnapshot = {
        messages: [],
        commandSuggestions: [
          { command: "execute-plan", description: "Execute plan" },
          { command: "aos status", description: "Show status" },
        ],
        quickActions: [
          { label: "Execute", command: "execute-plan", icon: "play" },
          { label: "Status", command: "aos status", icon: "info" },
        ],
      };

      const mockResponse = {
        role: "assistant",
        content: "Response",
        timestamp: "2024-01-01T00:01:00Z",
        agentId: "orchestrator",
        gate: null,
        artifacts: [],
        timeline: null,
        nextCommand: null,
        runId: null,
        logs: [],
      };

      vi.mocked(domainClient.getChatSnapshot)
        .mockResolvedValueOnce(initialSnapshot)
        .mockResolvedValueOnce(updatedSnapshot);
      vi.mocked(domainClient.postChatTurn).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      // Initial suggestions
      expect(result.current.commandSuggestions).toHaveLength(1);
      expect(result.current.quickActions).toHaveLength(1);

      await act(async () => {
        await result.current.submitTurn("test message");
      });

      // Should refresh suggestions
      expect(result.current.commandSuggestions).toHaveLength(2);
      expect(result.current.commandSuggestions[0].command).toBe("execute-plan");
      expect(result.current.quickActions).toHaveLength(2);
      expect(result.current.quickActions[0].command).toBe("execute-plan");
    });

    it("handles suggestion refresh failure gracefully", async () => {
      const mockSnapshot = {
        messages: [],
        commandSuggestions: [{ command: "aos status", description: "Show status" }],
        quickActions: [{ label: "Status", command: "aos status", icon: "info" }],
      };

      const mockResponse = {
        role: "assistant",
        content: "Response",
        timestamp: "2024-01-01T00:01:00Z",
        agentId: "orchestrator",
        gate: null,
        artifacts: [],
        timeline: null,
        nextCommand: null,
        runId: null,
        logs: [],
      };

      vi.mocked(domainClient.getChatSnapshot)
        .mockResolvedValueOnce(mockSnapshot)
        .mockRejectedValueOnce(new Error("Refresh failed"));
      vi.mocked(domainClient.postChatTurn).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      const initialSuggestions = result.current.commandSuggestions;
      const initialActions = result.current.quickActions;

      await act(async () => {
        await result.current.submitTurn("test message");
      });

      // Should keep original suggestions on refresh failure
      expect(result.current.commandSuggestions).toEqual(initialSuggestions);
      expect(result.current.quickActions).toEqual(initialActions);
    });

    it("provides refreshSnapshot function", async () => {
      const mockSnapshot1 = {
        messages: [],
        commandSuggestions: [{ command: "aos status", description: "Show status" }],
        quickActions: [],
      };

      const mockSnapshot2 = {
        messages: [],
        commandSuggestions: [{ command: "execute-plan", description: "Execute plan" }],
        quickActions: [],
      };

      vi.mocked(domainClient.getChatSnapshot)
        .mockResolvedValueOnce(mockSnapshot1)
        .mockResolvedValueOnce(mockSnapshot2);

      const { result } = renderHook(() => useChatMessages("test-workspace-id"));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.commandSuggestions[0].command).toBe("aos status");

      await act(async () => {
        result.current.refreshSnapshot();
      });

      await waitFor(() => {
        expect(result.current.commandSuggestions[0].command).toBe("execute-plan");
      });

      expect(domainClient.getChatSnapshot).toHaveBeenCalledTimes(2);
    });
  });
});

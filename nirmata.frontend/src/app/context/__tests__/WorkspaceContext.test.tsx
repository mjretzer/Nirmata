/**
 * Tests for WorkspaceContext and WorkspaceProvider.
 *
 * Covers:
 *  - Default state shape and initial values
 *  - Daemon health poll: updates daemonConnected based on fetch response
 *  - Daemon health poll: gracefully handles fetch failures
 *
 * The health poll fires immediately on mount, so tests that verify
 * daemonConnected use waitFor to let the async poll resolve.
 */

import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, waitFor, act, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router";
import { WorkspaceLauncherPage } from "../../pages/WorkspaceLauncherPage";
import { WorkspaceProvider, useWorkspaceContext } from "../WorkspaceContext";
import { domainClient } from "../../utils/apiClient";

// Wrapper that provides the WorkspaceContext
function wrapper({ children }: { children: ReactNode }) {
  return <WorkspaceProvider>{children}</WorkspaceProvider>;
}

function startupWrapper({ children }: { children: ReactNode }) {
  return (
    <MemoryRouter initialEntries={["/"]}>
      <WorkspaceProvider>{children}</WorkspaceProvider>
    </MemoryRouter>
  );
}

describe("WorkspaceContext — default state", () => {
  it("daemonConnected starts as false", () => {
    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });
    expect(result.current.daemonConnected).toBe(false);
  });

  it("daemonConnectionState starts as 'connecting'", () => {
    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });
    expect(result.current.daemonConnectionState).toBe("connecting");
  });

  it("engineStatus starts as 'idle'", () => {
    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });
    expect(result.current.engineStatus).toBe("idle");
  });

  it("activeWorkspaceId starts as 'my-app'", () => {
    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });
    expect(result.current.activeWorkspaceId).toBe("my-app");
  });

  it("daemonPollingActive starts as true", () => {
    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });
    expect(result.current.daemonPollingActive).toBe(true);
  });

  it("exposes setActiveWorkspaceId, setEngineStatus, setDaemonConnected, reconnect", () => {
    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });
    expect(typeof result.current.setActiveWorkspaceId).toBe("function");
    expect(typeof result.current.setEngineStatus).toBe("function");
    expect(typeof result.current.setDaemonConnected).toBe("function");
    expect(typeof result.current.reconnect).toBe("function");
  });
});

describe("WorkspaceContext — health poll", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("sets daemonConnected=true when health endpoint returns ok:true", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({ ok: true, version: "1.0.0", uptimeMs: 42 }),
        { status: 200 }
      )
    );

    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });

    await waitFor(() => expect(result.current.daemonConnected).toBe(true));
    expect(result.current.daemonConnectionState).toBe("connected");
  });

  it("leaves daemonConnected=false when health returns ok:false", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({ ok: false, version: "1.0.0", uptimeMs: 0 }),
        { status: 200 }
      )
    );

    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });

    // Give the poll time to fire and resolve
    await waitFor(() => {
      expect(result.current.daemonConnected).toBe(false);
    });
  });

  it("stops polling when health returns ok:false", async () => {
    vi.useFakeTimers();
    const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({ ok: false, version: "1.0.0", uptimeMs: 0 }),
        { status: 200 }
      )
    );

    renderHook(() => useWorkspaceContext(), { wrapper });

    await act(async () => {
      await Promise.resolve();
    });
    expect(fetchSpy).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    expect(fetchSpy).toHaveBeenCalledTimes(1);
  });

  it("leaves daemonConnected=false when fetch rejects (network error)", async () => {
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });

    await waitFor(() => expect(result.current.daemonConnected).toBe(false));
  });

  it("leaves daemonConnected=false when health responds with non-2xx status", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response("Service Unavailable", { status: 503 })
    );

    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });

    await waitFor(() => expect(result.current.daemonConnected).toBe(false));
  });

  it("resets consecutiveFailures on success — daemonPollingActive stays true", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({ ok: true, version: "1.0.0", uptimeMs: 10 }),
        { status: 200 }
      )
    );

    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });

    await waitFor(() => expect(result.current.daemonConnected).toBe(true));
    expect(result.current.daemonPollingActive).toBe(true);
  });
});

describe("WorkspaceContext — startup console noise", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("keeps the browser console quiet during root startup", async () => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});

    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({ ok: true, version: "1.0.0", uptimeMs: 42 }),
        { status: 200 },
      ),
    );
    const workspacesSpy = vi.spyOn(domainClient, "getWorkspaces").mockResolvedValue([]);

    render(
      <WorkspaceLauncherPage />,
      { wrapper: startupWrapper },
    );

    await waitFor(() => expect(workspacesSpy).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByText("AOS Engine")).toBeInTheDocument());

    expect(errorSpy).not.toHaveBeenCalled();
    expect(warnSpy).not.toHaveBeenCalled();
  });
});

describe("WorkspaceContext — polling stop and reconnect", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("sets daemonPollingActive=false after the first network error", async () => {
    vi.useFakeTimers();
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("ECONNREFUSED"));

    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });

    await vi.runAllTimersAsync();

    expect(result.current.daemonPollingActive).toBe(false);
    expect(result.current.daemonConnected).toBe(false);
    expect(result.current.daemonConnectionState).toBe("disconnected");
  });

  it("sets daemonPollingActive=false after the first non-2xx response", async () => {
    vi.useFakeTimers();
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response("Service Unavailable", { status: 503 })
    );

    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });

    await vi.runAllTimersAsync();

    expect(result.current.daemonPollingActive).toBe(false);
    expect(result.current.daemonConnectionState).toBe("disconnected");
  });

  it("reconnect() sets daemonPollingActive back to true", async () => {
    vi.useFakeTimers();
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("ECONNREFUSED"));

    const { result } = renderHook(() => useWorkspaceContext(), { wrapper });

    // Drive the initial failure to stop polling
    await vi.runAllTimersAsync();

    expect(result.current.daemonPollingActive).toBe(false);

    // Reconnect
    act(() => { result.current.reconnect(); });

    expect(result.current.daemonPollingActive).toBe(true);
    expect(result.current.daemonConnectionState).toBe("connecting");
  });

});

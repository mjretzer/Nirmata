import { afterEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { daemonClient } from "../../utils/apiClient";
import { useWorkspaceInit } from "../useAosData";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("useWorkspaceInit", () => {
  it("forwards the selected root path as the daemon working directory", async () => {
    const submitSpy = vi.spyOn(daemonClient, "submitCommand").mockResolvedValue({
      ok: true,
      output: "Created: C:\\Users\\James Lestler\\Desktop\\Projects\\test project\\.aos",
    });

    const { result } = renderHook(() => useWorkspaceInit("550e8400-e29b-41d4-a716-446655440000"));

    await act(async () => {
      await result.current.init("C:\\Users\\James Lestler\\Desktop\\Projects\\test project  ");
    });

    await waitFor(() => expect(result.current.initResult).not.toBeNull());

    expect(submitSpy).toHaveBeenCalledWith({
      argv: ["aos", "init"],
      workingDirectory: "C:\\Users\\James Lestler\\Desktop\\Projects\\test project",
    });
    expect(result.current.initResult).toEqual({
      ok: true,
      aosDir: "C:\\Users\\James Lestler\\Desktop\\Projects\\test project\\.aos",
    });
  });
});

/**
 * Tests for useTimers hooks — validates useLiveClock and useUptime
 * produce expected return shapes and react to running state.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useLiveClock, useUptime } from "../useTimers";

describe("useLiveClock", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("returns a Date on first render", () => {
    const { result } = renderHook(() => useLiveClock());
    expect(result.current).toBeInstanceOf(Date);
  });

  it("ticks forward after 1 second", () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => useLiveClock());

    const first = result.current.getTime();

    act(() => {
      vi.advanceTimersByTime(1000);
    });

    const second = result.current.getTime();
    expect(second).toBeGreaterThanOrEqual(first);
  });

  it("cleans up interval on unmount", () => {
    vi.useFakeTimers();
    const spy = vi.spyOn(globalThis, "clearInterval");
    const { unmount } = renderHook(() => useLiveClock());
    unmount();
    expect(spy).toHaveBeenCalled();
    spy.mockRestore();
  });
});

describe("useUptime", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("returns a formatted string", () => {
    const { result } = renderHook(() => useUptime(true));
    // Should match "Xh Ym Zs" pattern
    expect(result.current).toMatch(/^\d+h \d+m \d+s$/);
  });

  it("starts at 0h 0m 0s", () => {
    const { result } = renderHook(() => useUptime(true));
    expect(result.current).toBe("0h 0m 0s");
  });

  it("does not tick when running is false", () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => useUptime(false));

    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(result.current).toBe("0h 0m 0s");
  });

  it("increments when running is true", () => {
    vi.useFakeTimers({ now: 0 });
    // Mock Date.now to advance with fake timers
    let mockNow = 0;
    const dateSpy = vi.spyOn(Date, "now").mockImplementation(() => mockNow);

    const { result } = renderHook(() => useUptime(true));

    // Advance 5 seconds
    mockNow = 5000;
    act(() => {
      vi.advanceTimersByTime(5000);
    });

    expect(result.current).toBe("0h 0m 5s");

    dateSpy.mockRestore();
  });

  it("formats hours and minutes correctly", () => {
    vi.useFakeTimers({ now: 0 });
    let mockNow = 0;
    const dateSpy = vi.spyOn(Date, "now").mockImplementation(() => mockNow);

    const { result } = renderHook(() => useUptime(true));

    // Advance 1h 2m 30s = 3750000ms
    mockNow = 3750000;
    act(() => {
      vi.advanceTimersByTime(3750000);
    });

    expect(result.current).toBe("1h 2m 30s");

    dateSpy.mockRestore();
  });

  it("cleans up interval on unmount", () => {
    vi.useFakeTimers();
    const spy = vi.spyOn(globalThis, "clearInterval");
    const { unmount } = renderHook(() => useUptime(true));
    unmount();
    expect(spy).toHaveBeenCalled();
    spy.mockRestore();
  });
});

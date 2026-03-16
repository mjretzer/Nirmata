import { describe, it, expect, vi, afterEach } from "vitest";
import { relativeTime } from "../format";

describe("relativeTime", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns "—" for empty string', () => {
    expect(relativeTime("")).toBe("—");
  });

  it('returns "just now" for a timestamp less than a minute ago', () => {
    const now = new Date().toISOString();
    expect(relativeTime(now)).toBe("just now");
  });

  it("returns minutes ago for recent timestamps", () => {
    vi.useFakeTimers();
    const base = new Date("2026-03-09T12:00:00Z");
    vi.setSystemTime(base);

    const fiveMinAgo = new Date("2026-03-09T11:55:00Z").toISOString();
    expect(relativeTime(fiveMinAgo)).toBe("5m ago");
  });

  it("returns hours ago for older timestamps", () => {
    vi.useFakeTimers();
    const base = new Date("2026-03-09T15:00:00Z");
    vi.setSystemTime(base);

    const threeHoursAgo = new Date("2026-03-09T12:00:00Z").toISOString();
    expect(relativeTime(threeHoursAgo)).toBe("3h ago");
  });

  it("returns days ago for timestamps over 24h", () => {
    vi.useFakeTimers();
    const base = new Date("2026-03-09T12:00:00Z");
    vi.setSystemTime(base);

    const twoDaysAgo = new Date("2026-03-07T12:00:00Z").toISOString();
    expect(relativeTime(twoDaysAgo)).toBe("2d ago");
  });
});

/**
 * Tests for apiClient utilities — error mapping strategy.
 */
import { describe, it, expect } from "vitest";
import { ApiError, mapApiError } from "../apiClient";

describe("mapApiError", () => {
  it("maps ApiError with status 0 to network error", () => {
    const err = new ApiError(0, "Request timed out after 10000ms");
    const mapped = mapApiError(err);
    expect(mapped.kind).toBe("network");
    expect(mapped.message).toBe("Request timed out after 10000ms");
  });

  it("maps ApiError with status 0 (connection refused) to network error", () => {
    const err = new ApiError(0, "Failed to fetch");
    const mapped = mapApiError(err);
    expect(mapped.kind).toBe("network");
    if (mapped.kind === "network") {
      expect(mapped.message).toBe("Failed to fetch");
    }
  });

  it("maps ApiError with 4xx status to server error", () => {
    const err = new ApiError(404, "HTTP 404: Not Found");
    const mapped = mapApiError(err);
    expect(mapped.kind).toBe("server");
    if (mapped.kind === "server") {
      expect(mapped.status).toBe(404);
      expect(mapped.message).toBe("HTTP 404: Not Found");
    }
  });

  it("maps ApiError with 5xx status to server error", () => {
    const err = new ApiError(500, "HTTP 500: Internal Server Error");
    const mapped = mapApiError(err);
    expect(mapped.kind).toBe("server");
    if (mapped.kind === "server") {
      expect(mapped.status).toBe(500);
    }
  });

  it("maps ApiError with 401 to server error", () => {
    const err = new ApiError(401, "HTTP 401: Unauthorized");
    const mapped = mapApiError(err);
    expect(mapped.kind).toBe("server");
    if (mapped.kind === "server") {
      expect(mapped.status).toBe(401);
    }
  });

  it("maps a generic Error to network error", () => {
    const err = new Error("Something unexpected");
    const mapped = mapApiError(err);
    expect(mapped.kind).toBe("network");
    expect(mapped.message).toBe("Something unexpected");
  });

  it("maps a thrown string to network error", () => {
    const mapped = mapApiError("oops");
    expect(mapped.kind).toBe("network");
    expect(mapped.message).toBe("oops");
  });

  it("maps undefined to network error", () => {
    const mapped = mapApiError(undefined);
    expect(mapped.kind).toBe("network");
  });
});

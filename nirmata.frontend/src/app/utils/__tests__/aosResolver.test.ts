/**
 * Tests for the AOS ID → path resolver.
 */
import { describe, it, expect } from "vitest";
import { resolveAosPath, getFileTypeFromId, getAosLink } from "../aosResolver";

describe("resolveAosPath", () => {
  it("resolves task IDs", () => {
    expect(resolveAosPath("TSK-000012")).toBe(".aos/spec/tasks/TSK-000012/task.json");
  });

  it("resolves run IDs", () => {
    expect(resolveAosPath("RUN-2026-02-22T143022Z")).toBe(
      ".aos/evidence/runs/RUN-2026-02-22T143022Z/run.json"
    );
  });

  it("resolves phase IDs", () => {
    expect(resolveAosPath("PH-0002")).toBe(".aos/spec/phases/PH-0002/phase.json");
  });

  it("resolves milestone IDs", () => {
    expect(resolveAosPath("MS-0001")).toBe(".aos/spec/milestones/MS-0001/milestone.json");
  });

  it("resolves issue IDs", () => {
    expect(resolveAosPath("ISS-0001")).toBe(".aos/spec/issues/ISS-0001.json");
  });

  it("resolves UAT IDs", () => {
    expect(resolveAosPath("UAT-000012")).toBe(".aos/spec/uat/UAT-000012.json");
  });

  it("passes through paths starting with .aos", () => {
    expect(resolveAosPath(".aos/state/handoff.json")).toBe(".aos/state/handoff.json");
  });

  it("strips leading slash from absolute paths", () => {
    expect(resolveAosPath("/src/index.ts")).toBe("src/index.ts");
  });

  it("returns empty string for empty input", () => {
    expect(resolveAosPath("")).toBe("");
  });
});

describe("getFileTypeFromId", () => {
  it("classifies task IDs as directory", () => {
    expect(getFileTypeFromId("TSK-000001")).toBe("directory");
  });

  it("classifies issue IDs as file", () => {
    expect(getFileTypeFromId("ISS-0001")).toBe("file");
  });

  it("classifies UAT IDs as file", () => {
    expect(getFileTypeFromId("UAT-000012")).toBe("file");
  });

  it("classifies trailing-slash paths as directory", () => {
    expect(getFileTypeFromId("src/")).toBe("directory");
  });

  it("classifies dotted names as file", () => {
    expect(getFileTypeFromId("config.json")).toBe("file");
  });
});

describe("getAosLink", () => {
  it("generates workspace file links from IDs", () => {
    expect(getAosLink("my-app", "TSK-000012")).toBe(
      "/ws/my-app/files/.aos/spec/tasks/TSK-000012/task.json"
    );
  });

  it("passes through full URL paths", () => {
    expect(getAosLink("my-app", "/ws/other/files/.aos/state")).toBe(
      "/ws/other/files/.aos/state"
    );
  });
});

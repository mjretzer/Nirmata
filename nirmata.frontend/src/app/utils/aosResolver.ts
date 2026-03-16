/**
 * Canonical ID -> File Path Resolver for AOS
 * 
 * Maps logical IDs (Tasks, Runs, Phases, etc.) to their concrete 
 * file system locations within the .aos/ directory structure.
 */

export function resolveAosPath(id: string): string {
  if (!id) return "";

  // 1. Tasks: TSK-* -> .aos/spec/tasks/TSK-*/
  if (id.match(/^TSK-\d+$/) || id.match(/^TSK-\w+$/)) {
    return `.aos/spec/tasks/${id}/task.json`; // Point to the main task object file
  }

  // 2. Runs: RUN-* -> .aos/evidence/runs/RUN-*/
  if (id.startsWith("RUN-")) {
    return `.aos/evidence/runs/${id}/run.json`; // Point to the run header
  }

  // 3. Phases: PH-* -> .aos/spec/phases/PH-*/phase.json
  if (id.startsWith("PH-")) {
    return `.aos/spec/phases/${id}/phase.json`;
  }

  // 4. Milestones: MS-* -> .aos/spec/milestones/MS-*/milestone.json
  if (id.startsWith("MS-")) {
    return `.aos/spec/milestones/${id}/milestone.json`;
  }

  // 5. Issues: ISS-* -> .aos/spec/issues/ISS-*.json
  // Note: Issues are flat files in the new structure
  if (id.startsWith("ISS-")) {
    return `.aos/spec/issues/${id}.json`;
  }
  
  // 6. UAT: UAT-* -> .aos/spec/uat/UAT-*.json
  if (id.startsWith("UAT-")) {
    return `.aos/spec/uat/${id}.json`;
  }

  // 7. Checkpoints: CHK-* -> .aos/state/checkpoints/CHK-*.json
  // Assuming CHK- is followed by a timestamp or ID
  if (id.startsWith("CHK-") || id.match(/^\d{4}-\d{2}-\d{2}T/)) {
    return `.aos/state/checkpoints/${id}.json`;
  }

  // 8. General Fallback or exact paths
  // If it already looks like a path, return it (normalized)
  if (id.startsWith("/") || id.startsWith(".aos") || id.startsWith("src")) {
    return id.startsWith("/") ? id.substring(1) : id;
  }

  return id;
}

export function getFileTypeFromId(id: string): "file" | "directory" | "unknown" {
  // Most IDs now map to directories containing an index file or object file, 
  // but for the purpose of the resolver, we often want to link to the *directory* in the explorer
  // or the *file* for the viewer.
  
  // Actually, standardizing on pointing to the main *file* for the ID is safer for the Viewer.
  // The Explorer should handle expanding parent directories.
  
  if (id.startsWith("TSK-")) return "directory"; // Task is a directory
  if (id.startsWith("RUN-")) return "directory"; // Run is a directory
  if (id.startsWith("MS-")) return "directory"; // Milestone is a directory
  if (id.startsWith("PH-")) return "directory"; // Phase is a directory
  
  if (id.startsWith("ISS-")) return "file";
  if (id.startsWith("UAT-")) return "file";
  if (id.startsWith("CHK-")) return "file";
  
  if (id.endsWith("/")) return "directory";
  if (id.includes(".")) return "file";
  
  return "unknown";
}

export function getAosLink(workspaceId: string, idOrPath: string): string {
  // If it's already a full URL path (e.g. starting with /ws/), just return it
  if (idOrPath.startsWith("/ws/")) return idOrPath;

  const resolvedPath = resolveAosPath(idOrPath);
  return `/ws/${workspaceId}/files/${resolvedPath}`;
}

import { Wrench } from "lucide-react";
import { useParams, Navigate } from "react-router";
import { DefaultFileViewer } from "../components/viewers/DefaultFileViewer";
import { useIssues, useWorkspace, useFileSystem } from "../hooks/useAosData";

function getFixFileContent(relativePath: string, allIssues: { id: string; severity: string; linkedTasks: string[]; status: string }[]): string | null {
  const fileName = relativePath.replace(/\/$/, "");

  if (fileName === "plan.json") {
    const openIssues = allIssues.filter((i) => i.status === "open" || i.status === "in-progress");
    return JSON.stringify({
      "$schema": "../../schemas/fix.schema.json",
      activeFixes: openIssues.map((i) => ({
        issueId: i.id,
        severity: i.severity,
        linkedTasks: i.linkedTasks,
        status: i.status,
      })),
      generatedAt: new Date().toISOString(),
    }, null, 2);
  }

  if (fileName === "patch.diff") {
    return `--- a/src/auth/OAuthProvider.ts\n+++ b/src/auth/OAuthProvider.ts\n@@ -42,7 +42,9 @@\n   const handleCallback = async (code: string) => {\n-    const token = await exchange(code);\n+    const token = await exchange(code, { timeout: 5000 });\n+    if (!token) throw new AuthError('Token exchange failed');\n     return token;\n   };\n`;
  }

  if (fileName === "apply.log") {
    return `[2026-02-24T10:00:00Z] Fix loop initiated for ISS-0001\n[2026-02-24T10:00:01Z] Analyzing impacted files...\n[2026-02-24T10:00:03Z] Generated patch for src/auth/OAuthProvider.ts\n[2026-02-24T10:00:05Z] Patch staged. Awaiting re-verification.\n`;
  }

  return null;
}

export function FixPage() {
  const params = useParams();
  const relativePath = params["*"] || "";
  const { workspace: currentWs } = useWorkspace();
  const { issues: allIssues } = useIssues();
  const { findNode } = useFileSystem();
  const ws = currentWs.projectName;

  // Folder-level → redirect to Verification Hub
  if (relativePath === "") {
    return <Navigate to={`/ws/${ws}/files/.aos/spec/uat`} replace />;
  }

  // File view
  const fullPath = `.aos/spec/fix/${relativePath}`;
  const fileNode = findNode(fullPath.split("/").filter(Boolean));
  const content = getFixFileContent(relativePath, allIssues);

  if (fileNode && fileNode.type === "file") {
    return (
      <div className="flex-1 flex flex-col overflow-hidden bg-background">
        <DefaultFileViewer node={fileNode} path={fullPath} content={content || JSON.stringify({ path: fullPath }, null, 2)} />
      </div>
    );
  }

  if (content) {
    const fileName = relativePath.split("/").pop() || relativePath;
    return (
      <div className="flex-1 flex flex-col overflow-hidden bg-background">
        <DefaultFileViewer node={{ id: `fix-${fileName}`, name: fileName, type: "file" }} path={fullPath} content={content} />
      </div>
    );
  }

  return (
    <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
      <Wrench className="h-12 w-12 mb-4 opacity-20" />
      <p className="text-sm font-mono">Path not found: {relativePath}</p>
    </div>
  );
}

export default FixPage;
import { useParams, useNavigate, useLocation } from "react-router";
import { 
  File, 
  Folder, 
  ChevronRight, 
  Download, 
  FileJson, 
  FileCode, 
  FileText, 
  Shield, 
  ArrowUp
} from "lucide-react";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { type FileSystemNode } from "../hooks/useAosData";
import { DefaultFileViewer } from "../components/viewers/DefaultFileViewer";
import { useWorkspace, useFileSystem } from "../hooks/useAosData";

export function WorkspacePathPage() {
  const params = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { workspace: defaultWs } = useWorkspace();
  const { findNode } = useFileSystem();
  const workspaceId = params.workspaceId || defaultWs.projectName;
  
  let relativePath = params["*"] || "";

  // Map /codebase route to .aos/codebase directory
  if (!relativePath && location.pathname.endsWith("/codebase")) {
     relativePath = ".aos/codebase";
  }

  // Find the node in the file system
  const node = findNode(relativePath.split("/").filter(Boolean));
  
  if (!node) {
    // Path not found state
    const parentPath = relativePath.split("/").slice(0, -1).join("/");
    return (
      <div className="flex flex-col items-center justify-center h-full p-8 text-center">
        <div className="bg-muted p-4 rounded-full mb-4">
          <Folder className="h-8 w-8 text-muted-foreground" />
        </div>
        <h2 className="text-xl font-semibold mb-2">Path not found</h2>
        <p className="text-muted-foreground mb-6 max-w-md">
          The path <span className="font-mono text-xs bg-muted px-1 py-0.5 rounded">{relativePath}</span> does not exist in this workspace.
        </p>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => navigate(-1)}>
            Go Back
          </Button>
          <Button onClick={() => navigate(`/ws/${workspaceId}/files/${parentPath}`)}>
            Go to Parent Directory
          </Button>
        </div>
      </div>
    );
  }

  // ─── FILE VIEW: render DefaultFileViewer full-bleed, no extra chrome ───
  if (node.type === "file") {
    return (
      <div className="flex-1 flex flex-col overflow-hidden bg-background">
        <DefaultFileViewer node={node} path={relativePath} />
      </div>
    );
  }

  // Helper to get icon
  const getIcon = (node: FileSystemNode) => {
    if (node.type === "directory") return <Folder className="h-4 w-4 text-blue-400 fill-blue-400/20" />;
    if (node.name.endsWith(".json")) return <FileJson className="h-4 w-4 text-yellow-500" />;
    if (node.name.endsWith(".ts") || node.name.endsWith(".tsx")) return <FileCode className="h-4 w-4 text-blue-500" />;
    return <FileText className="h-4 w-4 text-muted-foreground" />;
  };

  return (
    <div className="flex h-full bg-background">

      {/* CENTER PANEL: Primary Content */}
      <div className="flex-1 flex flex-col min-w-0">
        
        {/* Header */}
        <div className="h-12 border-b border-border flex items-center justify-between px-4 bg-background">
          <div className="flex items-center gap-2 overflow-hidden">
            <Folder className="h-5 w-5 text-muted-foreground" />
            <h1 className="font-semibold text-sm truncate">{node.name}</h1>
            <Badge variant="outline" className="text-[10px] font-mono font-normal ml-2">{relativePath || "/"}</Badge>
          </div>
          <div className="flex items-center gap-2">
            {relativePath && relativePath !== "" && relativePath !== ".aos/codebase" && (
               <Button variant="ghost" size="sm" onClick={() => {
                 const parent = relativePath.split("/").slice(0, -1).join("/");
                 navigate(`/ws/${workspaceId}/files/${parent}`);
               }}>
                 <ArrowUp className="h-4 w-4 mr-1" />
                 Up
               </Button>
            )}
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-auto bg-muted/5">
          <div className="p-4">
            <div className="rounded-md border border-border bg-card shadow-sm">
              <div className="grid grid-cols-[auto_1fr_auto_auto_auto] gap-4 p-3 border-b border-border bg-muted/40 font-medium text-xs text-muted-foreground uppercase tracking-wider">
                <div className="w-5"></div>
                <div>Name</div>
                <div className="text-right">Size</div>
                <div className="text-right">Last Modified</div>
                <div className="w-8"></div>
              </div>
              <div className="divide-y divide-border">
                {/* Folders First */}
                {node.children?.filter(c => c.type === "directory").map((child, i) => (
                  <div 
                    key={`dir-${i}`} 
                    className="grid grid-cols-[auto_1fr_auto_auto_auto] gap-4 p-3 items-center hover:bg-muted/50 cursor-pointer transition-colors group"
                    onClick={() => navigate(`/ws/${workspaceId}/files/${relativePath ? relativePath + '/' : ''}${child.name}`)}
                  >
                    <Folder className="h-4 w-4 text-blue-400 fill-blue-400/20" />
                    <span className="text-sm font-medium">{child.name}</span>
                    <span className="text-xs text-muted-foreground text-right font-mono">-</span>
                    <span className="text-xs text-muted-foreground text-right font-mono">Today</span>
                    <ChevronRight className="h-4 w-4 text-muted-foreground/30 group-hover:text-muted-foreground" />
                  </div>
                ))}
                {/* Files Second */}
                {node.children?.filter(c => c.type === "file").map((child, i) => (
                  <div 
                    key={`file-${i}`} 
                    className="grid grid-cols-[auto_1fr_auto_auto_auto] gap-4 p-3 items-center hover:bg-muted/50 cursor-pointer transition-colors group"
                    onClick={() => navigate(`/ws/${workspaceId}/files/${relativePath ? relativePath + '/' : ''}${child.name}`)}
                  >
                    {getIcon(child)}
                    <div className="flex items-center gap-2">
                      <span className="text-sm">{child.name}</span>
                      {child.status === "valid" && <Badge variant="outline" className="text-[10px] h-4 px-1 text-green-500 border-green-500/30 bg-green-500/10">Valid</Badge>}
                      {child.status === "warning" && <Badge variant="outline" className="text-[10px] h-4 px-1 text-yellow-500 border-yellow-500/30 bg-yellow-500/10">Warn</Badge>}
                    </div>
                    <span className="text-xs text-muted-foreground text-right font-mono">{child.size}</span>
                    <span className="text-xs text-muted-foreground text-right font-mono">Today</span>
                    <ChevronRight className="h-4 w-4 text-muted-foreground/30 group-hover:text-muted-foreground" />
                  </div>
                ))}
                {(!node.children || node.children.length === 0) && (
                  <div className="p-8 text-center text-muted-foreground text-sm">
                    Empty directory
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* RIGHT PANEL: Context / Metadata */}
      <div className="w-72 border-l border-border bg-background hidden xl:flex flex-col">
        <div className="p-4 border-b border-border">
          <h3 className="font-semibold text-sm mb-1">Context</h3>
          <p className="text-xs text-muted-foreground">Metadata and relations</p>
        </div>
        <div className="p-4 space-y-6">
          <div className="space-y-2">
            <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Info</span>
            <div className="grid grid-cols-2 gap-2 text-sm">
              <span className="text-muted-foreground">Type</span>
              <span className="text-right">{node.type}</span>
              <span className="text-muted-foreground">Size</span>
              <span className="text-right">{node.size || "-"}</span>
              <span className="text-muted-foreground">Modified</span>
              <span className="text-right">Just now</span>
            </div>
          </div>

          {relativePath.startsWith(".aos") && (
            <div className="space-y-2">
              <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">AOS Validation</span>
              <div className="bg-green-500/10 border border-green-500/20 rounded-md p-3 flex items-start gap-2">
                <Shield className="h-4 w-4 text-green-600 mt-0.5" />
                <div>
                  <p className="text-sm font-medium text-green-700">Valid Schema</p>
                  <p className="text-xs text-green-600/80 mt-1">This file conforms to the AOS specification v1.2</p>
                </div>
              </div>
            </div>
          )}

          <div className="space-y-2">
            <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Actions</span>
            <div className="grid grid-cols-1 gap-2">
              <Button variant="outline" size="sm" className="justify-start">
                <Download className="h-4 w-4 mr-2" />
                Download
              </Button>
              <Button variant="outline" size="sm" className="justify-start">
                <Copy className="h-4 w-4 mr-2" />
                Copy Path
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// Icon for Copy was missing in imports, adding locally to avoid re-edit if possible or just rely on generic Button
function Copy({ className }: { className?: string }) {
  return (
    <svg 
      xmlns="http://www.w3.org/2000/svg" 
      width="24" 
      height="24" 
      viewBox="0 0 24 24" 
      fill="none" 
      stroke="currentColor" 
      strokeWidth="2" 
      strokeLinecap="round" 
      strokeLinejoin="round" 
      className={className}
    >
      <rect width="14" height="14" x="8" y="8" rx="2" ry="2" />
      <path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2" />
    </svg>
  );
}
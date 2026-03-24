import { useState, useEffect } from "react";
import {
  ChevronRight,
  ChevronDown,
  File,
  FileText,
  FileJson,
  Folder,
  Code,
  Shield,
  History,
  Database,
  ListChecks,
  MapIcon,
  Zap,
} from "lucide-react";
import { cn } from "../ui/utils";
import { ScrollArea } from "../ui/scroll-area";
import { type FileSystemNode } from "../../hooks/useAosData";
import { useLocation, useNavigate, useSearchParams } from "react-router";
import { getAosLink } from "../../utils/aosResolver";
import { useWorkspace, useFileSystem } from "../../hooks/useAosData";
import { useVerification } from "../../context/VerificationContext";
import { useWorkspaceContext } from "../../context/WorkspaceContext";

// Map folder paths to their operational console labels
const CONSOLE_FOLDERS: Record<string, { label: string; color: string }> = {
  ".aos/spec": { label: "Plan", color: "text-emerald-400 bg-emerald-400/10 border-emerald-400/20" },
  ".aos/spec/uat": { label: "Verification", color: "text-violet-400 bg-violet-400/10 border-violet-400/20" },
  ".aos/spec/issues": { label: "Issues", color: "text-rose-400 bg-rose-400/10 border-rose-400/20" },
  ".aos/evidence/runs": { label: "Runs", color: "text-amber-400 bg-amber-400/10 border-amber-400/20" },
  ".aos/codebase": { label: "Codebase", color: "text-sky-400 bg-sky-400/10 border-sky-400/20" },
  ".aos/state": { label: "Continuity", color: "text-teal-400 bg-teal-400/10 border-teal-400/20" },
};

export function FileExplorer() {
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { checkedUatIds } = useVerification();
  const { activeWorkspaceId } = useWorkspaceContext();
  
  // Expanded IDs
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set([
    "root", 
    ".aos", 
    ".aos/spec", 
    ".aos/evidence", 
    ".aos/state",
    ".aos/schemas",
    ".aos/codebase"
  ]));
  const [selectedPath, setSelectedPath] = useState<string | null>(null);
  const [filter, setFilter] = useState("");

  const { workspace: currentWs } = useWorkspace(activeWorkspaceId);
  const { fileSystem } = useFileSystem();
  const wsId = activeWorkspaceId || location.pathname.split("/")[2] || currentWs.projectName;
  
  // Sync state with location/context
  useEffect(() => {
    // Generic URL sync (works for /files/.aos/spec/... as well)
    const match = location.pathname.match(/\/files\/(.+)$/);
    if (match) {
        const filePath = match[1];
        setSelectedPath(filePath);
        
        // Expand parents + the folder itself if it's a console folder
        const parts = filePath.split('/');
        setExpandedIds(prev => {
            const next = new Set(prev);
            let currentPath = "";
            for (let i = 0; i < parts.length; i++) {
                currentPath = currentPath ? `${currentPath}/${parts[i]}` : parts[i];
                // Always expand parents; also expand the target if it's a console folder
                if (i < parts.length - 1 || CONSOLE_FOLDERS[currentPath]) {
                    next.add(currentPath);
                }
            }
            return next;
        });
        return;
    }

    // Bare /files/.aos/spec (no trailing file) → default to roadmap.json
    if (location.pathname.match(/\/files\/\.aos\/spec\/?$/)) {
      setSelectedPath(".aos/spec");
      setExpandedIds(prev => {
        const next = new Set(prev);
        next.add("root");
        next.add(".aos");
        next.add(".aos/spec");
        return next;
      });
    }
  }, [location.pathname, searchParams]);

  const toggleExpand = (path: string, e?: React.MouseEvent) => {
    e?.stopPropagation();
    const newSet = new Set(expandedIds);
    if (newSet.has(path)) {
      newSet.delete(path);
    } else {
      newSet.add(path);
    }
    setExpandedIds(newSet);
  };

  const handleNodeClick = (node: FileSystemNode, fullPath: string) => {
    setSelectedPath(fullPath);
    if (node.type === "directory") {
      toggleExpand(fullPath);
      // If this folder is an operational console root, navigate to it
      if (CONSOLE_FOLDERS[fullPath]) {
        navigate(getAosLink(wsId, fullPath));
      }
    } else {
      // Navigate to standard file viewer path
      navigate(getAosLink(wsId, fullPath));
    }
  };

  const getIconForNode = (node: FileSystemNode) => {
    if (node.type === "directory") {
      if (node.name === ".aos") return Database;
      if (node.name === "spec") return ListChecks;
      if (node.name === "evidence") return Shield;
      if (node.name === "state") return History;
      if (node.name === "codebase") return Code;
      if (node.name === "schemas") return FileJson;
      if (node.name === "runs") return Zap;
      if (node.name === "roadmap") return MapIcon;
      return Folder;
    }
    if (node.name.endsWith(".json")) return FileJson;
    if (node.name.endsWith(".ts") || node.name.endsWith(".tsx")) return Code;
    if (node.name.endsWith(".md")) return FileText;
    if (node.name.endsWith(".log")) return FileText;
    return File;
  };

  const getFileColor = (node: FileSystemNode) => {
    if (node.type === "directory") return "text-blue-500";
    if (node.name.endsWith(".json")) return "text-yellow-500";
    if (node.name.endsWith(".tsx") || node.name.endsWith(".ts")) return "text-sky-500";
    if (node.name.endsWith(".css")) return "text-blue-400";
    if (node.name.endsWith(".md")) return "text-pink-500";
    if (node.name.endsWith(".log")) return "text-neutral-400";
    return "text-muted-foreground";
  };

  const renderNode = (node: FileSystemNode, parentPath: string = "", level: number = 0) => {
    const isRoot = !parentPath;
    const fullPath = parentPath ? `${parentPath}/${node.name}` : node.name;

    // Filter Logic
    // Always show .aos root and all its children (spec, evidence, state, etc.)
    if (isRoot && node.name !== ".aos") {
      return null;
    }
    
    // Generic filter
    if (filter && node.type === "file" && !node.name.toLowerCase().includes(filter.toLowerCase())) {
      return null;
    }

    const isExpanded = expandedIds.has(fullPath);
    const isSelected = selectedPath === fullPath;
    const Icon = getIconForNode(node);

    return (
      <div key={fullPath}>
        <div
          className={cn(
            "flex items-center gap-1.5 py-1.5 px-3 hover:bg-accent/50 cursor-pointer text-sm select-none group transition-colors",
            isSelected && "bg-accent text-accent-foreground"
          )}
          style={{ paddingLeft: `${level * 12 + 12}px` }}
          onClick={() => handleNodeClick(node, fullPath)}
        >
          <div
            className={cn(
              "p-0.5 rounded-sm hover:bg-muted/80 transition-colors mr-1",
              node.type === "directory" ? "visible" : "invisible"
            )}
            onClick={(e) => toggleExpand(fullPath, e)}
          >
            {node.type === "directory" &&
              (isExpanded ? (
                <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
              ) : (
                <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
              ))}
          </div>
          <Icon className={cn("h-4 w-4 shrink-0", getFileColor(node))} />
          <span className="truncate font-medium text-sm flex-1">{node.name}</span>
          {/* Console badge for operational folders */}
          {CONSOLE_FOLDERS[fullPath] && (
            <span className={cn(
              "text-[9px] font-mono font-semibold uppercase tracking-wider px-1.5 py-0.5 rounded border shrink-0",
              CONSOLE_FOLDERS[fullPath].color
            )}>
              {CONSOLE_FOLDERS[fullPath].label}
            </span>
          )}
          {/* Dot indicator: yellow checked state takes priority over status dots */}
          {(() => {
            // Check if this is a UAT file that's been checked in the table
            const isUatFile = node.name.startsWith("UAT-") && node.name.endsWith(".json");
            const uatId = isUatFile ? node.name.replace(".json", "") : "";
            const isChecked = isUatFile && checkedUatIds.has(uatId);

            if (isChecked) {
              // Yellow dot: checked in UAT table
              return <div className="h-2 w-2 rounded-full bg-yellow-400 shrink-0 animate-in zoom-in-50" />;
            }
            // For UAT files, only show dots when checked — skip status dots
            if (isUatFile) return null;
            if (node.status && node.status !== "valid") {
              // Red/orange status dot for non-UAT files
              return (
                <div className={cn("h-2 w-2 rounded-full shrink-0",
                  node.status === "error" ? "bg-red-500" : "bg-orange-500"
                )} />
              );
            }
            return null;
          })()}
        </div>
        
        {node.type === "directory" && isExpanded && node.children && (
          <div>
            {node.children.map((child) => renderNode(child, fullPath, level + 1))}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="w-80 border-r border-border flex flex-col bg-card h-full">
      <ScrollArea className="flex-1">
        <div className="py-2">
          {fileSystem.map(rootNode => renderNode(rootNode))}
        </div>
      </ScrollArea>
    </div>
  );
}
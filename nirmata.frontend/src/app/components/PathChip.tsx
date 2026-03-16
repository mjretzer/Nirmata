import { Copy, ExternalLink, FileText } from "lucide-react";
import { toast } from "sonner";
import { copyToClipboard } from "../utils/clipboard";
import { Button } from "./ui/button";
import { useWorkspace } from "../hooks/useAosData";
import { resolveAosPath, getAosLink } from "../utils/aosResolver";
import { useNavigate } from "react-router";

interface PathChipProps {
  path: string;
  asAbsolute?: boolean;
}

export function PathChip({ path, asAbsolute = false }: PathChipProps) {
  const navigate = useNavigate();
  const { workspace } = useWorkspace();
  
  // Resolve canonical path if it's an ID
  const resolvedPath = resolveAosPath(path);
  const displayPath = resolvedPath || path;

  const getAbsolutePath = (relativePath: string) => {
    const root = workspace.repoRoot.replace(/\/$/, "");
    const rel = relativePath.replace(/^\//, "");
    return `${root}/${rel}`;
  };

  const copyPath = async (useAbsolute: boolean) => {
    const pathToCopy = useAbsolute
      ? getAbsolutePath(displayPath)
      : displayPath;
    
    const success = await copyToClipboard(pathToCopy);
    if (success) {
      toast.success(`Copied ${useAbsolute ? "absolute" : "relative"} path`);
    } else {
      toast.error("Failed to copy to clipboard");
    }
  };

  const openInIDE = (e: React.MouseEvent) => {
    e.stopPropagation();
    toast.info("Would open in VS Code (demo mode)");
    // In real implementation: vscode://file/absolute/path
  };

  const handleNavigate = () => {
    if (resolvedPath) {
        navigate(getAosLink(workspace.projectName, resolvedPath));
    }
  };

  return (
    <div 
        className="flex items-center gap-2 bg-muted/50 border border-border rounded px-2 py-1.5 text-xs group hover:bg-muted transition-colors w-full max-w-full cursor-pointer"
        onClick={handleNavigate}
    >
      <FileText className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
      <code className="text-foreground font-mono flex-1 min-w-0 truncate" title={displayPath}>
        {displayPath}
      </code>
      <div className="flex items-center gap-0.5 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
        <Button
          variant="ghost"
          size="sm"
          onClick={(e) => { e.stopPropagation(); copyPath(false); }}
          className="h-5 w-5 p-0 hover:bg-background"
          title="Copy relative path"
        >
          <Copy className="h-3 w-3" />
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={(e) => { e.stopPropagation(); copyPath(true); }}
          className="h-5 w-5 p-0 hover:bg-background"
          title="Copy absolute path"
        >
          <Copy className="h-3 w-3 text-muted-foreground" />
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={openInIDE}
          className="h-5 w-5 p-0 hover:bg-background"
          title="Open in IDE"
        >
          <ExternalLink className="h-3 w-3" />
        </Button>
      </div>
    </div>
  );
}
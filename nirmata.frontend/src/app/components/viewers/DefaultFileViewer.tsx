import { useState } from "react";
import { type FileSystemNode } from "../../hooks/useAosData";
import { ScrollArea } from "../ui/scroll-area";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import {
  File,
  FileJson,
  FileCode,
  FileText,
  FileImage,
  Copy,
  Download,
  Maximize2,
  Minimize2,
  Hash,
  Clock,
  HardDrive,
  Terminal,
} from "lucide-react";
import { cn } from "../ui/utils";

interface DefaultFileViewerProps {
  node: FileSystemNode;
  path: string;
  content?: string;
}

function getFileIcon(name: string) {
  if (name.endsWith(".json")) return <FileJson className="h-4 w-4 text-yellow-500" />;
  if (name.match(/\.(ts|tsx|js|jsx|css|html)$/)) return <FileCode className="h-4 w-4 text-blue-500" />;
  if (name.match(/\.(png|jpg|jpeg|gif|svg|webp)$/)) return <FileImage className="h-4 w-4 text-purple-500" />;
  if (name.match(/\.(md|txt|log|ndjson|yml|yaml)$/)) return <FileText className="h-4 w-4 text-muted-foreground" />;
  return <File className="h-4 w-4 text-muted-foreground" />;
}

function getFileExtension(name: string): string {
  const parts = name.split(".");
  return parts.length > 1 ? `.${parts[parts.length - 1]}` : "unknown";
}

function getLanguageLabel(name: string): string {
  const ext = getFileExtension(name);
  const map: Record<string, string> = {
    ".json": "JSON",
    ".ts": "TypeScript",
    ".tsx": "TSX",
    ".js": "JavaScript",
    ".jsx": "JSX",
    ".css": "CSS",
    ".html": "HTML",
    ".md": "Markdown",
    ".txt": "Plain Text",
    ".log": "Log",
    ".ndjson": "NDJSON",
    ".yml": "YAML",
    ".yaml": "YAML",
    ".schema.json": "JSON Schema",
  };
  // Check compound extensions first
  if (name.endsWith(".schema.json")) return "JSON Schema";
  return map[ext] || "File";
}

export function DefaultFileViewer({ node, path, content }: DefaultFileViewerProps) {
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [copied, setCopied] = useState(false);

  const extension = getFileExtension(node.name);
  const language = getLanguageLabel(node.name);

  const handleCopyPath = () => {
    navigator.clipboard.writeText(path);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div
      className={cn(
        "flex flex-col bg-background transition-all duration-300",
        isFullscreen ? "fixed inset-0 z-50" : "h-full"
      )}
    >
      {/* Toolbar */}
      <div className="flex items-center justify-between px-4 py-2 border-b border-border bg-muted/20 shrink-0">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <div className="bg-muted/60 p-1.5 rounded-md">
              {getFileIcon(node.name)}
            </div>
            <div className="flex flex-col">
              <span className="text-sm font-medium">{node.name}</span>
              <span className="text-[10px] text-muted-foreground font-mono truncate max-w-[300px]">
                {path}
              </span>
            </div>
          </div>

          <div className="h-4 w-px bg-border mx-1" />

          <Badge
            variant="outline"
            className="h-5 text-[10px] gap-1 text-muted-foreground bg-muted/10 border-border"
          >
            {language}
          </Badge>

          {node.status && (
            <Badge
              variant="outline"
              className={cn(
                "h-5 text-[10px] gap-1",
                node.status === "valid" && "text-green-500 border-green-500/20 bg-green-500/5",
                node.status === "warning" && "text-yellow-500 border-yellow-500/20 bg-yellow-500/5",
                node.status === "error" && "text-red-500 border-red-500/20 bg-red-500/5"
              )}
            >
              {node.status}
            </Badge>
          )}
        </div>

        <div className="flex items-center gap-1.5">
          <Button
            variant="ghost"
            size="sm"
            className="h-7 px-2 text-xs gap-1.5"
            onClick={handleCopyPath}
          >
            <Copy className="h-3.5 w-3.5" />
            {copied ? "Copied" : "Path"}
          </Button>

          <Button
            variant="ghost"
            size="sm"
            className="h-7 px-2 text-xs gap-1.5"
          >
            <Download className="h-3.5 w-3.5" />
            Export
          </Button>

          <div className="w-px h-4 bg-border mx-1" />

          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7"
            onClick={() => setIsFullscreen(!isFullscreen)}
          >
            {isFullscreen ? (
              <Minimize2 className="h-3.5 w-3.5" />
            ) : (
              <Maximize2 className="h-3.5 w-3.5" />
            )}
          </Button>
        </div>
      </div>

      {/* Main Content Area */}
      <div className="flex-1 overflow-hidden flex flex-col">
        {/* File metadata bar */}
        <div className="px-4 py-3 border-b border-border/50 bg-card/50 flex items-center gap-6 shrink-0">
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <Hash className="h-3 w-3" />
            <span className="font-mono">{extension}</span>
          </div>
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <HardDrive className="h-3 w-3" />
            <span className="font-mono">{node.sizeBytes ?? "—"}</span>
          </div>
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <Clock className="h-3 w-3" />
            <span className="font-mono">Just now</span>
          </div>
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <Terminal className="h-3 w-3" />
            <span className="font-mono">UTF-8</span>
          </div>
        </div>

        {/* Empty content area — line-numbered gutter with blank content */}
        <ScrollArea className="flex-1">
          <div className="flex min-h-full">
            {/* Line number gutter */}
            <div className="w-12 shrink-0 bg-muted/10 border-r border-border/30 select-none">
              <div className="py-4 px-2 flex flex-col items-end">
                {Array.from({ length: 32 }, (_, i) => (
                  <span
                    key={i}
                    className="text-[11px] font-mono text-muted-foreground/30 h-5 flex items-center"
                  >
                    {i + 1}
                  </span>
                ))}
              </div>
            </div>

            {/* Content area — intentionally blank */}
            <div className="flex-1 p-4">
              <div className="font-mono text-xs leading-5 text-muted-foreground/50 whitespace-pre">
                {content || ""}
              </div>
            </div>
          </div>
        </ScrollArea>
      </div>

      {/* Status bar */}
      <div className="h-7 border-t border-border bg-muted/20 px-4 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-4 text-[10px] text-muted-foreground font-mono">
          <span>Ln 1, Col 1</span>
          <span>{language}</span>
          <span>UTF-8</span>
        </div>
        <div className="flex items-center gap-4 text-[10px] text-muted-foreground font-mono">
          <span>{node.sizeBytes ?? "0 B"}</span>
          <span>Read Only</span>
        </div>
      </div>
    </div>
  );
}
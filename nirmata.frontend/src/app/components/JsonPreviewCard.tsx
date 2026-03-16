import { FileJson, ExternalLink, type LucideIcon } from "lucide-react";
import { cn } from "./ui/utils";

interface JsonPreviewCardProps {
  /** Card header label (e.g. "Run Evidence") */
  label: string;
  /** Subtitle shown in mono below the label (e.g. "run.json") */
  filename: string;
  /** Optional icon — defaults to FileJson */
  icon?: LucideIcon;
  /** Data to stringify into the preview box */
  data: unknown;
  /** Called when the card is clicked */
  onClick: () => void;
  /** Optional extra class on the outer wrapper (e.g. for status-tinted borders) */
  className?: string;
  /** Max height for the scrollable JSON box — defaults to "max-h-40" */
  maxHeight?: string;
}

export function JsonPreviewCard({
  label,
  filename,
  icon: Icon = FileJson,
  data,
  onClick,
  className,
  maxHeight = "max-h-40",
}: JsonPreviewCardProps) {
  return (
    <div
      className={cn(
        "w-full rounded-md overflow-hidden bg-background border border-border/60",
        "cursor-pointer transition-all hover:border-primary/40 hover:bg-accent/30 hover:shadow-sm group/jsoncard",
        className
      )}
      onClick={onClick}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-3 py-2 bg-card border-b border-border/40 group-hover/jsoncard:bg-accent/40 transition-colors">
        <div className="flex items-center gap-2.5">
          <div className="h-6 w-6 rounded-md bg-muted/20 border border-border/50 flex items-center justify-center shrink-0">
            <Icon className="h-3.5 w-3.5 text-muted-foreground" />
          </div>
          <div className="flex flex-col gap-0.5">
            <span className="text-xs font-medium text-foreground">{label}</span>
            <span className="text-[10px] text-muted-foreground font-mono">{filename}</span>
          </div>
        </div>
        <ExternalLink className="h-3.5 w-3.5 text-muted-foreground/40 group-hover/jsoncard:text-primary/60 transition-colors shrink-0" />
      </div>

      {/* JSON preview */}
      <div className="m-3 rounded-md border border-border/40 overflow-hidden">
        <div className={cn("bg-zinc-950 px-4 py-3 font-mono text-xs overflow-auto", maxHeight)}>
          <pre className="text-zinc-300 whitespace-pre-wrap leading-relaxed">
            {JSON.stringify(data, null, 2)}
          </pre>
        </div>
      </div>
    </div>
  );
}

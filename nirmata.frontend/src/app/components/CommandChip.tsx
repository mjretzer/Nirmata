import { Copy, Terminal } from "lucide-react";
import { toast } from "sonner";
import { copyToClipboard } from "../utils/clipboard";
import { Button } from "./ui/button";

interface CommandChipProps {
  command: string;
  description?: string;
  onExecute?: () => void;
}

export function CommandChip({ command, description, onExecute }: CommandChipProps) {
  const copyCommand = async () => {
    const success = await copyToClipboard(command);
    if (success) {
      toast.success("Command copied to clipboard");
    } else {
      toast.error("Failed to copy to clipboard");
    }
  };

  return (
    <div className="flex flex-col gap-1 w-full group">
      {description && (
        <span className="text-xs text-muted-foreground ml-1">{description}</span>
      )}
      <div className="flex items-center gap-2 bg-muted border border-border rounded px-3 py-2 w-full transition-colors hover:border-primary/50">
        <Terminal className="h-4 w-4 text-muted-foreground shrink-0" />
        <div className="flex-1 min-w-0">
          <code className="text-sm font-mono text-foreground truncate block" title={command}>
            {command}
          </code>
        </div>
        <div className="flex items-center gap-1 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
          <Button
            variant="ghost"
            size="sm"
            onClick={copyCommand}
            className="h-7 w-7 p-0 hover:bg-background"
            title="Copy command"
          >
            <Copy className="h-3.5 w-3.5" />
          </Button>
          {onExecute && (
            <Button
              variant="outline"
              size="sm"
              onClick={onExecute}
              className="h-7 px-2 text-xs bg-background hover:bg-accent"
            >
              Run
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

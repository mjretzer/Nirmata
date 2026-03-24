import { useState } from "react";
import { useNavigate } from "react-router";
import { FolderOpen, ChevronDown, ChevronUp, HelpCircle } from "lucide-react";
import { Button } from "./ui/button";

interface NoWorkspaceDetectedProps {
  /** Page-specific description of what this page requires a workspace for. */
  description: string;
  /** Optional help text shown in the collapsible "Why do I need a workspace?" section. */
  helpText?: string;
}

export function NoWorkspaceDetected({ description, helpText }: NoWorkspaceDetectedProps) {
  const navigate = useNavigate();
  const [helpOpen, setHelpOpen] = useState(false);

  return (
    <div className="flex h-full items-center justify-center px-4">
      <div
        className="w-full max-w-sm border-2 border-dashed border-border/60 rounded-xl p-8 space-y-5 text-center"
        style={{
          background:
            "radial-gradient(ellipse at center, hsl(var(--muted)/0.3) 0%, transparent 70%)",
        }}
      >
        {/* Icon */}
        <div className="mx-auto w-16 h-16 relative">
          <div className="absolute inset-0 rounded-2xl bg-muted/40" />
          <div className="relative h-full flex items-center justify-center">
            <FolderOpen className="h-8 w-8 text-muted-foreground/60" aria-hidden="true" />
          </div>
        </div>

        {/* Text */}
        <div className="space-y-1.5">
          <h2 className="text-base font-medium">No workspace detected</h2>
          <p className="text-sm text-muted-foreground">{description}</p>
        </div>

        {/* Primary CTA */}
        <Button
          className="w-full gap-2"
          onClick={() => navigate("/")}
          aria-label="Go to workspace launcher"
        >
          <FolderOpen className="h-4 w-4" aria-hidden="true" />
          Go to Workspace Launcher
        </Button>

        {/* Optional collapsible help */}
        {helpText && (
          <div className="text-left">
            <button
              type="button"
              className="flex items-center gap-1.5 text-xs text-muted-foreground/60 hover:text-muted-foreground transition-colors focus-visible:outline-none w-full justify-center"
              onClick={() => setHelpOpen((o) => !o)}
              aria-expanded={helpOpen}
            >
              <HelpCircle className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
              <span>Why do I need a workspace?</span>
              {helpOpen ? (
                <ChevronUp className="h-3 w-3 shrink-0" aria-hidden="true" />
              ) : (
                <ChevronDown className="h-3 w-3 shrink-0" aria-hidden="true" />
              )}
            </button>
            {helpOpen && (
              <p className="mt-2 text-xs text-muted-foreground/60 text-center leading-relaxed">
                {helpText}
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default NoWorkspaceDetected;

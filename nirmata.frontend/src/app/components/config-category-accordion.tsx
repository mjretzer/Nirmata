/**
 * ConfigCategoryAccordion
 *
 * Shared accordion UI for AOS config entries — used in the full Settings page
 * and the workspace quick-settings panel. Self-contained: exports the component,
 * the handle type callers must satisfy, and the category descriptions.
 */

import {
  Play,
  Database,
  Shield,
  GitCommit,
  RotateCcw,
  AlertCircle,
  Info,
  ChevronUp,
  ChevronDown,
  ChevronRight,
  Lock,
} from "lucide-react";
import { Badge } from "./ui/badge";
import { Input } from "./ui/input";
import { Switch } from "./ui/switch";
import { Label } from "./ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./ui/select";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "./ui/tooltip";
import { cn } from "./ui/utils";
import { type ConfigEntry, type ConfigCategory } from "./workspace-config-panel";

// ── Category descriptions ───────────────────────────────────────────

export const CATEGORY_DESCRIPTIONS: Record<
  Exclude<ConfigCategory, "Tools">,
  string
> = {
  Execution:
    "Controls how the engine sequences work — mode and the atomic step circuit-breaker. The sub-agent flow enforces the small-step model itself; these settings do not override that invariant.",
  Context:
    "Sets limits on how much information AOS reads before starting a task. Lower budgets are faster; higher budgets are more thorough.",
  Verification:
    "Verification is a hard gate: executed work is not done until UAT evidence is recorded as pass or fail. The enforcement setting must remain 'required' for production workspaces.",
  Git: "The Atomic Git Committer enforces one task = one commit immediately after verification passes. The commit policy is fixed to per_task — per_phase and manual are unsupported.",
};

// ── Config handle — minimum surface callers must satisfy ────────────

export type ConfigAccordionHandle = {
  getByCategory: (cat: ConfigCategory) => ConfigEntry[];
  pendingEdits: Record<string, string>;
  validationErrors: Record<string, string>;
  handleChange: (entry: ConfigEntry, raw: string) => void;
  handleReset: (entry: ConfigEntry) => void;
};

// ── Internal helpers ────────────────────────────────────────────────

function humanizeError(entry: ConfigEntry, raw: string): string {
  if (entry.type === "int") {
    if (raw.includes("integer")) return "Please enter a whole number (e.g. 3)";
    if (raw.startsWith("Min:"))
      return `Value must be at least ${raw.replace("Min:", "").trim()}`;
    if (raw.startsWith("Max:"))
      return `Value cannot exceed ${raw.replace("Max:", "").trim()}`;
  }
  return raw;
}

// ── ConfigRow ───────────────────────────────────────────────────────

function ConfigRow({
  entry,
  config,
}: {
  entry: ConfigEntry;
  config: ConfigAccordionHandle;
}) {
  const currentValue = config.pendingEdits[entry.key] ?? entry.value;
  const isDirty = config.pendingEdits[entry.key] !== undefined;
  const isOverride = entry.scope === "workspace" || isDirty;
  const rawError = config.validationErrors[entry.key];
  const error = rawError ? humanizeError(entry, rawError) : undefined;
  const inputId = `setting-${entry.key.replace(/\./g, "-")}`;
  const warnActive =
    entry.warnWhenValue !== undefined && currentValue === entry.warnWhenValue;
  const isLockedEnum =
    entry.type === "enum" && (entry.enumOptions?.length ?? 0) === 1;

  return (
    <div
      className={cn(
        "group rounded-lg border border-l-2 bg-card overflow-hidden transition-all",
        error
          ? "border-red-500/25 border-l-red-500/70"
          : isDirty
          ? "border-yellow-500/20 border-l-yellow-500/60"
          : warnActive
          ? "border-red-500/20 border-l-red-500/50"
          : entry.invariantNote
          ? "border-border border-l-amber-500/30"
          : isOverride
          ? "border-border border-l-cyan-500/50"
          : "border-border border-l-border/40"
      )}
    >
      <div className="flex items-stretch">
        {/* ── Left: meta + description ──────────────────────────── */}
        <div className="flex-1 min-w-0 px-4 pt-3 pb-3 space-y-1.5">
          {/* Row 1: key + scope badge + invariant badge + reset */}
          <div className="flex items-center gap-2 flex-wrap">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <code className="text-[11px] font-mono text-primary/60 cursor-default tracking-tight hover:text-primary/90 transition-colors">
                    {entry.key}
                  </code>
                </TooltipTrigger>
                <TooltipContent className="text-xs font-mono">
                  aos config set {entry.key} &lt;value&gt;
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span
                    className={cn(
                      "text-[9px] font-mono px-1.5 py-0.5 rounded border cursor-default select-none",
                      isOverride
                        ? "text-cyan-400 bg-cyan-500/10 border-cyan-500/25"
                        : "text-muted-foreground/30 border-border/25"
                    )}
                  >
                    {isOverride ? "ws" : "def"}
                  </span>
                </TooltipTrigger>
                <TooltipContent className="text-xs">
                  {isOverride
                    ? "Overridden at workspace scope"
                    : "Using global default value"}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>

            {isLockedEnum && (
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span className="inline-flex items-center gap-1 text-[9px] font-mono px-1.5 py-0.5 rounded border text-amber-400/70 bg-amber-500/[0.06] border-amber-500/20 cursor-default select-none">
                      <Lock className="h-2.5 w-2.5" />
                      invariant
                    </span>
                  </TooltipTrigger>
                  <TooltipContent className="text-xs max-w-[220px]">
                    This value is fixed by the engine. Only one valid option
                    exists.
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            )}

            {isOverride && currentValue !== entry.defaultValue && !isLockedEnum && (
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <button
                      type="button"
                      aria-label={`Reset ${entry.label} to default (${entry.defaultValue})`}
                      onClick={() => config.handleReset(entry)}
                      className="flex items-center gap-1 text-[9px] font-mono text-muted-foreground/25 hover:text-muted-foreground transition-colors opacity-0 group-hover:opacity-100"
                    >
                      <RotateCcw className="h-2.5 w-2.5" />
                      reset
                    </button>
                  </TooltipTrigger>
                  <TooltipContent side="right" className="text-xs">
                    Reset to default: {entry.defaultValue}
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            )}
          </div>

          <Label
            htmlFor={inputId}
            className="text-sm cursor-pointer block leading-tight"
          >
            {entry.label}
          </Label>

          {entry.description && (
            <p className="text-xs text-muted-foreground leading-relaxed max-w-sm">
              {entry.description}
            </p>
          )}

          {isOverride && currentValue !== entry.defaultValue && (
            <p className="text-[10px] text-muted-foreground/25 font-mono">
              default: {entry.defaultValue}
            </p>
          )}

          {error && (
            <div
              role="alert"
              className="flex items-start gap-1.5 mt-1 px-2.5 py-1.5 rounded bg-red-500/8 border border-red-500/20"
            >
              <AlertCircle className="h-3 w-3 text-red-400 mt-0.5 shrink-0" />
              <p className="text-[11px] text-red-400 leading-snug">{error}</p>
            </div>
          )}

          {/* Invariant note — amber, always visible when set */}
          {entry.invariantNote && (
            <div className="flex items-start gap-1.5 mt-1 px-2.5 py-1.5 rounded bg-amber-500/[0.06] border border-amber-500/20">
              <Info className="h-3 w-3 text-amber-400/70 mt-0.5 shrink-0" />
              <p className="text-[11px] text-amber-400/80 leading-snug">
                {entry.invariantNote}
              </p>
            </div>
          )}

          {/* Warn-when-value — red, only when the dangerous value is active */}
          {warnActive && (
            <div className="flex items-start gap-1.5 mt-1 px-2.5 py-1.5 rounded bg-red-500/8 border border-red-500/25">
              <AlertCircle className="h-3 w-3 text-red-400 mt-0.5 shrink-0" />
              <p className="text-[11px] text-red-400 leading-snug">
                Dev override active — verification is bypassed. This is not
                supported for production workspaces and will not be accepted in
                CI.
              </p>
            </div>
          )}
        </div>

        {/* ── Right: control column ─────────────────────────────── */}
        <div
          className={cn(
            "flex items-center justify-center px-4 border-l min-w-[130px] shrink-0",
            isDirty && !error
              ? "border-yellow-500/15 bg-yellow-500/[0.03]"
              : error
              ? "border-red-500/15 bg-red-500/[0.03]"
              : warnActive
              ? "border-red-500/15 bg-red-500/[0.03]"
              : isOverride
              ? "border-cyan-500/10 bg-cyan-500/[0.02]"
              : "border-border/40 bg-muted/[0.03]"
          )}
        >
          {entry.type === "bool" && (
            <Switch
              id={inputId}
              aria-label={entry.label}
              checked={currentValue === "true"}
              onCheckedChange={(checked) =>
                config.handleChange(entry, checked ? "true" : "false")
              }
            />
          )}

          {entry.type === "enum" && entry.enumOptions && !isLockedEnum && (
            <Select
              value={currentValue}
              onValueChange={(val) => config.handleChange(entry, val)}
            >
              <SelectTrigger
                id={inputId}
                aria-label={entry.label}
                className={cn(
                  "w-32 text-xs font-mono h-8",
                  warnActive && "border-red-500/40 text-red-400"
                )}
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {entry.enumOptions.map((opt) => (
                  <SelectItem key={opt} value={opt} className="text-xs font-mono">
                    {opt}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}

          {/* Locked single-option enum — readonly invariant pill */}
          {isLockedEnum && (
            <div className="flex items-center gap-1.5">
              <span className="text-xs font-mono text-amber-400/80 bg-amber-500/8 border border-amber-500/25 px-2.5 py-1 rounded-md">
                {currentValue}
              </span>
              <Lock
                className="h-3.5 w-3.5 text-amber-400/40 shrink-0"
                aria-label="fixed invariant"
              />
            </div>
          )}

          {entry.type === "int" && (
            <div className="flex items-center gap-1">
              <Input
                id={inputId}
                aria-label={entry.label}
                aria-invalid={!!error}
                aria-describedby={error ? `${inputId}-error` : undefined}
                type="text"
                inputMode="numeric"
                value={currentValue}
                onChange={(e) => config.handleChange(entry, e.target.value)}
                className={cn(
                  "h-8 w-20 text-xs font-mono text-center",
                  error && "border-red-500/50 focus-visible:ring-red-500/30"
                )}
              />
              <div className="flex flex-col">
                <button
                  type="button"
                  aria-label="Increase value"
                  onClick={() => {
                    const n = Number(currentValue);
                    if (!isNaN(n)) config.handleChange(entry, String(n + 1));
                  }}
                  className="h-4 px-0.5 text-muted-foreground/40 hover:text-primary transition-colors"
                >
                  <ChevronUp className="h-3 w-3" />
                </button>
                <button
                  type="button"
                  aria-label="Decrease value"
                  onClick={() => {
                    const n = Number(currentValue);
                    if (!isNaN(n)) config.handleChange(entry, String(n - 1));
                  }}
                  className="h-4 px-0.5 text-muted-foreground/40 hover:text-primary transition-colors"
                >
                  <ChevronDown className="h-3 w-3" />
                </button>
              </div>
            </div>
          )}

          {entry.type === "string" && (
            <Input
              id={inputId}
              aria-label={entry.label}
              type="text"
              value={currentValue}
              onChange={(e) => config.handleChange(entry, e.target.value)}
              className="h-8 w-36 text-xs font-mono"
            />
          )}
        </div>
      </div>
    </div>
  );
}

// ── ConfigCategoryAccordion ─────────────────────────────────────────

const CATEGORIES = ["Execution", "Context", "Verification", "Git"] as const;
type AccordionCategory = (typeof CATEGORIES)[number];

const ICON_MAP: Record<AccordionCategory, React.ElementType> = {
  Execution: Play,
  Context: Database,
  Verification: Shield,
  Git: GitCommit,
};

export interface ConfigCategoryAccordionProps {
  config: ConfigAccordionHandle;
  expanded: Record<string, boolean>;
  toggleCategory: (cat: string) => void;
}

export function ConfigCategoryAccordion({
  config,
  expanded,
  toggleCategory,
}: ConfigCategoryAccordionProps) {
  const dirtyInCategory = (cat: AccordionCategory) =>
    config
      .getByCategory(cat)
      .filter((e) => config.pendingEdits[e.key] !== undefined).length;

  return (
    <div className="space-y-3">
      {CATEGORIES.map((cat) => {
        const entries = config
          .getByCategory(cat)
          .filter(
            (e) =>
              e.key !== "execution.mode" && !e.key.startsWith("tools.")
          );
        if (entries.length === 0) return null;

        const Icon = ICON_MAP[cat];
        const isOpen = expanded[cat];
        const dirty = dirtyInCategory(cat);

        return (
          <div
            key={cat}
            className="border border-border rounded-lg overflow-hidden"
          >
            <button
              type="button"
              aria-expanded={isOpen}
              aria-controls={`category-${cat}`}
              onClick={() => toggleCategory(cat)}
              className="w-full flex items-center justify-between px-4 py-3 bg-muted/10 hover:bg-muted/20 transition-colors text-left"
            >
              <div className="flex items-center gap-3">
                <Icon className="h-4 w-4 text-primary" />
                <div>
                  <span className="text-sm">{cat}</span>
                  <span className="ml-2 text-[10px] text-muted-foreground/50">
                    {entries.length} setting{entries.length !== 1 ? "s" : ""}
                  </span>
                </div>
                {dirty > 0 && (
                  <Badge
                    variant="outline"
                    className="text-[9px] h-4 px-1 border-yellow-500/30 text-yellow-400 bg-yellow-500/10"
                  >
                    {dirty} changed
                  </Badge>
                )}
              </div>
              <ChevronRight
                className={cn(
                  "h-4 w-4 text-muted-foreground/40 transition-transform duration-200",
                  isOpen && "rotate-90"
                )}
              />
            </button>

            {isOpen && (
              <div id={`category-${cat}`} className="px-4 py-4 space-y-2">
                {CATEGORY_DESCRIPTIONS[cat] && (
                  <p className="text-xs text-muted-foreground/60 mb-3 flex items-start gap-1.5">
                    <Info className="h-3 w-3 mt-0.5 shrink-0" />
                    {CATEGORY_DESCRIPTIONS[cat]}
                  </p>
                )}
                <div className="space-y-2">
                  {entries.map((entry) => (
                    <ConfigRow key={entry.key} entry={entry} config={config} />
                  ))}
                </div>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

import { useState, useCallback, useMemo } from "react";
import { useNavigate } from "react-router";
import {
  Settings,
  ChevronDown,
  RotateCcw,
  Search,
  Info,
  FileCode,
  Save,
  Zap,
} from "lucide-react";
import { Badge } from "./ui/badge";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "./ui/tooltip";
import { cn } from "./ui/utils";
import { toast } from "sonner";
import {
  ConfigCategoryAccordion,
  type ConfigAccordionHandle,
} from "./config-category-accordion";

// ── Config data model ──────────────────────────────────────────────
// Maps to `aos config show` / `aos config set <key> <value>`

export type ConfigCategory =
  | "Execution"
  | "Context"
  | "Verification"
  | "Git"
  | "Tools";
export type ConfigScope = "default" | "workspace";
export type ConfigValueType = "enum" | "bool" | "int" | "string";

export interface ConfigEntry {
  key: string;
  category: ConfigCategory;
  value: string;
  defaultValue: string;
  scope: ConfigScope;
  type: ConfigValueType;
  label: string;
  description: string;
  modifiedAt: string | null;
  enumOptions?: string[];
  intMin?: number;
  intMax?: number;
  /** Amber callout shown on every render — use to surface engine invariants. */
  invariantNote?: string;
  /** Red callout shown only when the current value equals this string. */
  warnWhenValue?: string;
}

// Full config registry — loaded by the Orchestrator on every routing cycle
export const configRegistry: Omit<
  ConfigEntry,
  "value" | "defaultValue" | "scope" | "modifiedAt"
>[] = [
  {
    key: "execution.mode",
    category: "Execution",
    label: "Mode",
    description: "How the engine sequences work in this repo",
    type: "enum",
    enumOptions: ["atomic", "manual"],
  },
  {
    key: "execution.auto_fix_plan",
    category: "Execution",
    label: "Auto-create fix plan on fail",
    description: "Automatically generate a fix plan when a task fails verification or encounters an error.",
    type: "bool",
  },
  {
    key: "execution.atomic_step_budget",
    category: "Execution",
    label: "Atomic step budget",
    description:
      "Circuit-breaker: maximum sub-steps the executor may take before forcing a checkpoint. The engine enforces one-task = one-atomic-step itself — this is not a workflow tuning knob.",
    type: "int",
    intMin: 1,
    intMax: 5,
    invariantNote:
      "Keep this at 3 or below. Values above 5 are rejected. The sub-agent flow is designed for small atomic steps — high budgets indicate a scope problem, not a config problem.",
  },
  {
    key: "context.default_budget",
    category: "Context",
    label: "Default budget",
    description: "Token budget for context assembly",
    type: "int",
    intMin: 1000,
    intMax: 1000000,
  },
  {
    key: "context.include_codebase",
    category: "Context",
    label: "Include codebase",
    description: "Auto-add .aos/codebase/** to context when relevant",
    type: "bool",
  },
  {
    key: "verify.enforcement",
    category: "Verification",
    label: "Verification enforcement",
    description:
      "Executed work is not done until UAT evidence is recorded as pass or fail. 'dev_override_unsafe' skips verification — not supported in production workflows.",
    type: "enum",
    enumOptions: ["required", "dev_override_unsafe"],
    warnWhenValue: "dev_override_unsafe",
  },
  {
    key: "verify.always_verify_after_task",
    category: "Verification",
    label: "Verify after task vs. phase gate",
    description: "When enabled, forces verification immediately after each task. When disabled, postpones verification until the phase gate.",
    type: "bool",
  },
  {
    key: "verify.default_target",
    category: "Verification",
    label: "Default target",
    description: "Scope of the default verification check",
    type: "enum",
    enumOptions: ["task", "phase", "plan"],
  },
  {
    key: "git.commit_policy",
    category: "Git",
    label: "Commit policy",
    description:
      "Engine invariant: one task = one commit, applied immediately after verification passes.",
    type: "enum",
    enumOptions: ["per_task"],
    invariantNote:
      "Fixed to per_task. The Atomic Git Committer enforces this invariant — per_phase and manual modes conflict with the documented execution model and are not supported.",
  },
  {
    key: "git.require_clean_tree",
    category: "Git",
    label: "Require clean tree",
    description: "Block execution if working tree is dirty",
    type: "bool",
  },
  {
    key: "tools.llm_provider",
    category: "Tools",
    label: "LLM provider",
    description: "Which model provider the engine calls",
    type: "string",
  },
  {
    key: "tools.llm_model",
    category: "Tools",
    label: "LLM model",
    description: "Model identifier passed to the active provider on every run",
    type: "string",
  },
  {
    key: "tools.mcp_endpoints",
    category: "Tools",
    label: "MCP endpoints",
    description: "External tool endpoints exposed to this workspace",
    type: "string",
  },
];

export const configDefaults: Record<string, string> = {
  "execution.mode": "atomic",
  "execution.auto_fix_plan": "true",
  "execution.atomic_step_budget": "3",
  "context.default_budget": "128000",
  "context.include_codebase": "true",
  "verify.enforcement": "required",
  "verify.always_verify_after_task": "true",
  "verify.default_target": "task",
  "git.commit_policy": "per_task",
  "git.require_clean_tree": "true",
  "tools.llm_provider": "anthropic",
  "tools.llm_model": "claude-3-7-sonnet",
  "tools.mcp_endpoints": '[{"id":"github","name":"GitHub MCP","url":"https://api.github.com/mcp","authType":"bearer","tokenConfigured":true,"tokenHint":"...ghp_9f3x","enabled":true,"status":"connected","lastChecked":"1m ago"}]',
};

// Per-workspace overrides (simulates .aos/config.json)
export const workspaceOverrides: Record<
  string,
  Record<string, { value: string; modifiedAt: string }>
> = {
  "acme-frontend": {
    "execution.mode": {
      value: "atomic",
      modifiedAt: "2026-03-04T18:22:00Z",
    },
    "execution.atomic_step_budget": {
      value: "3",
      modifiedAt: "2026-03-03T09:15:00Z",
    },
    "verify.default_target": {
      value: "phase",
      modifiedAt: "2026-03-01T14:00:00Z",
    },
    "git.commit_policy": {
      value: "per_task",
      modifiedAt: "2026-02-28T11:30:00Z",
    },
    "tools.llm_provider": {
      value: "anthropic",
      modifiedAt: "2026-03-02T20:45:00Z",
    },
    "tools.llm_model": {
      value: "claude-3-7-sonnet",
      modifiedAt: "2026-03-02T20:45:00Z",
    },
  },
  "new-project": {},
};

export function resolveConfig(wsName: string): ConfigEntry[] {
  const overrides = workspaceOverrides[wsName] ?? {};
  return configRegistry.map((reg) => {
    const defVal = configDefaults[reg.key] ?? "";
    const ov = overrides[reg.key];
    return {
      ...reg,
      defaultValue: defVal,
      value: ov?.value ?? defVal,
      scope: ov ? ("workspace" as ConfigScope) : ("default" as ConfigScope),
      modifiedAt: ov?.modifiedAt ?? null,
    };
  });
}

// ── Styling ────────────────────────────────────────────────────────

// (categoryColors retained for the mode badge in the collapsed header)
const categoryColors: Record<ConfigCategory, string> = {
  Execution: "text-blue-400 bg-blue-500/10 border-blue-500/20",
  Context: "text-purple-400 bg-purple-500/10 border-purple-500/20",
  Verification: "text-cyan-400 bg-cyan-500/10 border-cyan-500/20",
  Git: "text-orange-400 bg-orange-500/10 border-orange-500/20",
  Tools: "text-green-400 bg-green-500/10 border-green-500/20",
};

// ── Component ──────────────────────────────────────────────────────

interface WorkspaceConfigPanelProps {
  wsName: string;
  isOpen: boolean;
  onToggle: () => void;
  /** Skip the outer collapsible header and render the accordion inline. */
  inline?: boolean;
}

export function WorkspaceConfigPanel({
  wsName,
  isOpen,
  onToggle,
  inline = false,
}: WorkspaceConfigPanelProps) {
  const navigate = useNavigate();

  // Resolved config from defaults + workspace overrides
  const baseConfig = useMemo(() => resolveConfig(wsName), [wsName]);

  // Pending edits (dirty state) — key → new value
  const [pendingEdits, setPendingEdits] = useState<Record<string, string>>({});
  const [searchQuery, setSearchQuery] = useState("");
  const [validationErrors, setValidationErrors] = useState<
    Record<string, string>
  >({});

  // Accordion expanded state — Execution open by default
  const [expanded, setExpanded] = useState<Record<string, boolean>>({
    Execution: true,
    Context: false,
    Verification: false,
    Git: false,
  });

  const toggleCategory = useCallback((cat: string) => {
    setExpanded((prev) => ({ ...prev, [cat]: !prev[cat] }));
  }, []);

  // Effective config with pending edits applied
  const effectiveConfig = useMemo(() => {
    return baseConfig.map((entry) => {
      if (pendingEdits[entry.key] !== undefined) {
        return { ...entry, value: pendingEdits[entry.key] };
      }
      return entry;
    });
  }, [baseConfig, pendingEdits]);

  // Counts
  const overrideCount = baseConfig.filter(
    (c) => c.scope === "workspace"
  ).length;
  const dirtyCount = Object.keys(pendingEdits).length;
  const executionMode =
    pendingEdits["execution.mode"] ??
    baseConfig.find((c) => c.key === "execution.mode")?.value ??
    "atomic";

  // ── Handlers ──

  const validate = useCallback(
    (entry: ConfigEntry, val: string): string | null => {
      if (entry.type === "int") {
        const n = Number(val);
        if (isNaN(n) || !Number.isInteger(n))
          return "Must be an integer";
        if (entry.intMin !== undefined && n < entry.intMin)
          return `Min: ${entry.intMin}`;
        if (entry.intMax !== undefined && n > entry.intMax)
          return `Max: ${entry.intMax}`;
      }
      return null;
    },
    []
  );

  const handleChange = useCallback(
    (entry: ConfigEntry, newValue: string) => {
      const err = validate(entry, newValue);
      setValidationErrors((prev) => {
        const next = { ...prev };
        if (err) next[entry.key] = err;
        else delete next[entry.key];
        return next;
      });

      // If value matches original resolved value, remove from pending
      const original = baseConfig.find((c) => c.key === entry.key);
      if (original && newValue === original.value) {
        setPendingEdits((prev) => {
          const next = { ...prev };
          delete next[entry.key];
          return next;
        });
      } else {
        setPendingEdits((prev) => ({ ...prev, [entry.key]: newValue }));
      }
    },
    [baseConfig, validate]
  );

  const handleReset = useCallback(
    (entry: ConfigEntry) => {
      if (entry.value !== entry.defaultValue) {
        setPendingEdits((prev) => ({
          ...prev,
          [entry.key]: entry.defaultValue,
        }));
      } else {
        setPendingEdits((prev) => {
          const next = { ...prev };
          delete next[entry.key];
          return next;
        });
      }
      setValidationErrors((prev) => {
        const next = { ...prev };
        delete next[entry.key];
        return next;
      });
    },
    []
  );

  const handleResetAll = useCallback(() => {
    const edits: Record<string, string> = {};
    for (const entry of baseConfig) {
      if (entry.scope === "workspace") {
        edits[entry.key] = entry.defaultValue;
      }
    }
    setPendingEdits(edits);
    setValidationErrors({});
  }, [baseConfig]);

  const handleApply = useCallback(() => {
    if (Object.keys(validationErrors).length > 0) {
      toast.error("Fix validation errors before applying");
      return;
    }
    const keys = Object.keys(pendingEdits);
    for (const key of keys) {
      toast.success(`aos config set ${key} ${pendingEdits[key]}`);
    }
    setPendingEdits({});
  }, [pendingEdits, validationErrors]);

  const handleDiscard = useCallback(() => {
    setPendingEdits({});
    setValidationErrors({});
  }, []);

  const hasErrors = Object.keys(validationErrors).length > 0;

  // ── Mode segmented control ──
  const modeEntry = effectiveConfig.find((c) => c.key === "execution.mode");
  const modeOptions = modeEntry?.enumOptions ?? ["atomic", "manual"];

  // ── Accordion handle ──
  // When a search is active, entries are filtered per-category; all categories
  // are also expanded automatically so results aren't hidden behind closed headers.
  const q = searchQuery.toLowerCase().trim();

  const accordionHandle: ConfigAccordionHandle = useMemo(
    () => ({
      getByCategory: (cat) => {
        let entries = effectiveConfig.filter((e) => e.category === cat);
        if (q) {
          entries = entries.filter(
            (e) =>
              e.key.toLowerCase().includes(q) ||
              e.label.toLowerCase().includes(q) ||
              e.description.toLowerCase().includes(q)
          );
        }
        return entries;
      },
      pendingEdits,
      validationErrors,
      handleChange,
      handleReset,
    }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [effectiveConfig, pendingEdits, validationErrors, handleChange, handleReset, q]
  );

  // Auto-expand all categories when searching
  const effectiveExpanded = q
    ? { Execution: true, Context: true, Verification: true, Git: true }
    : expanded;

  // ── Inline (no outer wrapper) render ────────────────────────────
  if (inline) {
    const searchResults = searchQuery.trim()
      ? effectiveConfig.filter(
          (e) =>
            e.key.toLowerCase().includes(searchQuery.toLowerCase()) ||
            e.label.toLowerCase().includes(searchQuery.toLowerCase()) ||
            e.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
            e.category.toLowerCase().includes(searchQuery.toLowerCase())
        )
      : [];

    const categories: ConfigCategory[] = [
      "Execution",
      "Context",
      "Verification",
      "Git",
      "Tools",
    ];

    return (
      null
    );
  }

  return (
    <div className="rounded-md border border-border/60 bg-card/30 overflow-hidden">
      {/* ── Collapsed header ────────────────────────── */}
      <button
        type="button"
        onClick={onToggle}
        className="w-full flex items-center justify-between px-3 py-2 text-xs hover:bg-muted/30 transition-colors"
      >
        <span className="flex items-center gap-2 text-muted-foreground">
          <Settings className="h-3.5 w-3.5" />
          <span>Config</span>
          {/* Mode badge */}
          <Badge
            variant="outline"
            className={cn(
              "text-[9px] h-4 px-1.5 border",
              categoryColors["Execution"]
            )}
          >
            {executionMode}
          </Badge>
          {/* Override count */}
          {overrideCount > 0 && (
            <span className="text-[10px] text-muted-foreground/50">
              {overrideCount} override{overrideCount !== 1 ? "s" : ""}
            </span>
          )}
          {/* Dirty indicator */}
          {dirtyCount > 0 && (
            <span className="text-[10px] text-yellow-400">
              {dirtyCount} unsaved
            </span>
          )}
        </span>
        <ChevronDown
          className={cn(
            "h-3.5 w-3.5 text-muted-foreground/50 transition-transform duration-200",
            isOpen && "rotate-180"
          )}
        />
      </button>

      {/* ── Expanded panel ──────────────────────────── */}
      {isOpen && (
        <div className="border-t border-border/40">
          {/* Header: mode segmented + overrides chip */}
          <div className="px-3 pt-3 pb-2 space-y-2.5">
            {/* Mode segmented control */}
            <div className="flex items-center gap-3">
              <div className="flex rounded-md border border-border/60 overflow-hidden">
                {modeOptions.map((opt) => (
                  <button
                    key={opt}
                    type="button"
                    onClick={() => {
                      if (modeEntry) handleChange(modeEntry, opt);
                    }}
                    className={cn(
                      "px-3 py-1 text-[11px] font-mono transition-colors capitalize",
                      executionMode === opt
                        ? "bg-blue-500/20 text-blue-400"
                        : "text-muted-foreground/60 hover:text-muted-foreground hover:bg-muted/20"
                    )}
                  >
                    {opt}
                  </button>
                ))}
              </div>
            </div>

            {/* Effective config subline */}
            <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground/50">
              <span>
                Effective = defaults + {overrideCount} workspace override
                {overrideCount !== 1 ? "s" : ""}
              </span>
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Info className="h-3 w-3 cursor-help" />
                  </TooltipTrigger>
                  <TooltipContent side="top" className="max-w-[240px] text-xs">
                    The engine loads defaults, then merges .aos/config.json
                    overrides at the start of every routing cycle.
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            </div>

            {/* Search */}
            <div className="relative">
              <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3 w-3 text-muted-foreground/40" />
              <Input
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search keys..."
                className="h-7 pl-7 text-[11px] bg-transparent border-border/40"
              />
            </div>
          </div>

          {/* ── Category accordion ───────────────────── */}
          <div className="px-3 pb-3">
            <ConfigCategoryAccordion
              config={accordionHandle}
              expanded={effectiveExpanded}
              toggleCategory={toggleCategory}
            />
          </div>

          {/* ── Dirty state bar ─────────────────────── */}
          {dirtyCount > 0 && (
            <div className="border-t border-yellow-500/20 bg-yellow-500/5 px-3 py-2 flex items-center justify-between">
              <span className="text-[11px] text-yellow-400">
                {dirtyCount} change{dirtyCount !== 1 ? "s" : ""}
              </span>
              <div className="flex items-center gap-2">
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 text-[10px] text-muted-foreground"
                  onClick={handleDiscard}
                >
                  Discard
                </Button>
                <Button
                  size="sm"
                  className={cn(
                    "h-6 text-[10px]",
                    hasErrors && "opacity-50 cursor-not-allowed"
                  )}
                  onClick={handleApply}
                  disabled={hasErrors}
                >
                  Apply
                </Button>
              </div>
            </div>
          )}

          {/* ── Footer ──────────────────────────────── */}
          <div className="border-t border-border/40 px-3 py-1.5 flex items-center justify-between">
            <button
              type="button"
              onClick={() =>
                toast.info("Would open .aos/config.json in file explorer")
              }
              className="flex items-center gap-1.5 text-[11px] text-muted-foreground/50 hover:text-muted-foreground transition-colors"
            >
              <FileCode className="h-3 w-3" />
              View config file
            </button>

            <div className="flex items-center gap-3">
              {overrideCount > 0 && dirtyCount === 0 && (
                <button
                  type="button"
                  onClick={handleResetAll}
                  className="flex items-center gap-1 text-[10px] text-muted-foreground/40 hover:text-red-400 transition-colors"
                >
                  <RotateCcw className="h-2.5 w-2.5" />
                  Reset all overrides
                </button>
              )}
              <button
                type="button"
                onClick={() => navigate(`/ws/${wsName}/settings`)}
                className="text-[11px] text-muted-foreground/50 hover:text-muted-foreground transition-colors"
              >
                View all {effectiveConfig.length} keys &rarr;
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
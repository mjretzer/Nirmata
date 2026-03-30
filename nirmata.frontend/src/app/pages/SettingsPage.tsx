import { useState, useCallback, useMemo, useRef, useEffect } from "react";
import { useParams, useNavigate, useLocation, useOutletContext, Outlet, Link, Navigate } from "react-router";
import type { NavigateFunction } from "react-router";
import {
  Save,
  RotateCcw,
  Settings,
  Server,
  Code,
  Shield,
  Cpu,
  FileJson,
  CheckCircle,
  AlertCircle,
  Play,
  GitCommit,
  GitBranch,
  Globe,
  RefreshCw,
  Search,
  Check,
  ChevronUp,
  ChevronDown,
  ChevronRight,
  Info,
  Eye,
  EyeOff,
  X,
  Zap,
  ExternalLink,
  Clock,
  Lock,
  Key,
  WifiOff,
  Circle,
  Link2,
  FolderOpen,
  Plus,
  Loader2,
} from "lucide-react";
import { Button } from "../components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "../components/ui/dialog";
import { Input } from "../components/ui/input";
import { Switch } from "../components/ui/switch";
import { Label } from "../components/ui/label";
import { Badge } from "../components/ui/badge";
import { Separator } from "../components/ui/separator";
import { ScrollArea } from "../components/ui/scroll-area";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../components/ui/select";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "../components/ui/tooltip";
import { cn } from "../components/ui/utils";
import { toast } from "sonner";
import { useWorkspace, useWorkspaces, useWorkspaceInit, useBootstrapWorkspace, useAosCommand, useEngineConnection, useGitHubWorkspaceBootstrap, isGuidWorkspaceId } from "../hooks/useAosData";
import { useWorkspaceContext } from "../context/WorkspaceContext";
import { domainClient } from "../utils/apiClient";
import {
  type ConfigEntry,
  type ConfigCategory,
  resolveConfig,
} from "../components/workspace-config-panel";
import { ConfigCategoryAccordion } from "../components/config-category-accordion";
import { WorkspaceHealthPanel } from "../components/WorkspaceHealthPanel";
import { DAEMON_BASE_URL } from "../api/routing";

// ── Constants ────────────────────────────────────────────────────────



// ── Analytics hook ────────────────────────────────────────────────────

function useSettingsAnalytics() {
  const [changesByKey, setChangesByKey] = useState<Record<string, number>>({});

  const trackChange = useCallback((key: string) => {
    setChangesByKey((prev) => ({ ...prev, [key]: (prev[key] ?? 0) + 1 }));
  }, []);

  const topChanged = useMemo(
    () =>
      Object.entries(changesByKey)
        .sort(([, a], [, b]) => b - a)
        .slice(0, 5),
    [changesByKey]
  );

  return { trackChange, topChanged };
}

// ── Config state hook ─────────────────────────────────────────────────

function humanizeError(entry: ConfigEntry, raw: string): string {
  if (entry.type === "int") {
    if (raw.includes("integer")) return "Please enter a whole number (e.g. 50)";
    if (raw.startsWith("Min:")) return `Value must be at least ${raw.replace("Min:", "").trim()}`;
    if (raw.startsWith("Max:")) return `Value cannot exceed ${raw.replace("Max:", "").trim()}`;
  }
  return raw;
}

function useConfigState(
  wsName: string,
  trackChange: (key: string) => void
) {
  const command = useAosCommand();
  const baseConfig = useMemo(() => resolveConfig(wsName), [wsName]);
  const [pendingEdits, setPendingEdits] = useState<Record<string, string>>({});
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  const effectiveConfig = useMemo(
    () =>
      baseConfig.map((entry) =>
        pendingEdits[entry.key] !== undefined
          ? { ...entry, value: pendingEdits[entry.key] }
          : entry
      ),
    [baseConfig, pendingEdits]
  );

  const dirtyCount = Object.keys(pendingEdits).length;
  const hasErrors = Object.keys(validationErrors).length > 0;

  const handleChange = useCallback(
    (entry: ConfigEntry, raw: string) => {
      let error = "";
      if (entry.type === "int") {
        const n = Number(raw);
        if (!Number.isInteger(n)) error = "Must be an integer";
        else if (entry.intMin !== undefined && n < entry.intMin) error = `Min: ${entry.intMin}`;
        else if (entry.intMax !== undefined && n > entry.intMax) error = `Max: ${entry.intMax}`;
      }
      if (entry.type === "bool" && raw !== "true" && raw !== "false") {
        error = "Must be true or false";
      }
      setPendingEdits((prev) => ({ ...prev, [entry.key]: raw }));
      setValidationErrors((prev) => {
        if (error) return { ...prev, [entry.key]: error };
        const next = { ...prev };
        delete next[entry.key];
        return next;
      });
      trackChange(entry.key);
    },
    [trackChange]
  );

  const handleReset = useCallback(
    (entry: ConfigEntry) => {
      setPendingEdits((prev) => {
        const next = { ...prev };
        delete next[entry.key];
        return next;
      });
      setValidationErrors((prev) => {
        const next = { ...prev };
        delete next[entry.key];
        return next;
      });
    },
    []
  );

  const handleApply = useCallback(async () => {
    if (Object.keys(validationErrors).length > 0) {
      toast.error("Fix validation errors before applying");
      return;
    }
    const keys = Object.keys(pendingEdits);
    for (const key of keys) {
      const result = await command.execute(["aos", "config", "set", key, pendingEdits[key]]);
      if (!result.ok) {
        toast.error(`Failed to set ${key}`);
        return;
      }
    }
    toast.success(`${keys.length} config value${keys.length !== 1 ? "s" : ""} applied`);
    setPendingEdits({});
  }, [pendingEdits, validationErrors, command]);

  const handleDiscard = useCallback(() => {
    setPendingEdits({});
    setValidationErrors({});
  }, []);

  const getEntry = useCallback(
    (key: string) => effectiveConfig.find((c) => c.key === key),
    [effectiveConfig]
  );

  const getByCategory = useCallback(
    (cat: ConfigCategory) => effectiveConfig.filter((c) => c.category === cat),
    [effectiveConfig]
  );

  return {
    effectiveConfig,
    pendingEdits,
    validationErrors,
    dirtyCount,
    hasErrors,
    handleChange,
    handleReset,
    handleApply,
    handleDiscard,
    getEntry,
    getByCategory,
    isApplying: command.isRunning,
  };
}

// ── Skeleton ──────────────────────────────────────────────────────────

function TabSkeleton() {
  return (
    <div className="space-y-6 animate-pulse">
      <div className="h-6 w-48 bg-muted/40 rounded" />
      <div className="h-3 w-72 bg-muted/30 rounded" />
      <div className="space-y-3 mt-6">
        {[1, 2, 3, 4].map((i) => (
          <div key={i} className="h-14 bg-muted/20 rounded-lg border border-border" />
        ))}
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════
// MODULE-LEVEL SUB-COMPONENTS
// (Defined outside SettingsPage so React never remounts them on re-render)
// ═══════════════════════════════════════════════════════════════════════

// ── Section wrapper ───────────────────────────────────────────────────

function Section({
  title,
  description,
  children,
  icon: Icon,
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
  icon?: React.ElementType;
}) {
  return (
    <div className="space-y-5 mb-10">
      <div className="border-b border-border pb-3">
        <div className="flex items-center gap-2">
          {Icon && <Icon className="h-4 w-4 text-primary shrink-0" />}
          <h2 className="text-base">{title}</h2>
        </div>
        {description && (
          <p className="text-xs text-muted-foreground mt-1 ml-6">{description}</p>
        )}
      </div>
      <div className="space-y-5 px-1">{children}</div>
    </div>
  );
}

// ── SettingRow ────────────────────────────────────────────────────────

function SettingRow({
  label,
  description,
  htmlFor,
  privacyNote,
  children,
}: {
  label: string;
  description?: string;
  htmlFor?: string;
  privacyNote?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-0.5">
          <Label htmlFor={htmlFor} className="text-sm cursor-pointer">
            {label}
          </Label>
          {description && (
            <p className="text-xs text-muted-foreground max-w-md">{description}</p>
          )}
        </div>
        <div className="shrink-0">{children}</div>
      </div>
      {privacyNote && (
        <div className="flex items-start gap-2 p-2.5 bg-blue-500/5 border border-blue-500/20 rounded-lg">
          <Eye className="h-3.5 w-3.5 text-blue-400 mt-0.5 shrink-0" />
          <p className="text-[11px] text-blue-300/80 leading-relaxed">{privacyNote}</p>
        </div>
      )}
    </div>
  );
}

// ── ConfigRow ─────────────────────────────────────────────────────────

type ConfigHandle = Pick<
  ReturnType<typeof useConfigState>,
  "pendingEdits" | "validationErrors" | "handleChange" | "handleReset"
>;

function ConfigRow({ entry, config }: { entry: ConfigEntry; config: ConfigHandle }) {
  const currentValue = config.pendingEdits[entry.key] ?? entry.value;
  const isDirty = config.pendingEdits[entry.key] !== undefined;
  const isOverride = entry.scope === "workspace" || isDirty;
  const rawError = config.validationErrors[entry.key];
  const error = rawError ? humanizeError(entry, rawError) : undefined;
  const inputId = `setting-${entry.key.replace(/\./g, "-")}`;
  const warnActive = entry.warnWhenValue !== undefined && currentValue === entry.warnWhenValue;
  const isLockedEnum = entry.type === "enum" && (entry.enumOptions?.length ?? 0) === 1;

  return (
    <div
      className={cn(
        "group rounded-lg border border-l-2 bg-card overflow-hidden transition-all",
        error           ? "border-red-500/25 border-l-red-500/70"      :
        isDirty         ? "border-yellow-500/20 border-l-yellow-500/60" :
        warnActive      ? "border-red-500/20 border-l-red-500/50"       :
        entry.invariantNote ? "border-border border-l-amber-500/30"     :
        isOverride      ? "border-border border-l-cyan-500/50"          :
                          "border-border border-l-border/40"
      )}
    >
      <div className="flex items-stretch">
        {/* ── Left: meta + description ─────────────────────── */}
        <div className="flex-1 min-w-0 px-4 pt-3 pb-3 space-y-1.5">
          {/* Row 1: key + scope badge + reset */}
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
                  {isOverride ? "Overridden at workspace scope" : "Using global default value"}
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
                    This value is fixed by the engine. Only one valid option exists.
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

          <Label htmlFor={inputId} className="text-sm cursor-pointer block leading-tight">
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
              <p className="text-[11px] text-amber-400/80 leading-snug">{entry.invariantNote}</p>
            </div>
          )}

          {/* Warn-when-value — red, only when the dangerous value is active */}
          {warnActive && (
            <div className="flex items-start gap-1.5 mt-1 px-2.5 py-1.5 rounded bg-red-500/8 border border-red-500/25">
              <AlertCircle className="h-3 w-3 text-red-400 mt-0.5 shrink-0" />
              <p className="text-[11px] text-red-400 leading-snug">
                Dev override active — verification is bypassed. This is not supported for production
                workspaces and will not be accepted in CI.
              </p>
            </div>
          )}
        </div>

        {/* ── Right: control column ─────────────────────────── */}
        <div
          className={cn(
            "flex items-center justify-center px-4 border-l min-w-[130px] shrink-0",
            isDirty && !error ? "border-yellow-500/15 bg-yellow-500/[0.03]" :
            error             ? "border-red-500/15 bg-red-500/[0.03]"       :
            warnActive        ? "border-red-500/15 bg-red-500/[0.03]"       :
            isOverride        ? "border-cyan-500/10 bg-cyan-500/[0.02]"     :
                                "border-border/40 bg-muted/[0.03]"
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
              <Lock className="h-3.5 w-3.5 text-amber-400/40 shrink-0" aria-label="fixed invariant" />
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

// ── ConfigTab ─────────────────────────────────────────────────────────

type FullConfigState = ReturnType<typeof useConfigState>;

function ConfigTab({
  config,
  expanded,
  toggleCategory,
}: {
  config: FullConfigState;
  expanded: Record<string, boolean>;
  toggleCategory: (cat: string) => void;
}) {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl flex items-center gap-2">
          <FileJson className="h-5 w-5 text-primary" />
          AOS Configuration
        </h1>
        <p className="text-sm text-muted-foreground mt-1 max-w-lg">
          Key/value settings stored in{" "}
          <code className="text-xs font-mono bg-muted px-1.5 py-0.5 rounded">
            .aos/config.json
          </code>
          . Workspace overrides take precedence over global defaults.
          Changes take effect on the next run.
        </p>
      </div>

      <ConfigCategoryAccordion
        config={config}
        expanded={expanded}
        toggleCategory={toggleCategory}
      />
    </div>
  );
}

// ── EngineTab ─────────────────────────────────────────────────────────

function EngineTab({
  workspaceId,
  navigate,
  conn,
}: {
  workspaceId: string | undefined;
  navigate: NavigateFunction;
  conn: ReturnType<typeof useEngineConnection>;
}) {
  const hostConsolePath = `/ws/${workspaceId}/host`;

  const [hostLabel, setHostLabel] = useState("Local Dev Host");
  const [hostUrl, setHostUrl] = useState(DAEMON_BASE_URL);
  const [hostEnv, setHostEnv] = useState("local");

  const pingOk = conn.lastPing?.ok ?? true;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-xl flex items-center gap-2">
          <Server className="h-5 w-5 text-primary" />
          Engine Host
        </h1>
        <p className="text-sm text-muted-foreground mt-1 max-w-lg">
          Save the connection profile for the AOS engine. To monitor or control the
          running process, open the Host Console.
        </p>
      </div>

      <Section
        title="Host Profile"
        description="Identity and connection details stored in your workspace config."
        icon={Server}
      >
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="host-name">Host Label</Label>
            <Input
              id="host-name"
              value={hostLabel}
              onChange={(e) => setHostLabel(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              A friendly name for this host — display only.
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="host-env">Environment</Label>
            <Select value={hostEnv} onValueChange={setHostEnv}>
              <SelectTrigger id="host-env" aria-label="Environment">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="local">Local</SelectItem>
                <SelectItem value="dev">Dev</SelectItem>
                <SelectItem value="staging">Staging</SelectItem>
                <SelectItem value="prod">Prod</SelectItem>
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Affects which config overrides are active.
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="host-url">Base URL</Label>
            <Input
              id="host-url"
              value={hostUrl}
              onChange={(e) => setHostUrl(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              Root address of the engine API. Must be reachable from this machine.
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="host-token">API Key / Auth Token</Label>
            <Input id="host-token" type="password" defaultValue="aos-key-local-dev" />
            <p className="text-xs text-muted-foreground">
              Stored locally. Never sent to third-party services.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3 px-3 py-2.5 bg-muted/20 border border-border rounded-lg">
          {conn.isTesting ? (
            <span className="flex items-center gap-1.5 text-[10px] text-muted-foreground">
              <RefreshCw className="h-3 w-3 animate-spin" />
              Testing…
            </span>
          ) : pingOk ? (
            <Badge className="bg-green-500 hover:bg-green-500 gap-1 text-white text-[10px]">
              <Check className="h-3 w-3" />Connected
            </Badge>
          ) : (
            <Badge className="bg-amber-500 hover:bg-amber-500 gap-1 text-white text-[10px]">
              <AlertCircle className="h-3 w-3" />Unreachable
            </Badge>
          )}
          <span className="text-xs text-muted-foreground">
            Windows Service · {conn.lastPing?.version ?? "v2.4.0-alpha"} · {hostEnv}
          </span>
          {conn.lastPing ? (
            <div className="flex items-center gap-1 ml-auto text-[11px] text-muted-foreground/60">
              <Clock className="h-3 w-3" />
              {conn.lastPing.latencyMs}ms
            </div>
          ) : (
            <div className="flex items-center gap-1 ml-auto text-[11px] text-muted-foreground/60">
              <Clock className="h-3 w-3" />
              Last ping: 2s ago
            </div>
          )}
        </div>

        <div className="flex gap-2 flex-wrap">
          <Button
            variant="outline"
            size="sm"
            className="gap-2"
            disabled={conn.isTesting}
            onClick={async () => {
              const result = await conn.test(hostUrl);
              if (result.ok) toast.success(`Connected · ${result.version} · ${result.latencyMs}ms`);
            }}
          >
            <RefreshCw className={cn("h-3 w-3", conn.isTesting && "animate-spin")} />
            {conn.isTesting ? "Testing…" : "Test Connection"}
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="gap-2"
            disabled={conn.isSaving}
            onClick={async () => {
              const result = await conn.save({ label: hostLabel, baseUrl: hostUrl, env: hostEnv });
              if (result.ok) toast.success("Host profile saved");
            }}
          >
            {conn.isSaving ? <Loader2 className="h-3 w-3 animate-spin" /> : <Save className="h-3 w-3" />}
            {conn.isSaving ? "Saving…" : "Save Host"}
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="gap-2 text-destructive border-destructive/20 hover:bg-destructive/10"
            onClick={() => toast.error("Host forgotten")}
          >
            Forget Host
          </Button>
        </div>
      </Section>

      <div className="flex items-start gap-4 p-4 border border-primary/20 bg-primary/5 rounded-lg">
        <div className="h-9 w-9 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
          <Lock className="h-4 w-4 text-primary" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm">Host Console</p>
          <p className="text-xs text-muted-foreground mt-0.5">
            Start, stop, restart the engine process, view service health, API surface
            reachability, and live uptime. Runtime operations live here, not in Settings.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          className="gap-1.5 shrink-0 border-primary/30 text-primary hover:bg-primary/10"
          onClick={() => navigate(hostConsolePath)}
        >
          Open Host Console
          <ExternalLink className="h-3 w-3" />
        </Button>
      </div>
    </div>
  );
}

// ── ProvidersTab ──────────────────────────────────────────────────────

type ConnStatus = "idle" | "testing" | "connected" | "invalid_key" | "missing_key" | "rate_limited";

function ProviderStatusBadge({ status, lastTested }: { status: ConnStatus; lastTested?: string }) {
  if (status === "testing")
    return (
      <span className="flex items-center gap-1.5 text-[10px] text-muted-foreground">
        <RefreshCw className="h-3 w-3 animate-spin" />
        Testing…
      </span>
    );
  if (status === "connected")
    return (
      <span className="flex items-center gap-1.5 text-[10px] text-green-400">
        <CheckCircle className="h-3 w-3" />
        Connected{lastTested ? ` · ${lastTested}` : ""}
      </span>
    );
  if (status === "invalid_key")
    return (
      <span className="flex items-center gap-1.5 text-[10px] text-red-400">
        <AlertCircle className="h-3 w-3" />
        Invalid key
      </span>
    );
  if (status === "missing_key")
    return (
      <span className="flex items-center gap-1.5 text-[10px] text-yellow-400">
        <AlertCircle className="h-3 w-3" />
        No key configured
      </span>
    );
  if (status === "rate_limited")
    return (
      <span className="flex items-center gap-1.5 text-[10px] text-orange-400">
        <AlertCircle className="h-3 w-3" />
        Rate limited
      </span>
    );
  return (
    <span className="flex items-center gap-1.5 text-[10px] text-muted-foreground/40">
      <Circle className="h-2.5 w-2.5" />
      Not tested
    </span>
  );
}

function KeyField({
  id,
  label,
  configured,
  hint,
  placeholder = "sk-…",
  revealed,
  onToggleReveal,
  onSave,
  isSaving,
}: {
  id: string;
  label: string;
  configured: boolean;
  hint?: string;
  placeholder?: string;
  revealed: boolean;
  onToggleReveal: () => void;
  onSave?: () => Promise<void>;
  isSaving?: boolean;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id} className="text-xs">
        {label}
      </Label>
      {configured ? (
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <div className="flex-1 flex items-center h-8 px-3 rounded-md border border-border bg-muted/20 text-xs font-mono text-muted-foreground overflow-hidden">
              {revealed ? (
                <span className="truncate">{hint ?? "••••••••••••••••"}</span>
              ) : (
                <span className="tracking-widest text-muted-foreground/30">
                  ••••••••••••••••
                </span>
              )}
            </div>
            <button
              type="button"
              onClick={onToggleReveal}
              aria-label={revealed ? "Hide key" : "Reveal key"}
              className="h-8 w-8 flex items-center justify-center rounded-md border border-border text-muted-foreground/50 hover:text-muted-foreground transition-colors shrink-0"
            >
              {revealed ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
            </button>
            <button
              type="button"
              onClick={() => toast.info("Key update dialog")}
              className="h-8 px-2.5 text-[11px] rounded-md border border-border text-muted-foreground/50 hover:text-muted-foreground transition-colors shrink-0"
            >
              Replace
            </button>
          </div>
          <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground/50">
            <Lock className="h-2.5 w-2.5" />
            Stored securely · never written to config.json
          </div>
        </div>
      ) : (
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <Input
              id={id}
              type="password"
              placeholder={placeholder}
              className="h-8 text-xs font-mono flex-1"
            />
            <button
              type="button"
              disabled={isSaving}
              onClick={onSave}
              className="h-8 px-2.5 text-[11px] rounded-md border border-border text-muted-foreground/50 hover:text-muted-foreground transition-colors shrink-0 disabled:opacity-50"
            >
              {isSaving ? "Saving…" : "Save"}
            </button>
          </div>
          <div className="flex items-center gap-1.5 text-[10px] text-yellow-400/70">
            <AlertCircle className="h-2.5 w-2.5" />
            No key configured — provider won't be usable
          </div>
        </div>
      )}
    </div>
  );
}

const PROVIDERS = [
  {
    id: "anthropic",
    name: "Anthropic",
    tagline: "Claude 3.7 Sonnet",
    initials: "An",
    keyConfigured: true,
    keyHint: "sk-ant-api03-••••••••••••4f2a",
    lastTested: "2m ago",
    noKeyNeeded: false,
    isAzure: false,
    models: ["claude-3-7-sonnet", "claude-3-5-haiku", "claude-opus-4"],
    defaultModel: "claude-3-7-sonnet",
  },
  {
    id: "openai",
    name: "OpenAI",
    tagline: "GPT-4o",
    initials: "Op",
    keyConfigured: false,
    keyHint: undefined as string | undefined,
    lastTested: undefined as string | undefined,
    noKeyNeeded: false,
    isAzure: false,
    models: ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo"],
    defaultModel: "gpt-4o",
  },
  {
    id: "azure",
    name: "Azure OpenAI",
    tagline: "Enterprise-managed endpoint",
    initials: "Az",
    keyConfigured: false,
    keyHint: undefined as string | undefined,
    lastTested: undefined as string | undefined,
    noKeyNeeded: false,
    isAzure: true,
    models: [] as string[],
    defaultModel: "",
  },
  {
    id: "gemini",
    name: "Google Gemini",
    tagline: "Gemini 2.0 Flash · enterprise quota",
    initials: "Ge",
    keyConfigured: false,
    keyHint: undefined as string | undefined,
    lastTested: undefined as string | undefined,
    noKeyNeeded: false,
    isAzure: false,
    models: ["gemini-2.0-flash", "gemini-1.5-pro", "gemini-1.5-flash"],
    defaultModel: "gemini-2.0-flash",
  },
  {
    id: "ollama",
    name: "Ollama",
    tagline: "Local · no data leaves your machine",
    initials: "Ol",
    keyConfigured: true,
    keyHint: undefined as string | undefined,
    lastTested: "14m ago",
    noKeyNeeded: true,
    isAzure: false,
    models: ["llama3.2", "codestral", "mistral", "deepseek-r1"],
    defaultModel: "llama3.2",
  },
];

type McpServer = {
  id: string;
  name: string;
  url: string;
  authType: "none" | "bearer" | "header";
  tokenConfigured: boolean;
  tokenHint: string;
  enabled: boolean;
  status: ConnStatus;
  lastChecked: string;
};

// INITIAL_MCP_SERVERS removed — seeded via configDefaults["tools.mcp_endpoints"] in workspace-config-panel.tsx

function ProvidersTab({
  llmProvider,
  onLlmProviderChange,
  isLlmProviderPending,
  llmModel,
  onLlmModelChange,
  isLlmModelPending,
  mcpServers,
  onMcpServersChange,
  isMcpPending,
}: {
  llmProvider: string;
  onLlmProviderChange: (id: string) => void;
  isLlmProviderPending: boolean;
  llmModel: string;
  onLlmModelChange: (model: string) => void;
  isLlmModelPending: boolean;
  mcpServers: McpServer[];
  onMcpServersChange: (servers: McpServer[]) => void;
  isMcpPending: boolean;
}) {
  const [expandedProvider, setExpandedProvider] = useState<string | null>("anthropic");
  const [expandedMcp, setExpandedMcp] = useState<string | null>(null);
  const [revealedKeys, setRevealedKeys] = useState<Record<string, boolean>>({});
  const [connStatus, setConnStatus] = useState<Record<string, ConnStatus>>({
    anthropic: "connected",
    openai: "missing_key",
    azure: "idle",
    gemini: "missing_key",
    ollama: "connected",
  });
  const [savingKeys, setSavingKeys] = useState<Record<string, boolean>>({});
  // Ephemeral per-server test-connection status — NOT persisted to config
  const [testStatuses, setTestStatuses] = useState<Record<string, ConnStatus>>({});
  const [testLastChecked, setTestLastChecked] = useState<Record<string, string>>({});

  // Inline "Add MCP Server" form state
  const [showAddForm, setShowAddForm] = useState(false);
  const [addFormName, setAddFormName] = useState("");
  const [addFormUrl, setAddFormUrl] = useState("");
  const [addFormAuth, setAddFormAuth] = useState<McpServer["authType"]>("none");

  // Merge config data with ephemeral test statuses for rendering
  const displayServers = mcpServers.map((s) => ({
    ...s,
    status: testStatuses[s.id] ?? s.status,
    lastChecked: testLastChecked[s.id] ?? s.lastChecked,
  }));

  // Per-provider model selections for inactive providers — seeded from static defaults.
  // For the active provider the source of truth is the `llmModel` config prop.
  const [perProviderModel, setPerProviderModel] = useState<Record<string, string>>(
    () => Object.fromEntries(PROVIDERS.map((p) => [p.id, p.defaultModel]))
  );

  // Atomically switch both provider and model config keys, preserving each
  // provider's previously-chosen model when toggling back.
  const handleProviderChange = useCallback(
    (id: string) => {
      // Snapshot the current active provider's effective model into local state
      // so it's restored correctly if the user switches back.
      setPerProviderModel((prev) => ({ ...prev, [llmProvider]: llmModel }));
      onLlmProviderChange(id);
      // Write the new provider's locally-remembered model (or its default).
      const nextModel =
        perProviderModel[id] ||
        PROVIDERS.find((p) => p.id === id)?.defaultModel ||
        "";
      onLlmModelChange(nextModel);
      setExpandedProvider(id);
    },
    [llmProvider, llmModel, onLlmProviderChange, onLlmModelChange, perProviderModel]
  );

  const toggleReveal = (id: string) =>
    setRevealedKeys((prev) => ({ ...prev, [id]: !prev[id] }));

  const testConnection = (providerId: string, hasKey: boolean) => {
    setConnStatus((prev) => ({ ...prev, [providerId]: "testing" }));
    setTimeout(() => {
      setConnStatus((prev) => ({
        ...prev,
        [providerId]: hasKey ? "connected" : "invalid_key",
      }));
      if (hasKey) toast.success(`${providerId} connection verified`);
      else toast.error(`${providerId} — API key missing or invalid`);
    }, 1400);
  };

  const testMcp = (id: string) => {
    setTestStatuses((prev) => ({ ...prev, [id]: "testing" }));
    setTimeout(() => {
      setTestStatuses((prev) => ({ ...prev, [id]: "connected" }));
      setTestLastChecked((prev) => ({ ...prev, [id]: "just now" }));
      toast.success("MCP server reachable");
    }, 1200);
  };

  const handleAddMcpServer = () => {
    if (!addFormName.trim() || !addFormUrl.trim()) {
      toast.error("Name and URL are required");
      return;
    }
    const newServer: McpServer = {
      id: `mcp-${Date.now()}`,
      name: addFormName.trim(),
      url: addFormUrl.trim(),
      authType: addFormAuth,
      tokenConfigured: false,
      tokenHint: "",
      enabled: true,
      status: "idle",
      lastChecked: "",
    };
    onMcpServersChange([...mcpServers, newServer]);
    setShowAddForm(false);
    setAddFormName("");
    setAddFormUrl("");
    setAddFormAuth("none");
    toast.success(`${newServer.name} added — apply config to take effect`);
  };

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-xl flex items-center gap-2">
          <Cpu className="h-5 w-5 text-primary" />
          Providers
        </h1>
        <p className="text-sm text-muted-foreground mt-1 max-w-lg">
          Configure which AI provider drives the engine, manage credentials, and connect
          external tool servers.
        </p>
      </div>

      {/* ── LLM Provider ─────────────────────────────────────────── */}
      <div className="space-y-4">
        <div className="border-b border-border pb-3">
          <div className="flex items-center gap-2">
            <Cpu className="h-4 w-4 text-primary shrink-0" />
            <h2 className="text-base">LLM Provider</h2>
          </div>
          <p className="text-xs text-muted-foreground mt-1 ml-6">
            The AI model that reads your codebase and generates changes. Only one provider
            can be active at a time.
          </p>
          {/* Config linkage notice */}
          <div className="mt-2 ml-6 flex items-center gap-2 flex-wrap">
            <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground/50">
              <Info className="h-3 w-3 shrink-0" aria-hidden="true" />
              <span>
                Selecting a provider + model writes{" "}
                <code className="font-mono text-primary/50">tools.llm_provider</code>
                {" "}and{" "}
                <code className="font-mono text-primary/50">tools.llm_model</code>
                {" "}in AOS Config. The engine reads both on every run.
              </span>
            </div>
            {isLlmProviderPending && (
              <span className="inline-flex items-center gap-1 text-[9px] font-mono px-1.5 py-0.5 rounded border border-yellow-500/30 bg-yellow-500/10 text-yellow-400">
                <Zap className="h-2.5 w-2.5" aria-hidden="true" />
                pending — apply from sidebar to take effect
              </span>
            )}
          </div>
        </div>

        <div className="space-y-2">
          {PROVIDERS.map((p) => {
            const isActive = llmProvider === p.id;
            const isExpanded = expandedProvider === p.id;
            const status = connStatus[p.id] ?? "idle";

            return (
              <div
                key={p.id}
                className={cn(
                  "rounded-lg border overflow-hidden transition-all",
                  isActive ? "border-primary/30 bg-primary/[0.02]" : "border-border bg-card"
                )}
              >
                {/* Card header */}
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    type="button"
                    aria-label={`Set ${p.name} as active provider`}
                    onClick={() => handleProviderChange(p.id)}
                    className={cn(
                      "h-4 w-4 rounded-full border-2 shrink-0 transition-colors flex items-center justify-center",
                      isActive
                        ? "border-primary bg-primary"
                        : "border-border/60 hover:border-primary/50"
                    )}
                  >
                    {isActive && <div className="h-1.5 w-1.5 rounded-full bg-background" />}
                  </button>

                  <div
                    className={cn(
                      "h-8 w-8 rounded-md flex items-center justify-center text-xs font-mono shrink-0 border",
                      isActive
                        ? "bg-primary/10 border-primary/20 text-primary"
                        : "bg-muted border-border text-muted-foreground"
                    )}
                  >
                    {p.initials}
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-sm">{p.name}</span>
                      {isActive && (
                        <Badge
                          variant="outline"
                          className="text-[9px] h-4 px-1.5 border-primary/30 text-primary bg-primary/5"
                        >
                          active
                        </Badge>
                      )}
                      {isActive && isLlmProviderPending && (
                        <Badge
                          variant="outline"
                          className="text-[9px] h-4 px-1.5 border-yellow-500/30 text-yellow-400 bg-yellow-500/10"
                        >
                          pending
                        </Badge>
                      )}
                    </div>
                    <div className="flex items-center gap-2 mt-0.5">
                      <span className="text-[11px] text-muted-foreground">{p.tagline}</span>
                      <span className="text-muted-foreground/20">·</span>
                      <ProviderStatusBadge status={status} lastTested={p.lastTested} />
                    </div>
                  </div>

                  <button
                    type="button"
                    aria-label={isExpanded ? "Collapse" : "Expand credentials"}
                    onClick={() => setExpandedProvider(isExpanded ? null : p.id)}
                    className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground/40 hover:text-muted-foreground transition-colors"
                  >
                    <ChevronRight
                      className={cn(
                        "h-4 w-4 transition-transform duration-150",
                        isExpanded && "rotate-90"
                      )}
                    />
                  </button>
                </div>

                {/* Expanded credentials */}
                {isExpanded && (
                  <div className="border-t border-border/50 px-4 py-4 bg-muted/[0.03] space-y-4">
                    {p.isAzure && (
                      <div className="grid grid-cols-2 gap-3">
                        <div className="space-y-1.5">
                          <Label htmlFor="azure-endpoint" className="text-xs">
                            Endpoint URL
                          </Label>
                          <Input
                            id="azure-endpoint"
                            placeholder="https://your-resource.openai.azure.com"
                            className="h-8 text-xs font-mono"
                          />
                        </div>
                        <div className="space-y-1.5">
                          <Label htmlFor="azure-deployment" className="text-xs">
                            Deployment / Model Name
                          </Label>
                          <Input
                            id="azure-deployment"
                            placeholder="gpt-4o-deployment"
                            className="h-8 text-xs font-mono"
                          />
                        </div>
                      </div>
                    )}

                    {!p.noKeyNeeded && (
                      <KeyField
                        id={`${p.id}-key`}
                        label="API Key"
                        configured={p.keyConfigured}
                        hint={p.keyHint}
                        placeholder={p.id === "gemini" ? "AIza…" : undefined}
                        revealed={!!revealedKeys[`${p.id}-key`]}
                        onToggleReveal={() => toggleReveal(`${p.id}-key`)}
                        onSave={async () => {
                          setSavingKeys((prev) => ({ ...prev, [p.id]: true }));
                          await new Promise((r) => setTimeout(r, 800));
                          setSavingKeys((prev) => ({ ...prev, [p.id]: false }));
                          toast.success(`${p.name} API key saved securely`);
                        }}
                        isSaving={!!savingKeys[p.id]}
                      />
                    )}

                    {p.noKeyNeeded && (
                      <div className="space-y-1.5">
                        <Label htmlFor="ollama-host" className="text-xs">
                          Host URL
                        </Label>
                        <Input
                          id="ollama-host"
                          defaultValue="http://localhost:11434"
                          className="h-8 text-xs font-mono"
                        />
                        <p className="text-[10px] text-muted-foreground/50">
                          Ollama doesn't require an API key by default.
                        </p>
                      </div>
                    )}

                    {p.models.length > 0 && (() => {
                      const modelValue = isActive ? llmModel : (perProviderModel[p.id] || p.defaultModel);
                      const showPending = isActive && isLlmModelPending;
                      return (
                        <div className="space-y-1.5">
                          <div className="flex items-center gap-2">
                            <Label htmlFor={`${p.id}-model`} className="text-xs">
                              Model
                            </Label>
                            {showPending && (
                              <span className="inline-flex items-center gap-1 text-[9px] font-mono px-1.5 py-0.5 rounded border border-yellow-500/30 bg-yellow-500/10 text-yellow-400">
                                <Zap className="h-2.5 w-2.5" aria-hidden="true" />
                                pending
                              </span>
                            )}
                            {isActive && (
                              <span className="text-[10px] font-mono text-muted-foreground/40">
                                → tools.llm_model
                              </span>
                            )}
                          </div>
                          <Select
                            value={modelValue}
                            onValueChange={(m) => {
                              setPerProviderModel((prev) => ({ ...prev, [p.id]: m }));
                              if (isActive) onLlmModelChange(m);
                            }}
                          >
                            <SelectTrigger
                              id={`${p.id}-model`}
                              className="h-8 text-xs font-mono w-56"
                            >
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {p.models.map((m) => (
                                <SelectItem key={m} value={m} className="text-xs font-mono">
                                  {m}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          {isActive && !isLlmModelPending && (
                            <p className="text-[10px] text-muted-foreground/40">
                              Applied — engine will use this model on the next run.
                            </p>
                          )}
                        </div>
                      );
                    })()}

                    <div className="flex items-center gap-3 pt-1 border-t border-border/30">
                      <Button
                        variant="outline"
                        size="sm"
                        className="gap-1.5 h-7 text-xs"
                        disabled={status === "testing"}
                        onClick={() => testConnection(p.id, p.keyConfigured || p.noKeyNeeded)}
                      >
                        <RefreshCw
                          className={cn("h-3 w-3", status === "testing" && "animate-spin")}
                        />
                        Test Connection
                      </Button>
                      <ProviderStatusBadge status={status} lastTested={p.lastTested} />
                      {!isActive && (
                        <button
                          type="button"
                          onClick={() => handleProviderChange(p.id)}
                          className="ml-auto text-[11px] text-primary/60 hover:text-primary transition-colors"
                        >
                          Set as active →
                        </button>
                      )}
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* ── MCP Endpoints ─────────────────────────────────────────── */}
      <div className="space-y-4">
        <div className="border-b border-border pb-3">
          <div className="flex items-center gap-2">
            <Globe className="h-4 w-4 text-primary shrink-0" />
            <h2 className="text-base">MCP Endpoints</h2>
          </div>
          <p className="text-xs text-muted-foreground mt-1 ml-6">
            External tool servers AOS can call during a run. Each server has its own
            auth method and health state.
          </p>
          {/* Config linkage notice */}
          <div className="mt-2 ml-6 flex items-center gap-2 flex-wrap">
            <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground/50">
              <Info className="h-3 w-3 shrink-0" aria-hidden="true" />
              <span>
                Changes write{" "}
                <code className="font-mono text-primary/50">tools.mcp_endpoints</code>
                {" "}in AOS Config. Apply from the sidebar to take effect.
              </span>
            </div>
            {isMcpPending && (
              <span className="inline-flex items-center gap-1 text-[9px] font-mono px-1.5 py-0.5 rounded border border-yellow-500/30 bg-yellow-500/10 text-yellow-400">
                <Zap className="h-2.5 w-2.5" aria-hidden="true" />
                pending — apply from sidebar to take effect
              </span>
            )}
          </div>
        </div>

        <div className="space-y-2">
          {displayServers.map((server) => {
            const isOpen = expandedMcp === server.id;

            return (
              <div
                key={server.id}
                className="rounded-lg border border-border bg-card overflow-hidden"
              >
                <div className="flex items-center gap-3 px-4 py-3">
                  <Switch
                    id={`mcp-${server.id}`}
                    aria-label={`Enable ${server.name}`}
                    checked={server.enabled}
                    onCheckedChange={(v) =>
                      onMcpServersChange(
                        mcpServers.map((s) => (s.id === server.id ? { ...s, enabled: v } : s))
                      )
                    }
                  />

                  <div className="h-7 w-7 rounded bg-green-500/10 border border-green-500/20 flex items-center justify-center shrink-0">
                    <Globe className="h-3.5 w-3.5 text-green-500" />
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-sm">{server.name}</span>
                      <Badge
                        variant="outline"
                        className="text-[9px] h-4 px-1.5 font-mono border-border/30 text-muted-foreground/40"
                      >
                        {server.authType}
                      </Badge>
                    </div>
                    <div className="flex items-center gap-2 mt-0.5">
                      <code className="text-[10px] text-muted-foreground/50 truncate">
                        {server.url}
                      </code>
                      <span className="text-muted-foreground/20">·</span>
                      <ProviderStatusBadge status={server.status} lastTested={server.lastChecked} />
                    </div>
                  </div>

                  <button
                    type="button"
                    aria-label={isOpen ? "Collapse" : "Expand"}
                    onClick={() => setExpandedMcp(isOpen ? null : server.id)}
                    className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground/40 hover:text-muted-foreground transition-colors"
                  >
                    <ChevronRight
                      className={cn(
                        "h-4 w-4 transition-transform duration-150",
                        isOpen && "rotate-90"
                      )}
                    />
                  </button>
                </div>

                {isOpen && (
                  <div className="border-t border-border/50 px-4 py-4 bg-muted/[0.03] space-y-4">
                    <div className="space-y-1.5">
                      <Label htmlFor={`mcp-url-${server.id}`} className="text-xs">
                        Endpoint URL
                      </Label>
                      <Input
                        id={`mcp-url-${server.id}`}
                        defaultValue={server.url}
                        className="h-8 text-xs font-mono"
                        onBlur={(e) => {
                          const newUrl = e.target.value.trim();
                          if (newUrl && newUrl !== server.url) {
                            onMcpServersChange(
                              mcpServers.map((s) =>
                                s.id === server.id ? { ...s, url: newUrl } : s
                              )
                            );
                          }
                        }}
                      />
                    </div>

                    <div className="space-y-1.5">
                      <Label htmlFor={`mcp-auth-${server.id}`} className="text-xs">
                        Auth Method
                      </Label>
                      <Select
                        value={server.authType}
                        onValueChange={(v) =>
                          onMcpServersChange(
                            mcpServers.map((s) =>
                              s.id === server.id
                                ? { ...s, authType: v as McpServer["authType"] }
                                : s
                            )
                          )
                        }
                      >
                        <SelectTrigger
                          id={`mcp-auth-${server.id}`}
                          className="h-8 text-xs font-mono w-40"
                        >
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none" className="text-xs font-mono">none</SelectItem>
                          <SelectItem value="bearer" className="text-xs font-mono">bearer</SelectItem>
                          <SelectItem value="header" className="text-xs font-mono">header</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    {server.authType !== "none" && (
                      <div className="space-y-1.5">
                        <Label className="text-xs">
                          {server.authType === "bearer" ? "Bearer Token" : "Header Secret"}
                        </Label>
                        {server.tokenConfigured ? (
                          <div className="space-y-1">
                            <div className="flex items-center gap-2">
                              <div className="flex-1 flex items-center h-8 px-3 rounded-md border border-border bg-muted/20 text-xs font-mono overflow-hidden">
                                {revealedKeys[`mcp-${server.id}`] ? (
                                  <span className="text-muted-foreground truncate">
                                    {server.tokenHint}
                                  </span>
                                ) : (
                                  <span className="tracking-widest text-muted-foreground/30">
                                    ••••••••••••••••
                                  </span>
                                )}
                              </div>
                              <button
                                type="button"
                                onClick={() => toggleReveal(`mcp-${server.id}`)}
                                className="h-8 w-8 flex items-center justify-center rounded-md border border-border text-muted-foreground/50 hover:text-muted-foreground transition-colors shrink-0"
                              >
                                {revealedKeys[`mcp-${server.id}`] ? (
                                  <EyeOff className="h-3.5 w-3.5" />
                                ) : (
                                  <Eye className="h-3.5 w-3.5" />
                                )}
                              </button>
                              <button
                                type="button"
                                onClick={() => toast.info("Token update dialog")}
                                className="h-8 px-2.5 text-[11px] rounded-md border border-border text-muted-foreground/50 hover:text-muted-foreground transition-colors shrink-0"
                              >
                                Replace
                              </button>
                            </div>
                            <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground/50">
                              <Lock className="h-2.5 w-2.5" />
                              Stored securely
                            </div>
                          </div>
                        ) : (
                          <Input
                            type="password"
                            placeholder="Paste token…"
                            className="h-8 text-xs font-mono"
                          />
                        )}
                      </div>
                    )}

                    <div className="flex items-center gap-3 pt-1 border-t border-border/30">
                      <Button
                        variant="outline"
                        size="sm"
                        className="gap-1.5 h-7 text-xs"
                        disabled={server.status === "testing"}
                        onClick={() => testMcp(server.id)}
                      >
                        <RefreshCw
                          className={cn(
                            "h-3 w-3",
                            server.status === "testing" && "animate-spin"
                          )}
                        />
                        Test Endpoint
                      </Button>
                      <ProviderStatusBadge status={server.status} lastTested={server.lastChecked} />
                      <button
                        type="button"
                        onClick={() => {
                          onMcpServersChange(mcpServers.filter((s) => s.id !== server.id));
                          toast.error(`${server.name} removed`);
                        }}
                        className="ml-auto text-[11px] text-destructive/50 hover:text-destructive transition-colors"
                      >
                        Remove server
                      </button>
                    </div>
                  </div>
                )}
              </div>
            );
          })}

          {/* ── Inline Add MCP Server form ────────────────────── */}
          {showAddForm ? (
            <div className="rounded-lg border border-dashed border-primary/30 bg-primary/[0.02] p-4 space-y-3">
              <p className="text-xs text-muted-foreground">New MCP server</p>
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="new-mcp-name" className="text-xs">Display name</Label>
                  <Input
                    id="new-mcp-name"
                    value={addFormName}
                    onChange={(e) => setAddFormName(e.target.value)}
                    placeholder="My Tool Server"
                    className="h-8 text-xs"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="new-mcp-auth" className="text-xs">Auth method</Label>
                  <Select value={addFormAuth} onValueChange={(v) => setAddFormAuth(v as McpServer["authType"])}>
                    <SelectTrigger id="new-mcp-auth" className="h-8 text-xs font-mono">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none" className="text-xs font-mono">none</SelectItem>
                      <SelectItem value="bearer" className="text-xs font-mono">bearer</SelectItem>
                      <SelectItem value="header" className="text-xs font-mono">header</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="new-mcp-url" className="text-xs">Endpoint URL</Label>
                <Input
                  id="new-mcp-url"
                  value={addFormUrl}
                  onChange={(e) => setAddFormUrl(e.target.value)}
                  placeholder="https://my-tool-server.example.com/mcp"
                  className="h-8 text-xs font-mono"
                />
              </div>
              <div className="flex items-center gap-2 pt-1">
                <Button size="sm" className="h-7 text-xs gap-1.5" onClick={handleAddMcpServer}>
                  <Plus className="h-3 w-3" />
                  Add server
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 text-xs text-muted-foreground"
                  onClick={() => {
                    setShowAddForm(false);
                    setAddFormName("");
                    setAddFormUrl("");
                    setAddFormAuth("none");
                  }}
                >
                  Cancel
                </Button>
              </div>
            </div>
          ) : (
            <button
              type="button"
              onClick={() => setShowAddForm(true)}
              className="w-full flex items-center justify-center gap-2 h-10 rounded-lg border border-dashed border-border text-xs text-muted-foreground hover:text-foreground hover:border-border/80 transition-colors"
            >
              <Plus className="h-3.5 w-3.5" />
              Add MCP Server
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

// ── GitTab ────────────────────────────────────────────────────────────

type AuthMethod = "token" | "ssh" | "https";
type FetchStatus = "idle" | "fetching" | "ok" | "error";
type AutoPushPolicy = "never" | "after-approval" | "after-verified";
type RepoState = "not-initialized" | "initialized" | "attached";

// ── WorkspaceTab ──────────────────────────────────────────────────────

function WorkspaceTab({ workspaceId }: { workspaceId: string | undefined }) {
  const { workspace, bootstrapDiagnostic } = useWorkspace(workspaceId);
  const { init, isIniting, initResult } = useWorkspaceInit(workspaceId);
  const { bootstrap: bootstrapWorkspace, isBootstrapping } = useBootstrapWorkspace();
  const { workspaces } = useWorkspaces();
  const { activeWorkspaceId } = useWorkspaceContext();
  const navigate = useNavigate();
  const location = useLocation();
  const workspaceRecordId = isGuidWorkspaceId(workspaceId) ? workspaceId : activeWorkspaceId;

  const { hasAosDir } = workspace;

  // Seed the root path from router state when navigating here from the launcher
  // (e.g. after Init New Project or empty-folder initialization).
  const passedRootPath =
    (location.state as { rootPath?: string } | null)?.rootPath ?? "";
  const initialRootPath = passedRootPath || workspace.repoRoot;

  const [savedRootPath, setSavedRootPath] = useState(initialRootPath);
  const [draftRootPath, setDraftRootPath] = useState(initialRootPath);
  const [rootPathDirty, setRootPathDirty] = useState(!!passedRootPath);
  const [pathSaved, setPathSaved] = useState(!passedRootPath);
  const [browseOpen, setBrowseOpen] = useState(false);

  const sortedWorkspaces = useMemo(
    () =>
      [...workspaces].sort(
        (a, b) => new Date(b.lastOpened).getTime() - new Date(a.lastOpened).getTime()
      ),
    [workspaces]
  );

  const rootPathValidationError =
    draftRootPath.trim() === ""
      ? "Root path is required — the engine cannot operate without a repo root."
      : !/^(\/|[A-Za-z]:[\\/])/.test(draftRootPath.trim())
      ? "Must be an absolute path (e.g. /home/user/my-app or C:\\Users\\dev\\my-app)"
      : "";

  useEffect(() => {
    if (rootPathDirty) return;

    setSavedRootPath(initialRootPath);
    setDraftRootPath(initialRootPath);
    setPathSaved(initialRootPath !== "");
  }, [initialRootPath, rootPathDirty]);

  const pendingRootPathRef = useRef({
    workspaceId: workspaceRecordId,
    draftRootPath,
    rootPathDirty,
    rootPathValidationError,
  });

  useEffect(() => {
    pendingRootPathRef.current = {
      workspaceId: workspaceRecordId,
      draftRootPath,
      rootPathDirty,
      rootPathValidationError,
    };
  }, [workspaceRecordId, draftRootPath, rootPathDirty, rootPathValidationError]);

  useEffect(() => {
    return () => {
      const current = pendingRootPathRef.current;
      if (!current.workspaceId || !current.rootPathDirty || current.rootPathValidationError) return;

      void domainClient.updateWorkspace(current.workspaceId, {
        path: current.draftRootPath.trim(),
      });
    };
  }, []);

  const handleRootPathChange = (val: string) => {
    setDraftRootPath(val);
    setRootPathDirty(val !== savedRootPath);
    setPathSaved(false);
  };

  const handleSaveRootPath = async () => {
    if (rootPathValidationError) return;
    const trimmed = draftRootPath.trim();

    const bootstrapResult = await bootstrapWorkspace(trimmed);
    if (!bootstrapResult) return;
    if (!bootstrapResult.success) {
      toast.error("Workspace initialization failed", {
        description: bootstrapResult.error ?? undefined,
      });
      return;
    }

    try {
      const updated = workspaceRecordId
        ? await domainClient.updateWorkspace(workspaceRecordId, { path: trimmed })
        : null;
      const nextRootPath = updated?.path ?? trimmed;

      setSavedRootPath(nextRootPath);
      setDraftRootPath(nextRootPath);
      setRootPathDirty(false);
      setPathSaved(true);
      toast.success(`Workspace root → ${nextRootPath}`, {
        description: bootstrapResult.gitRepositoryCreated
          ? "Git repository created."
          : "Existing git repository found.",
      });
    } catch {
      toast.error("Failed to save workspace root");
    }
  };

  const handleDiscardRootPath = () => {
    setDraftRootPath(savedRootPath);
    setRootPathDirty(false);
    setPathSaved(true);
  };

  const handleBrowseRootPath = () => {
    setBrowseOpen(true);
  };

  return (
    <div className="space-y-10">
      {/* ── Page header ───────────────────────────────────────── */}
      <div>
        <h1 className="text-xl flex items-center gap-2">
          <FolderOpen className="h-5 w-5 text-primary" />
          Workspace
        </h1>
        <p className="text-sm text-muted-foreground mt-1 max-w-lg">
          Foundational workspace settings. The root path must point to a git-backed
          directory — saving a new path runs bootstrap, which creates a git repository and
          AOS scaffold if either is missing. Git is required for the engine to operate.
        </p>
      </div>

      {bootstrapDiagnostic && (
        <div className="rounded-lg border border-red-500/20 bg-red-500/5 px-4 py-3 space-y-1.5">
          <div className="flex items-center gap-2 text-sm text-red-400">
            <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
            <span>Workspace bootstrap failed</span>
          </div>
          <p className="text-[11px] text-muted-foreground/70 whitespace-pre-wrap font-mono leading-relaxed">
            {bootstrapDiagnostic}
          </p>
        </div>
      )}

      {/* ══════════════════════════════════════════════════════
          § 0  Workspace Root Path
      ══════════════════════════════════════════════════════ */}
      <Section
        title="Workspace Root Path"
        icon={FolderOpen}
        description="Absolute filesystem path to the repository root. Every engine operation — including aos init, execution, and commit — runs relative to this directory."
      >
        {/* Path input row */}
        <div className="space-y-2">
          <Label htmlFor="workspace-root-path" className="text-xs">
            Repository root
          </Label>
          <div className="flex items-center gap-2">
            <div className="relative flex-1">
              <FolderOpen
                className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground/40 pointer-events-none"
                aria-hidden="true"
              />
              <Input
                id="workspace-root-path"
                value={draftRootPath}
                onChange={(e) => handleRootPathChange(e.target.value)}
                placeholder="/absolute/path/to/your/repo"
                className={cn(
                  "pl-9 h-9 text-xs font-mono",
                  rootPathValidationError && rootPathDirty
                    ? "border-red-500/50 focus-visible:ring-red-500/30"
                    : rootPathDirty
                    ? "border-yellow-500/40 focus-visible:ring-yellow-500/20"
                    : "border-border"
                )}
                aria-invalid={!!(rootPathValidationError && rootPathDirty)}
                aria-describedby={rootPathValidationError && rootPathDirty ? "root-path-error" : undefined}
                spellCheck={false}
              />
            </div>
            <Dialog open={browseOpen} onOpenChange={setBrowseOpen}>
              <DialogTrigger asChild>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-9 gap-1.5 text-xs shrink-0"
                  onClick={handleBrowseRootPath}
                >
                  <FolderOpen className="h-3.5 w-3.5" aria-hidden="true" />
                  Browse…
                </Button>
              </DialogTrigger>

              <DialogContent className="max-w-md gap-0 overflow-hidden p-0">
                <DialogHeader className="px-5 pt-5 pb-4 border-b border-border/40">
                  <DialogTitle className="flex items-center gap-2 text-base">
                    <FolderOpen className="h-4 w-4 text-primary shrink-0" />
                    Choose a Workspace Root
                  </DialogTitle>
                  <DialogDescription className="text-xs">
                    Pick a saved workspace root path to populate the field.
                  </DialogDescription>
                </DialogHeader>

                <div className="max-h-[22rem] overflow-y-auto px-5 py-4 space-y-3">
                  {sortedWorkspaces.length === 0 ? (
                    <p className="rounded-lg border border-dashed border-border/40 px-3 py-4 text-sm text-muted-foreground/60">
                      No saved workspaces yet.
                    </p>
                  ) : (
                    sortedWorkspaces.map((ws) => (
                      <button
                        key={ws.id}
                        type="button"
                        onClick={() => {
                          setDraftRootPath(ws.repoRoot);
                          const isSamePath = ws.repoRoot === savedRootPath;
                          setRootPathDirty(!isSamePath);
                          setPathSaved(isSamePath);
                          setBrowseOpen(false);
                        }}
                        className="w-full rounded-lg border border-border/50 bg-card/40 p-3 text-left transition-colors hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                      >
                        <div className="flex items-center justify-between gap-2">
                          <span className="text-sm truncate">{ws.alias ?? ws.projectName}</span>
                          <span className="text-[10px] text-muted-foreground/50 font-mono">
                            {ws.status}
                          </span>
                        </div>
                        <p className="mt-1 text-[10px] font-mono text-muted-foreground/50 truncate">
                          {ws.repoRoot}
                        </p>
                      </button>
                    ))
                  )}
                </div>

                <DialogFooter className="border-t border-border/40 px-5 py-3">
                  <Button type="button" variant="ghost" size="sm" onClick={() => setBrowseOpen(false)}>
                    Close
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </div>

          {/* Validation error */}
          {rootPathValidationError && rootPathDirty && (
            <div
              id="root-path-error"
              role="alert"
              className="flex items-start gap-1.5 px-2.5 py-1.5 rounded bg-red-500/8 border border-red-500/20"
            >
              <AlertCircle className="h-3.5 w-3.5 text-red-400 mt-0.5 shrink-0" aria-hidden="true" />
              <p className="text-[11px] text-red-400 leading-snug">{rootPathValidationError}</p>
            </div>
          )}


          {/* Status + action row */}
          <div className="flex items-center justify-between min-h-[1.75rem]">
            <div className="flex items-center gap-2">
              {pathSaved && !rootPathDirty && savedRootPath !== "" ? (
                <span className="flex items-center gap-1.5 text-[11px] text-green-400">
                  <CheckCircle className="h-3.5 w-3.5" aria-hidden="true" />
                  Path configured
                </span>
              ) : savedRootPath === "" && !rootPathDirty ? (
                <span className="flex items-center gap-1.5 text-[11px] text-red-400">
                  <AlertCircle className="h-3.5 w-3.5" aria-hidden="true" />
                  No root path set — engine will not start
                </span>
              ) : rootPathDirty ? (
                <span className="flex items-center gap-1.5 text-[11px] text-yellow-400">
                  <AlertCircle className="h-3.5 w-3.5" aria-hidden="true" />
                  Unsaved changes
                </span>
              ) : null}
            </div>

            {rootPathDirty && (
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="h-7 px-2 text-[11px] text-muted-foreground"
                  onClick={handleDiscardRootPath}
                >
                  Discard
                </Button>
                <Button
                  type="button"
                  size="sm"
                  className={cn(
                    "h-7 px-3 text-[11px] gap-1.5",
                    rootPathValidationError ? "opacity-50 cursor-not-allowed" : ""
                  )}
                  onClick={handleSaveRootPath}
                  disabled={!!rootPathValidationError || isBootstrapping}
                >
                  {isBootstrapping ? (
                    <><Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" />Initializing…</>
                  ) : (
                    <><Save className="h-3 w-3" aria-hidden="true" />Save Root</>
                  )}
                </Button>
              </div>
            )}
          </div>
        </div>

        {/* Foundational callout */}
        <div className="flex items-start gap-3 px-3 py-2.5 rounded-lg border border-primary/15 bg-primary/[0.03]">
          <Info className="h-3.5 w-3.5 text-primary/50 mt-0.5 shrink-0" aria-hidden="true" />
          <div className="space-y-1 min-w-0">
            <p className="text-[11px] text-muted-foreground/70 leading-relaxed">
              This is the single source of truth for the engine. Both{" "}
              <code className="font-mono text-primary/60">.git/</code> and{" "}
              <code className="font-mono text-primary/60">.aos/</code> must exist here
              for the engine to operate — saving a new path bootstraps both if missing.
              Every task commit executes relative to this root. Set it once — changing
              it mid-project requires re-indexing the codebase.
            </p>
            {savedRootPath && (
              <p className="text-[10px] font-mono text-muted-foreground/35 truncate">
                .aos/ → <span className="text-primary/45">{savedRootPath}/.aos/</span>
              </p>
            )}
          </div>
        </div>
      </Section>

      {/* ══════════════════════════════════════════════════════
          § 1  Workspace Initialization
      ══════════════════════════════════════════════════════ */}
      <Section
        title="Workspace Initialization"
        description="Run aos init to create the .aos/ workspace folder. A git repository is also required — bootstrap creates one automatically when you save a root path. Without both .git/ and .aos/, the engine cannot operate."
        icon={Zap}
      >
        {initResult !== null ? (
          /* ── State C: just completed ── */
          <div className="space-y-4">
            {initResult.ok ? (
              <div className="space-y-3">
                <div className="flex items-center gap-2 text-sm text-green-400">
                  <CheckCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
                  <span>.aos/ created at <code className="font-mono text-xs text-green-300/80">{initResult.aosDir}</code></span>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  className="gap-1.5 text-primary"
                  onClick={() => navigate(`/ws/${workspaceId}/settings/config`)}
                >
                  Continue to AOS Config →
                </Button>
              </div>
            ) : (
              <div className="flex items-center gap-2 text-sm text-red-400">
                <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
                <span>Initialization failed — check the root path and try again.</span>
              </div>
            )}
          </div>
        ) : hasAosDir ? (
          /* ── State A: already initialized ── */
          <div className="space-y-4">
            <div className="flex items-center gap-2 text-sm text-green-400">
              <CheckCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
              <span>.aos/ workspace detected — engine can operate</span>
            </div>
            <p className="text-[11px] text-muted-foreground/60 leading-relaxed">
              Re-running <code className="font-mono text-primary/60">aos init</code> on an existing workspace is safe — it validates the directory structure without overwriting existing artifacts.
            </p>
            <Button
              variant="outline"
              size="sm"
              className="gap-1.5"
              disabled={isIniting}
              onClick={() => {
                if (!savedRootPath) {
                  toast.error("Set and save a root path first");
                  return;
                }
                init(savedRootPath);
              }}
            >
              {isIniting ? (
                <><Loader2 className="h-3.5 w-3.5 animate-spin" />Initializing…</>
              ) : (
                <>Re-run aos init</>
              )}
            </Button>
          </div>
        ) : (
          /* ── State B: not initialized ── */
          <div className="space-y-4">
            <div className="flex items-center gap-2 text-sm text-amber-400">
              <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
              <span>Workspace not initialized — .aos/ directory is missing (a git repository is also required)</span>
            </div>
            <div className="space-y-1.5">
              <p className="text-[11px] text-muted-foreground/60">
                <code className="font-mono text-primary/60">aos init</code> will create:
              </p>
              <ul className="space-y-0.5 ml-3">
                {[".aos/spec/", ".aos/state/", ".aos/evidence/", ".aos/codebase/", ".aos/context/", ".aos/schemas/"].map((dir) => (
                  <li key={dir} className="flex items-center gap-1.5 text-[11px] text-muted-foreground/50">
                    <span className="h-1 w-1 rounded-full bg-muted-foreground/30 shrink-0" />
                    <code className="font-mono text-primary/50">{dir}</code>
                  </li>
                ))}
              </ul>
            </div>
            <Button
              size="sm"
              className="gap-1.5"
              disabled={isIniting}
              onClick={() => {
                if (!savedRootPath) {
                  toast.error("Set and save a root path first");
                  return;
                }
                init(savedRootPath);
              }}
            >
              {isIniting ? (
                <><Loader2 className="h-3.5 w-3.5 animate-spin" />Initializing…</>
              ) : (
                <><Zap className="h-3.5 w-3.5" />Initialize Workspace</>
              )}
            </Button>
          </div>
        )}
      </Section>

      <WorkspaceHealthPanel workspaceId={workspaceId} />
    </div>
  );
}

function GitTab({ workspaceId }: { workspaceId: string | undefined }) {
  // wsName replaces the old `repoRoot` variable removed during WorkspaceTab extraction
  const wsName = workspaceId ?? "my-app";

  const detectedBranch = "main";
  const aheadCount = 0;
  const behindCount = 2;

  // § 1 — Repository Setup  (Git is always required — no enable/disable toggle)
  const { workspace } = useWorkspace(workspaceId);
  const { bootstrap: bootstrapWorkspace, isBootstrapping: isBootstrappingGit } = useBootstrapWorkspace();
  const { refresh: refreshWorkspaces } = useWorkspaces();

  // § 3 — GitHub Connection
  const { start: startGitHubBootstrap, isStarting: isConnectingGitHub } = useGitHubWorkspaceBootstrap();
  const repoState: RepoState = workspace.hasAosDir ? "attached" : "not-initialized";

  // § 2 — Remote Configuration
  const [hasRemote, setHasRemote] = useState(true);
  const [remoteUrl, setRemoteUrl] = useState(`https://github.com/dev/${wsName}.git`);
  const [editingRemote, setEditingRemote] = useState(false);
  const [draftRemote, setDraftRemote] = useState(`https://github.com/dev/${wsName}.git`);
  const [fetchStatus, setFetchStatus] = useState<FetchStatus>("idle");
  const [lastFetched, setLastFetched] = useState<string | null>("2026-02-23T08:15:00Z");
  const [autoPushPolicy, setAutoPushPolicy] = useState<AutoPushPolicy>("never");
  const [pushRemote, setPushRemote] = useState("origin");
  const [pushBranch, setPushBranch] = useState("main");
  const [authMethod, setAuthMethod] = useState<AuthMethod>("token");
  const [tokenRevealed, setTokenRevealed] = useState(false);

  const handleBootstrapRepo = async () => {
    if (!workspace.repoRoot) return;
    const result = await bootstrapWorkspace(workspace.repoRoot);
    if (!result) return;
    if (!result.success) {
      toast.error("Bootstrap failed", { description: result.error ?? undefined });
      return;
    }
    refreshWorkspaces();
    toast.success(result.gitRepositoryCreated ? "Git repository created." : "Existing git repository found.");
  };
  const handleFetch = () => {
    setFetchStatus("fetching");
    setTimeout(() => {
      setFetchStatus("ok");
      setLastFetched(new Date().toISOString());
      toast.success("Fetched from origin — up to date");
    }, 1600);
  };
  const handleSaveRemote = () => {
    setRemoteUrl(draftRemote);
    setEditingRemote(false);
    toast.success(`Remote updated → ${draftRemote}`);
  };
  const handleRemoveRemote = () => {
    setHasRemote(false);
    setRemoteUrl("");
    toast.info("Remote removed — workspace is now local only");
  };
  const handleAddRemote = () => {
    const url = `https://github.com/dev/${wsName}.git`;
    setHasRemote(true);
    setRemoteUrl(url);
    setDraftRemote(url);
    toast.success("Remote added");
  };
  const relFmt = (ts: string) => {
    const diff = Date.now() - new Date(ts).getTime();
    const m = Math.floor(diff / 60000);
    if (m < 1) return "just now";
    if (m < 60) return `${m}m ago`;
    const h = Math.floor(m / 60);
    if (h < 24) return `${h}h ago`;
    return `${Math.floor(h / 24)}d ago`;
  };
  const autoPushOptions: {
    value: AutoPushPolicy;
    label: string;
    description: string;
    recommended?: boolean;
  }[] = [
    {
      value: "never",
      label: "Never",
      description: "Commits stay local. You push manually.",
      recommended: true,
    },
    {
      value: "after-approval",
      label: "After manual approval",
      description: "AOS prompts before each push. You confirm every time.",
    },
    {
      value: "after-verified",
      label: "After verified commit",
      description: "Pushes automatically when the post-run verification step passes.",
    },
  ];

  return (
    <div className="space-y-10">
      {/* ── Page header ───────────────────────────────────────── */}
      <div>
        <h1 className="text-xl flex items-center gap-2">
          <GitBranch className="h-5 w-5 text-primary" />
          Git
          <span className="ml-1 text-[10px] uppercase tracking-wider px-1.5 py-0.5 rounded border border-red-500/40 bg-red-500/10 text-red-400">
            Required
          </span>
        </h1>
        <p className="text-sm text-muted-foreground mt-1 max-w-lg">
          Git is a hard dependency of the AOS engine. The engine will not start unless a valid
          repository is configured for this workspace. AOS uses the repository to read branch
          context, commit task output, and optionally push to origin.
        </p>
      </div>

      {/* ══════════════════════════════════════════════════════
          § 1  Repository Setup
      ══════════════════════════════════════════════════════ */}
      <Section
        title="1. Repository Setup"
        icon={GitBranch}
        description="AOS requires a git repository at the workspace root. Without one the engine cannot execute runs."
      >
        {/* Repo status card */}
        <div
          className={cn(
            "rounded-lg border p-4 space-y-4 transition-opacity",
            repoState === "not-initialized"
              ? "border-red-500/30 bg-red-500/5"
              : "border-green-500/20 bg-green-500/5"
          )}
          aria-label="Repository status"
        >
          {/* Status row */}
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              {repoState === "not-initialized" ? (
                <AlertCircle className="h-4 w-4 text-red-500 shrink-0" aria-hidden="true" />
              ) : (
                <CheckCircle className="h-4 w-4 text-green-500 shrink-0" aria-hidden="true" />
              )}
              <span
                className={cn(
                  "text-xs font-mono",
                  repoState === "not-initialized" ? "text-red-400" : "text-green-400"
                )}
              >
                {repoState === "not-initialized"
                  ? "No repository — engine blocked"
                  : "Attached · existing repo"}
              </span>
            </div>
          </div>

          {/* ── Not-initialized state ── */}
          {repoState === "not-initialized" && (
            <div className="space-y-4">
              <div className="flex items-start gap-2 rounded-md border border-red-500/20 bg-red-500/5 px-3 py-2">
                <AlertCircle className="h-3.5 w-3.5 text-red-400 shrink-0 mt-0.5" aria-hidden="true" />
                <p className="text-[11px] text-red-400/80 leading-relaxed">
                  The AOS engine requires a git repository at{" "}
                  <code className="font-mono text-red-300/70">{wsName}</code>. Create a fresh
                  repo or attach an existing one — the engine will remain blocked until this
                  is resolved.
                </p>
              </div>

              {/* Action buttons */}
              <div className="flex gap-2">
                <Button
                  className="flex-1 gap-1.5 h-8 text-xs"
                  disabled={isBootstrappingGit}
                  onClick={handleBootstrapRepo}
                >
                  <GitBranch className="h-3.5 w-3.5" aria-hidden="true" />
                  {isBootstrappingGit ? "Initializing…" : "Create Git Repo"}
                </Button>
                <Button
                  variant="outline"
                  className="flex-1 gap-1.5 h-8 text-xs"
                  disabled={isBootstrappingGit}
                  onClick={handleBootstrapRepo}
                >
                  <Link2 className="h-3.5 w-3.5" aria-hidden="true" />
                  Attach Existing Repo
                </Button>
              </div>
            </div>
          )}

          {/* ── Initialized / Attached state ── */}
          {repoState !== "not-initialized" && (
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-[11px] font-mono">
                <FolderOpen
                  className="h-3.5 w-3.5 text-muted-foreground/30 shrink-0"
                  aria-hidden="true"
                />
                <code className="text-primary/60 truncate">{wsName}</code>
              </div>
              <div className="flex items-center gap-2 text-[11px] font-mono">
                <GitBranch
                  className="h-3.5 w-3.5 text-muted-foreground/30 shrink-0"
                  aria-hidden="true"
                />
                <span className="text-primary/60">{detectedBranch}</span>
                <span className="text-muted-foreground/30 text-[10px]">current branch</span>
              </div>
            </div>
          )}
        </div>

      </Section>

      {/* ══════════════════════════════════════════════════════
          § 2  Remote Configuration
      ══════════════════════════════════════════════════════ */}
      <Section
        title="2. Remote Configuration"
        icon={Globe}
        description="Is this repo local-only or connected upstream? A remote lets AOS fetch context and push commits."
      >
        {/* Origin remote URL */}
        <div className="space-y-1.5">
          <Label className="text-xs">Origin remote</Label>

          {!hasRemote ? (
            <div className="flex items-center gap-3 h-9 px-3 rounded-md border border-dashed border-border/50 bg-muted/10">
              <WifiOff className="h-3.5 w-3.5 text-muted-foreground/30 shrink-0" aria-hidden="true" />
              <span className="text-xs text-muted-foreground/40 italic flex-1">
                Local only — no upstream configured
              </span>
              <Button
                size="sm"
                variant="ghost"
                className="h-6 gap-1 text-[11px] text-primary/60 hover:text-primary px-2"
                onClick={handleAddRemote}
                aria-label="Add remote"
              >
                <Plus className="h-3 w-3" aria-hidden="true" />
                Add remote
              </Button>
            </div>
          ) : editingRemote ? (
            <div className="flex items-center gap-2">
              <Input
                autoFocus
                value={draftRemote}
                onChange={(e) => setDraftRemote(e.target.value)}
                className="h-8 text-xs font-mono flex-1"
                placeholder="https://github.com/org/repo.git"
                aria-label="Remote origin URL"
              />
              <Button size="sm" className="h-8 gap-1.5 text-xs" onClick={handleSaveRemote}>
                <Check className="h-3 w-3" />
                Save
              </Button>
              <Button
                size="sm"
                variant="ghost"
                className="h-8 w-8 p-0"
                onClick={() => { setEditingRemote(false); setDraftRemote(remoteUrl); }}
                aria-label="Cancel editing remote"
              >
                <X className="h-3.5 w-3.5" />
              </Button>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <div className="flex-1 flex items-center h-8 px-3 rounded-md border border-border bg-muted/20 text-xs font-mono text-primary/70 overflow-hidden">
                <Globe className="h-3 w-3 text-muted-foreground/30 mr-2 shrink-0" aria-hidden="true" />
                <span className="truncate">{remoteUrl}</span>
              </div>
              <button
                type="button"
                onClick={() => { setEditingRemote(true); setDraftRemote(remoteUrl); }}
                className="h-8 px-2.5 text-[11px] rounded-md border border-border text-muted-foreground/50 hover:text-muted-foreground transition-colors shrink-0"
              >
                Edit
              </button>
              <button
                type="button"
                onClick={handleRemoveRemote}
                className="h-8 w-8 flex items-center justify-center rounded-md border border-border text-muted-foreground/40 hover:text-red-400 hover:border-red-500/30 transition-colors shrink-0"
                aria-label="Remove remote"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </div>
          )}
        </div>

        {/* Fetch remote status */}
        {hasRemote && (
          <div className="space-y-2">
            <Label className="text-xs">Fetch remote status</Label>
            <div className="flex items-center gap-3 flex-wrap">
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5 h-7 text-xs"
                disabled={fetchStatus === "fetching"}
                onClick={handleFetch}
              >
                <RefreshCw className={cn("h-3 w-3", fetchStatus === "fetching" && "animate-spin")} />
                Fetch Now
              </Button>
              {fetchStatus === "ok" && (
                <span className="flex items-center gap-1 text-[10px] text-green-400">
                  <CheckCircle className="h-3 w-3" />
                  Fetched successfully
                </span>
              )}
              {fetchStatus === "error" && (
                <span className="flex items-center gap-1 text-[10px] text-red-400">
                  <AlertCircle className="h-3 w-3" />
                  Fetch failed — check credentials
                </span>
              )}
              {fetchStatus === "idle" && lastFetched && (
                <span className="flex items-center gap-1 text-[10px] text-muted-foreground/40">
                  <Clock className="h-3 w-3" />
                  Last fetched {relFmt(lastFetched)}
                </span>
              )}
              {fetchStatus === "idle" && !lastFetched && (
                <span className="flex items-center gap-1 text-[10px] text-muted-foreground/30">
                  <Circle className="h-2.5 w-2.5" />
                  Never fetched
                </span>
              )}
            </div>

            {/* Ahead / behind indicator */}
            <div className="flex items-center gap-1.5">
              <span
                className={cn(
                  "inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-mono border",
                  aheadCount > 0
                    ? "border-primary/20 bg-primary/5 text-primary/70"
                    : "border-border/40 bg-muted/20 text-muted-foreground/40"
                )}
              >
                ↑ {aheadCount} ahead
              </span>
              <span
                className={cn(
                  "inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-mono border",
                  behindCount > 0
                    ? "border-yellow-500/25 bg-yellow-500/5 text-yellow-400/80"
                    : "border-border/40 bg-muted/20 text-muted-foreground/40"
                )}
              >
                ↓ {behindCount} behind
              </span>
            </div>
          </div>
        )}

        <Separator />

        {/* Auto-push policy */}
        <div className="space-y-3">
          <div>
            <Label className="text-sm">Auto-push policy</Label>
            <p className="text-xs text-muted-foreground mt-0.5 max-w-md">
              Controls when AOS pushes committed branches to origin. Defaults to{" "}
              <code className="text-[11px] font-mono bg-muted px-1 rounded">never</code> —
              recommended until auth is verified.
            </p>
          </div>
          <div className="grid grid-cols-1 gap-2">
            {autoPushOptions.map((option) => (
              <button
                key={option.value}
                type="button"
                onClick={() => {
                  setAutoPushPolicy(option.value);
                  toast.success(`Auto-push: ${option.label}`);
                }}
                className={cn(
                  "w-full text-left px-3 py-2.5 rounded-md border transition-colors",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary",
                  autoPushPolicy === option.value
                    ? "border-primary/40 bg-primary/5 text-foreground"
                    : "border-border/40 bg-muted/10 text-muted-foreground hover:bg-muted/20 hover:border-border"
                )}
                aria-pressed={autoPushPolicy === option.value}
                aria-label={`Auto-push policy: ${option.label}`}
              >
                <div className="flex items-center gap-2">
                  <div
                    className={cn(
                      "h-3.5 w-3.5 rounded-full border-2 shrink-0 flex items-center justify-center",
                      autoPushPolicy === option.value
                        ? "border-primary bg-primary"
                        : "border-border/60"
                    )}
                  >
                    {autoPushPolicy === option.value && (
                      <div className="h-1.5 w-1.5 rounded-full bg-background" />
                    )}
                  </div>
                  <span className="text-xs">{option.label}</span>
                  {option.recommended && (
                    <span className="ml-auto text-[9px] uppercase tracking-wider text-muted-foreground/40 border border-border/40 px-1.5 py-0.5 rounded">
                      recommended
                    </span>
                  )}
                </div>
                <p className="text-[10px] text-muted-foreground/50 ml-5 mt-0.5">
                  {option.description}
                </p>
              </button>
            ))}
          </div>
        </div>

        {/* Default push target */}
        {hasRemote && (
          <>
            <Separator />
            <div className="space-y-3">
              <div>
                <Label className="text-sm">Default push target</Label>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Remote and branch to push to when auto-push fires.
                </p>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="git-push-remote" className="text-xs">Remote</Label>
                  <Input
                    id="git-push-remote"
                    value={pushRemote}
                    onChange={(e) => setPushRemote(e.target.value)}
                    className="h-8 text-xs font-mono"
                    aria-label="Push remote"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="git-push-branch" className="text-xs">Branch</Label>
                  <Input
                    id="git-push-branch"
                    value={pushBranch}
                    onChange={(e) => setPushBranch(e.target.value)}
                    className="h-8 text-xs font-mono"
                    aria-label="Push branch"
                  />
                </div>
              </div>
            </div>
          </>
        )}

        <Separator />

        {/* Credentials */}
        <div className="space-y-3">
          <div>
            <Label className="text-sm">Remote auth credentials</Label>
            <p className="text-xs text-muted-foreground mt-0.5 max-w-md">
              Credentials used for fetch and push operations against the remote.
            </p>
          </div>


          {/* Status pill */}
          <div
            className={cn(
              "inline-flex items-center gap-1.5 px-2 py-1 rounded-md text-[10px] border",
              authMethod === "token"
                ? "border-green-500/20 bg-green-500/5 text-green-400"
                : "border-yellow-500/20 bg-yellow-500/5 text-yellow-400/80"
            )}
          >
            {authMethod === "token" ? (
              <CheckCircle className="h-3 w-3" aria-hidden="true" />
            ) : (
              <Circle className="h-2.5 w-2.5" aria-hidden="true" />
            )}
            {authMethod === "token" ? "Token configured" : "Credentials not verified"}
          </div>

          <SettingRow label="Auth method" htmlFor="git-auth-method-2">
            <Select value={authMethod} onValueChange={(v) => setAuthMethod(v as AuthMethod)}>
              <SelectTrigger id="git-auth-method-2" aria-label="Auth method" className="w-32 h-8 text-xs font-mono">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="token" className="text-xs font-mono">token</SelectItem>
                <SelectItem value="https" className="text-xs font-mono">https</SelectItem>
                <SelectItem value="ssh" className="text-xs font-mono">ssh</SelectItem>
              </SelectContent>
            </Select>
          </SettingRow>

          {authMethod === "token" && (
            <div className="space-y-1.5">
              <Label className="text-xs">Personal access token</Label>
              <div className="flex items-center gap-2">
                <div className="flex-1 flex items-center h-8 px-3 rounded-md border border-border bg-muted/20 text-xs font-mono overflow-hidden">
                  {tokenRevealed ? (
                    <span className="text-primary/60 truncate">ghp_••••••••••••••••4f2a</span>
                  ) : (
                    <span className="tracking-widest text-muted-foreground/30">••••••••••••••••</span>
                  )}
                </div>
                <button
                  type="button"
                  onClick={() => setTokenRevealed((v) => !v)}
                  aria-label={tokenRevealed ? "Hide token" : "Reveal token"}
                  className="h-8 w-8 flex items-center justify-center rounded-md border border-border text-muted-foreground/50 hover:text-muted-foreground transition-colors shrink-0"
                >
                  {tokenRevealed ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
                </button>
                <button
                  type="button"
                  onClick={() => toast.info("Token replacement flow")}
                  className="h-8 px-2.5 text-[11px] rounded-md border border-border text-muted-foreground/50 hover:text-muted-foreground transition-colors shrink-0"
                >
                  Replace
                </button>
              </div>
              <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground/50">
                <Lock className="h-2.5 w-2.5" aria-hidden="true" />
                Stored in OS keychain · never written to config files
              </div>
            </div>
          )}

          {authMethod === "ssh" && (
            <div className="space-y-1.5">
              <Label htmlFor="git-ssh-key" className="text-xs">SSH key path</Label>
              <Input
                id="git-ssh-key"
                defaultValue="~/.ssh/id_ed25519"
                className="h-8 text-xs font-mono"
                aria-label="SSH key path"
              />
              <p className="text-[10px] text-muted-foreground/40">
                Private key on this machine. The matching public key must be registered with the remote host.
              </p>
            </div>
          )}

          {authMethod === "https" && (
            <div className="grid gap-3 md:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="git-user" className="text-xs">Username</Label>
                <Input id="git-user" defaultValue="dev" className="h-8 text-xs font-mono" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="git-pass" className="text-xs">Password / token</Label>
                <Input id="git-pass" type="password" defaultValue="••••••••" className="h-8 text-xs font-mono" />
              </div>
            </div>
          )}
        </div>
      </Section>

      {/* ══════════════════════════════════════════════════════
          § 3  GitHub Connection
      ══════════════════════════════════════════════════════ */}
      <Section
        title="3. GitHub Connection"
        icon={GitBranch}
        description="Connect this workspace to GitHub via OAuth. The backend creates or reuses a repository and configures origin automatically — no manual git remote commands needed."
      >
        <div className="space-y-4">
          <p className="text-xs text-muted-foreground max-w-md">
            Your browser will open the GitHub OAuth page. After you authorize, the backend
            provisions the repository (creating it if it does not exist) and sets{" "}
            <code className="font-mono text-[11px] bg-muted px-1 rounded">origin</code>{" "}
            for this workspace automatically.
          </p>

          {/* Current origin state — informational */}
          {hasRemote && (
            <div className="flex items-center gap-2 rounded-md border border-green-500/20 bg-green-500/5 px-3 py-2">
              <CheckCircle className="h-3.5 w-3.5 text-green-400 shrink-0" aria-hidden="true" />
              <span className="text-[11px] text-green-400/80">
                Origin is set —{" "}
                <code className="font-mono text-[10px]">{remoteUrl}</code>. Re-connecting
                will replace it.
              </span>
            </div>
          )}

          <Button
            size="sm"
            variant={hasRemote ? "outline" : "default"}
            className="gap-1.5"
            disabled={isConnectingGitHub || !workspace.repoRoot}
            onClick={async () => {
              if (!workspace.repoRoot) {
                toast.error("Set and save a workspace root path first");
                return;
              }
              const resp = await startGitHubBootstrap({
                path: workspace.repoRoot,
                name: workspace.projectName || wsName,
              });
              if (resp?.authorizeUrl) {
                window.location.href = resp.authorizeUrl;
              }
            }}
            aria-label={hasRemote ? "Reconnect GitHub" : "Connect to GitHub"}
          >
            {isConnectingGitHub ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
                Connecting…
              </>
            ) : (
              <>
                <GitBranch className="h-3.5 w-3.5" aria-hidden="true" />
                {hasRemote ? "Reconnect GitHub" : "Connect to GitHub"}
              </>
            )}
          </Button>

          {!workspace.repoRoot && (
            <p className="text-[11px] text-muted-foreground/50">
              A workspace root path is required before connecting to GitHub.
            </p>
          )}
        </div>
      </Section>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════
// Outlet context type — shared between SettingsPage and child routes
// ═══════════════════════════════════════════════════════════════════════

type SettingsOutletContext = {
  config: ReturnType<typeof useConfigState>;
  expanded: Record<string, boolean>;
  toggleCategory: (cat: string) => void;
  workspaceId: string | undefined;
  navigate: NavigateFunction;
  analytics: ReturnType<typeof useSettingsAnalytics>;
};

// ═══════════════════════════════════════════════════════════════════════
// SettingsPage
// ═══════════════════════════════════════════════════════════════════════

export function SettingsPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const { workspace: defaultWs } = useWorkspace();
  const wsName = workspaceId ?? defaultWs.projectName;

  const location = useLocation();
  const TAB_IDS = ["overview", "workspace", "config", "engine", "providers", "git"];
  const activeTab = TAB_IDS.find((id) => location.pathname.includes(`/settings/${id}`)) ?? "overview";

  const [expanded, setExpanded] = useState<Record<string, boolean>>({
    Execution: true,
    Context: true,
    Verification: false,
    Git: false,
  });
  const analytics = useSettingsAnalytics();
  const config = useConfigState(wsName, analytics.trackChange);

  const toggleCategory = useCallback(
    (cat: string) => setExpanded((prev) => ({ ...prev, [cat]: !prev[cat] })),
    []
  );

  const tabs = [
    { id: "overview",   label: "Overview",    icon: Search },
    { id: "workspace",  label: "Workspace",   icon: FolderOpen },
    { id: "config",     label: "AOS Config",  icon: FileJson },
    { id: "engine",     label: "Engine Host", icon: Server },
    { id: "providers",  label: "Providers",   icon: Cpu },
    { id: "git",        label: "Git",         icon: GitBranch },
  ];

  return (
    <div className="flex h-full bg-background">
      {/* ── Sidebar ──────────────────────────────────────────────── */}
      <div className="w-64 border-r border-border bg-muted/10 flex flex-col">
        <div className="p-4 border-b border-border">
          <h1 className="text-base flex items-center gap-2">
            <Settings className="h-4 w-4 text-primary" />
            Settings
          </h1>
          <p className="text-[11px] text-muted-foreground mt-0.5">{wsName}</p>
        </div>

        <ScrollArea className="flex-1 py-2">
          <nav className="space-y-0.5 px-2" aria-label="Settings sections">
            {tabs.map((item) => (
              <button
                key={item.id}
                role="tab"
                aria-selected={activeTab === item.id}
                onClick={() => navigate(`/ws/${wsName}/settings/${item.id}`)}
                className={cn(
                  "w-full flex items-center gap-2.5 h-9 px-3 rounded-md text-sm transition-colors text-left",
                  activeTab === item.id
                    ? "bg-muted text-foreground"
                    : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
                )}
              >
                <item.icon className="h-4 w-4 shrink-0" />
                {item.label}
                {item.id === "config" && config.dirtyCount > 0 && (
                  <Badge
                    variant="outline"
                    className="ml-auto text-[9px] h-4 px-1 border-yellow-500/30 text-yellow-400 bg-yellow-500/10"
                  >
                    {config.dirtyCount}
                  </Badge>
                )}
              </button>
            ))}
          </nav>
        </ScrollArea>

        {config.dirtyCount > 0 && (
          <div className="p-3 border-t border-yellow-500/20 bg-yellow-500/5 space-y-2">
            <p className="text-xs text-yellow-400 flex items-center gap-1.5">
              <Zap className="h-3 w-3" />
              {config.dirtyCount} unsaved change{config.dirtyCount !== 1 ? "s" : ""}
            </p>
            {config.hasErrors && (
              <p className="text-[10px] text-red-400 flex items-center gap-1">
                <AlertCircle className="h-3 w-3" />
                Fix errors before applying
              </p>
            )}
            <div className="flex gap-2">
              <Button
                variant="ghost"
                size="sm"
                className="flex-1 text-xs h-7"
                onClick={config.handleDiscard}
              >
                Discard all
              </Button>
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button
                      size="sm"
                      className="flex-1 gap-1 text-xs h-7"
                      disabled={config.hasErrors || config.isApplying}
                      onClick={config.handleApply}
                      aria-label="Apply all pending config changes"
                    >
                      {config.isApplying ? (
                        <><Loader2 className="h-3 w-3 animate-spin" />Applying…</>
                      ) : (
                        <><Save className="h-3 w-3" />Apply</>
                      )}
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent side="top" className="font-mono text-[10px] max-w-xs space-y-0.5">
                    {Object.entries(config.pendingEdits).slice(0, 5).map(([key, val]) => (
                      <p key={key}>aos config set {key} {val}</p>
                    ))}
                    {Object.keys(config.pendingEdits).length > 5 && (
                      <p className="text-muted-foreground/50">
                        +{Object.keys(config.pendingEdits).length - 5} more
                      </p>
                    )}
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            </div>
          </div>
        )}
        {config.dirtyCount === 0 && (
          <div className="p-3 border-t border-border">
            <Button className="w-full gap-2 h-8 text-xs" disabled aria-label="No pending changes">
              <Save className="h-3.5 w-3.5" />
              No Changes
            </Button>
          </div>
        )}

      </div>

      {/* ── Main Content ─────────────────────────────────────────── */}
      <div className="flex-1 overflow-auto">
        <div className="max-w-3xl mx-auto p-8">
          <Outlet context={{ config, expanded, toggleCategory, workspaceId, navigate, analytics } satisfies SettingsOutletContext} />
        </div>
      </div>

    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════
// SettingsOverview — searchable index, default settings landing
// ═══════════════════════════════════════════════════════════════════════

type SearchEntry = {
  id: string;
  label: string;
  description: string;
  tab: string;
  tabLabel: string;
  icon: React.ElementType;
  keywords?: string[];
};

const SETTINGS_INDEX: SearchEntry[] = [
  { id: "exec.model",      label: "Execution Model",       description: "The LLM model used for code execution and task planning.",        tab: "config",    tabLabel: "AOS Config",  icon: Cpu,       keywords: ["llm","model","gpt","claude"] },
  { id: "exec.timeout",    label: "Execution Timeout",     description: "Maximum seconds allowed for a single execution step.",            tab: "config",    tabLabel: "AOS Config",  icon: Clock },
  { id: "exec.retries",    label: "Retry Attempts",        description: "Number of retry attempts before a task is marked failed.",        tab: "config",    tabLabel: "AOS Config",  icon: RefreshCw },
  { id: "exec.shell",      label: "Shell",                 description: "The shell binary AOS uses when running commands.",               tab: "config",    tabLabel: "AOS Config",  icon: Code },
  { id: "exec.mode",           label: "Execution Mode",          description: "Whether AOS runs in atomic or manual sequencing mode.",            tab: "config",    tabLabel: "AOS Config",  icon: Play },
  { id: "exec.atomic_budget",  label: "Atomic Step Budget",      description: "Circuit-breaker cap on sub-steps before a forced checkpoint. Max 5.", tab: "config",    tabLabel: "AOS Config",  icon: Code,      keywords: ["steps","budget","circuit breaker","atomic"] },
  { id: "ctx.max_tokens",      label: "Max Context Tokens",      description: "Token budget for the context window per run.",                        tab: "config",    tabLabel: "AOS Config",  icon: FileJson,  keywords: ["tokens","budget","context window"] },
  { id: "ctx.root",        label: "Workspace Root",        description: "Root directory AOS uses as the working directory.",              tab: "config",    tabLabel: "AOS Config",  icon: FolderOpen },
  { id: "ctx.file_limit",  label: "File Read Limit",       description: "Maximum number of files scanned for context per run.",           tab: "config",    tabLabel: "AOS Config",  icon: FileJson },
  { id: "verify.enforcement", label: "Verification Enforcement", description: "Hard gate: required (default) or dev_override_unsafe. Production workspaces must stay on required.", tab: "config", tabLabel: "AOS Config", icon: CheckCircle, keywords: ["verification","required","bypass","override","unsafe"] },
  { id: "verify.target",     label: "Verification Target",      description: "Scope of the default verification check — task, phase, or plan.",  tab: "config",    tabLabel: "AOS Config",  icon: Shield },
  { id: "engine.address",  label: "Engine Address",        description: "Host and port of the AOS engine process.",                       tab: "engine",    tabLabel: "Engine Host", icon: Server,    keywords: ["host","port","address","url"] },
  { id: "engine.restart",  label: "Auto-restart on Crash", description: "Automatically restart the engine if the process crashes.",       tab: "engine",    tabLabel: "Engine Host", icon: RefreshCw },
  { id: "engine.log",      label: "Log Level",             description: "Verbosity of engine logs: debug / info / warn / error.",         tab: "engine",    tabLabel: "Engine Host", icon: Server,    keywords: ["debug","logging","verbose"] },
  { id: "engine.memory",   label: "Memory Limit",          description: "Maximum RAM the engine process is allowed to consume.",          tab: "engine",    tabLabel: "Engine Host", icon: Server,    keywords: ["ram","memory","limit"] },
  { id: "prov.openai",     label: "OpenAI API Key",        description: "API key and model selection for OpenAI GPT models.",             tab: "providers", tabLabel: "Providers",   icon: Key,       keywords: ["gpt","openai","api key"] },
  { id: "prov.anthropic",  label: "Anthropic API Key",     description: "API key for Claude 3 / Sonnet / Haiku models.",                 tab: "providers", tabLabel: "Providers",   icon: Key,       keywords: ["claude","anthropic","api key"] },
  { id: "prov.gemini",     label: "Google Gemini Key",     description: "API key and project ID for Google Gemini Pro.",                 tab: "providers", tabLabel: "Providers",   icon: Key,       keywords: ["gemini","google","api key"] },
  { id: "prov.default",    label: "Default Provider",      description: "Which provider AOS falls back to when no model is specified.",   tab: "providers", tabLabel: "Providers",   icon: Cpu },
  { id: "prov.model",      label: "Active Model",           description: "Model identifier written to tools.llm_model and passed to the active provider on every run.", tab: "providers", tabLabel: "Providers",   icon: Cpu,       keywords: ["model","claude","gpt","llm","sonnet","haiku","opus","tools.llm_model"] },
  { id: "ws.root_path",    label: "Workspace Root Path",   description: "Absolute filesystem path to the repository root. Every engine operation runs relative to this directory.", tab: "workspace", tabLabel: "Workspace",   icon: FolderOpen, keywords: ["root path","repo root","filesystem","absolute path","aos init","reindex"] },
  { id: "git.repo_state",  label: "Repository State",      description: "Bootstrap or attach the git repository at the workspace root.", tab: "git", tabLabel: "Git",         icon: FolderOpen, keywords: ["init","initialize","attach","create repo","bootstrap"] },
  { id: "git.remote_url",  label: "Origin Remote URL",     description: "The upstream URL AOS fetches and pushes to.",                   tab: "git",       tabLabel: "Git",         icon: Globe,     keywords: ["origin","remote","github","gitlab"] },
  { id: "git.commit_policy", label: "Commit Policy",        description: "Fixed invariant: one task = one commit. per_phase and manual are not supported.", tab: "git", tabLabel: "Git", icon: GitCommit, keywords: ["commit","per_task","atomic","invariant"] },
  { id: "git.autopush",      label: "Auto-push Policy",     description: "Controls when AOS pushes committed branches to origin.",        tab: "git",       tabLabel: "Git",         icon: Globe,     keywords: ["push","sync","auto push"] },
  { id: "git.auth",        label: "Remote Auth Credentials", description: "Token, SSH key, or HTTPS credentials for the remote.",        tab: "git",       tabLabel: "Git",         icon: Key,       keywords: ["token","ssh","credentials","auth"] },
  { id: "git.push_target", label: "Default Push Target",   description: "Remote name and branch to push to when auto-push fires.",       tab: "git",       tabLabel: "Git",         icon: GitCommit },
];

const OVERVIEW_CATEGORY_CARDS = [
  { id: "config",    label: "AOS Config",   description: "Core execution, context, and verification key/value settings.",      icon: FileJson,  count: 10, accent: "border-primary/20 hover:border-primary/40 hover:bg-primary/5", iconColor: "text-primary" },
  { id: "engine",    label: "Engine Host",  description: "Local engine process address, restart policy, and log level.",       icon: Server,    count: 4,  accent: "border-border/40 hover:border-border hover:bg-muted/20",       iconColor: "text-muted-foreground" },
  { id: "providers", label: "Providers",    description: "API keys and default model selection for each LLM provider.",        icon: Cpu,       count: 4,  accent: "border-border/40 hover:border-border hover:bg-muted/20",       iconColor: "text-muted-foreground" },
  { id: "git",       label: "Git",          description: "Repository setup, upstream remote, credentials, and push policy.",   icon: GitBranch, count: 6,  accent: "border-border/40 hover:border-border hover:bg-muted/20",       iconColor: "text-muted-foreground" },
] as const;

const OVERVIEW_TAB_ACCENT: Record<string, string> = {
  config:    "bg-primary/10 text-primary border-primary/20",
  engine:    "bg-blue-500/10 text-blue-400 border-blue-500/20",
  providers: "bg-purple-500/10 text-purple-400 border-purple-500/20",
  git:       "bg-orange-500/10 text-orange-400 border-orange-500/20",
};

const OVERVIEW_QUICK_LINKS = [
  { label: "Execution model",  tab: "config" },
  { label: "API keys",         tab: "providers" },
  { label: "Origin remote",    tab: "git" },
  { label: "Engine address",   tab: "engine" },
  { label: "Auto-push policy", tab: "git" },
  { label: "Verification",     tab: "config" },
];

function SettingsOverview() {
  const { workspaceId, navigate, analytics, config } = useOutletContext<SettingsOutletContext>();
  const { workspace: defaultWs2 } = useWorkspace();
  const wsName = workspaceId ?? defaultWs2.projectName;

  const [query, setQuery] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const results = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return [];
    return SETTINGS_INDEX.filter(
      (e) =>
        e.label.toLowerCase().includes(q) ||
        e.description.toLowerCase().includes(q) ||
        e.tab.toLowerCase().includes(q) ||
        e.tabLabel.toLowerCase().includes(q) ||
        (e.keywords ?? []).some((kw) => kw.toLowerCase().includes(q))
    );
  }, [query]);

  const grouped = useMemo(() => {
    const map = new Map<string, SearchEntry[]>();
    for (const r of results) {
      if (!map.has(r.tab)) map.set(r.tab, []);
      map.get(r.tab)!.push(r);
    }
    return map;
  }, [results]);

  const goTo = (tab: string) => navigate(`/ws/${wsName}/settings/${tab}`);

  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <h1 className="text-xl flex items-center gap-2">
          <Settings className="h-5 w-5 text-primary" />
          Settings
        </h1>
        <p className="text-sm text-muted-foreground mt-1 max-w-lg">
          Configure AOS behaviour, engine connection, provider keys, and git integration for this workspace.
        </p>
      </div>

      {/* Search bar */}
      <div className="relative">
        <Search
          className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground/40 pointer-events-none"
          aria-hidden="true"
        />
        <input
          ref={inputRef}
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search settings…"
          aria-label="Search settings"
          className={cn(
            "w-full h-11 pl-10 pr-10 rounded-lg border bg-muted/10 text-sm font-mono",
            "placeholder:text-muted-foreground/30 text-foreground",
            "border-border focus:border-primary/50 focus:outline-none focus:ring-1 focus:ring-primary/30",
            "transition-colors"
          )}
        />
        {query && (
          <button
            type="button"
            onClick={() => { setQuery(""); inputRef.current?.focus(); }}
            className="absolute right-3 top-1/2 -translate-y-1/2 h-5 w-5 flex items-center justify-center rounded text-muted-foreground/40 hover:text-muted-foreground transition-colors"
            aria-label="Clear search"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        )}
      </div>

      {/* Results */}
      {query.trim() ? (
        <div>
          {results.length === 0 ? (
            <div className="flex flex-col items-center py-14 gap-3 text-muted-foreground/40">
              <Search className="h-8 w-8" aria-hidden="true" />
              <p className="text-sm">
                No settings match{" "}
                <span className="font-mono text-muted-foreground/60">"{query}"</span>
              </p>
              <button
                type="button"
                onClick={() => setQuery("")}
                className="text-xs text-primary/60 hover:text-primary transition-colors mt-1"
              >
                Clear search
              </button>
            </div>
          ) : (
            <div className="space-y-6">
              <p className="text-[11px] text-muted-foreground/40 uppercase tracking-wider">
                {results.length} result{results.length !== 1 ? "s" : ""} for &ldquo;{query}&rdquo;
              </p>
              {Array.from(grouped.entries()).map(([tab, entries]) => (
                <div key={tab} className="space-y-1.5">
                  <div className="flex items-center gap-2 mb-2">
                    <span
                      className={cn(
                        "inline-flex items-center text-[10px] px-2 py-0.5 rounded border font-mono uppercase tracking-wider",
                        OVERVIEW_TAB_ACCENT[tab] ?? "bg-muted/20 text-muted-foreground border-border/40"
                      )}
                    >
                      {entries[0].tabLabel}
                    </span>
                    <button
                      type="button"
                      onClick={() => goTo(tab)}
                      className="text-[10px] text-muted-foreground/40 hover:text-primary transition-colors"
                    >
                      Open section →
                    </button>
                  </div>
                  {entries.map((entry) => {
                    const Icon = entry.icon;
                    return (
                      <button
                        key={entry.id}
                        type="button"
                        onClick={() => goTo(entry.tab)}
                        className={cn(
                          "w-full flex items-start gap-3 px-3 py-2.5 rounded-md border text-left",
                          "border-border/30 bg-muted/5 hover:bg-muted/20 hover:border-border",
                          "transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                        )}
                        aria-label={`Go to ${entry.label} in ${entry.tabLabel}`}
                      >
                        <Icon className="h-3.5 w-3.5 text-muted-foreground/40 mt-0.5 shrink-0" aria-hidden="true" />
                        <div className="flex-1 min-w-0">
                          <p className="text-xs text-foreground/80">{entry.label}</p>
                          <p className="text-[10px] text-muted-foreground/50 mt-0.5 leading-relaxed truncate">
                            {entry.description}
                          </p>
                        </div>
                        <ChevronRight className="h-3.5 w-3.5 text-muted-foreground/25 mt-0.5 shrink-0" aria-hidden="true" />
                      </button>
                    );
                  })}
                </div>
              ))}
            </div>
          )}
        </div>
      ) : (
        /* Category cards + quick chips (empty query) */
        <div className="space-y-6">

          {/* ── Recently Changed shortcut ── */}
          {analytics.topChanged.length > 0 && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <p className="text-[11px] text-muted-foreground/40 uppercase tracking-wider flex items-center gap-1.5">
                  <RotateCcw className="h-3 w-3" aria-hidden="true" />
                  Recently changed this session
                </p>
                <button
                  type="button"
                  onClick={() => goTo("config")}
                  className="text-[10px] text-primary/50 hover:text-primary transition-colors"
                >
                  Open AOS Config →
                </button>
              </div>
              <div
                className="rounded-lg border border-primary/15 bg-primary/[0.02] divide-y divide-border/30 overflow-hidden"
                role="list"
                aria-label="Recently changed settings"
              >
                {analytics.topChanged.map(([key, count]) => {
                  const entry = config.getEntry(key);
                  return (
                    <button
                      key={key}
                      type="button"
                      role="listitem"
                      onClick={() => goTo("config")}
                      className="w-full flex items-center gap-3 px-3 py-2.5 text-left hover:bg-primary/[0.04] transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-inset"
                      aria-label={`Go to ${entry?.label ?? key} in AOS Config`}
                    >
                      {/* Key + label */}
                      <div className="flex-1 min-w-0">
                        <code className="block text-[11px] font-mono text-primary/60 truncate">
                          {key}
                        </code>
                        {entry && (
                          <p className="text-[10px] text-muted-foreground/50 truncate mt-0.5">
                            {entry.label}
                          </p>
                        )}
                      </div>

                      {/* Change count badge */}
                      <div className="flex items-center gap-2 shrink-0">
                        <span className="inline-flex items-center gap-0.5 text-[10px] font-mono text-primary/70 bg-primary/8 border border-primary/20 px-1.5 py-0.5 rounded">
                          ×{count}
                        </span>
                        <span className="text-[9px] text-muted-foreground/30 uppercase tracking-wider hidden sm:block">
                          AOS Config
                        </span>
                        <ChevronRight className="h-3.5 w-3.5 text-muted-foreground/25 shrink-0" aria-hidden="true" />
                      </div>
                    </button>
                  );
                })}
              </div>
              <p className="text-[10px] text-muted-foreground/30 italic">
                These keys were edited during this session. Changes are pending until you apply them from the AOS Config tab.
              </p>
            </div>
          )}

          <p className="text-[11px] text-muted-foreground/40 uppercase tracking-wider">
            All sections
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {OVERVIEW_CATEGORY_CARDS.map((cat) => {
              const Icon = cat.icon;
              return (
                <button
                  key={cat.id}
                  type="button"
                  onClick={() => goTo(cat.id)}
                  className={cn(
                    "group flex flex-col gap-2 p-4 rounded-lg border text-left transition-colors",
                    cat.accent,
                    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                  )}
                  aria-label={`Open ${cat.label} settings`}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <Icon className={cn("h-4 w-4 shrink-0", cat.iconColor)} aria-hidden="true" />
                      <span className="text-sm text-foreground/80">{cat.label}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] font-mono text-muted-foreground/30 border border-border/30 px-1.5 py-0.5 rounded">
                        {cat.count}
                      </span>
                      <ChevronRight
                        className="h-3.5 w-3.5 text-muted-foreground/25 group-hover:text-muted-foreground/60 transition-colors"
                        aria-hidden="true"
                      />
                    </div>
                  </div>
                  <p className="text-[11px] text-muted-foreground/50 leading-relaxed">
                    {cat.description}
                  </p>
                </button>
              );
            })}
          </div>

          <WorkspaceHealthPanel workspaceId={workspaceId} compact />

          <div className="space-y-2">
            <p className="text-[11px] text-muted-foreground/40 uppercase tracking-wider">
              Quick search
            </p>
            <div className="flex flex-wrap gap-2">
              {OVERVIEW_QUICK_LINKS.map((ql) => (
                <button
                  key={ql.label}
                  type="button"
                  onClick={() => { setQuery(ql.label); inputRef.current?.focus(); }}
                  className="inline-flex items-center gap-1.5 h-7 px-2.5 rounded-md border border-border/40 bg-muted/10 text-[11px] text-muted-foreground/60 hover:text-foreground hover:bg-muted/20 hover:border-border transition-colors"
                >
                  <Search className="h-2.5 w-2.5" aria-hidden="true" />
                  {ql.label}
                </button>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════
// Child route page components
// ═══════════════════════════════════════════════════════════════════════

export function SettingsConfigPage() {
  const { config, expanded, toggleCategory } = useOutletContext<SettingsOutletContext>();
  return <ConfigTab config={config} expanded={expanded} toggleCategory={toggleCategory} />;
}

export function SettingsEnginePage() {
  const { workspaceId, navigate } = useOutletContext<SettingsOutletContext>();
  const conn = useEngineConnection();
  return <EngineTab workspaceId={workspaceId} navigate={navigate} conn={conn} />;
}

export function SettingsProvidersPage() {
  const { config } = useOutletContext<SettingsOutletContext>();

  const providerEntry = config.getEntry("tools.llm_provider");
  const llmProvider = providerEntry?.value ?? "anthropic";
  const isLlmProviderPending = "tools.llm_provider" in config.pendingEdits;

  const modelEntry = config.getEntry("tools.llm_model");
  const llmModel = modelEntry?.value ?? "claude-3-7-sonnet";
  const isLlmModelPending = "tools.llm_model" in config.pendingEdits;

  // ── MCP endpoints — sourced from tools.mcp_endpoints config key ──
  const mcpEntry = config.getEntry("tools.mcp_endpoints");
  const mcpServers = useMemo<McpServer[]>(() => {
    try {
      const parsed = JSON.parse(mcpEntry?.value ?? "[]");
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }, [mcpEntry?.value]);
  const isMcpPending = "tools.mcp_endpoints" in config.pendingEdits;

  const onLlmProviderChange = useCallback(
    (id: string) => {
      if (providerEntry) config.handleChange(providerEntry, id);
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [providerEntry?.key, config.handleChange]
  );

  const onLlmModelChange = useCallback(
    (model: string) => {
      if (modelEntry) config.handleChange(modelEntry, model);
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [modelEntry?.key, config.handleChange]
  );

  const onMcpServersChange = useCallback(
    (servers: McpServer[]) => {
      if (mcpEntry) config.handleChange(mcpEntry, JSON.stringify(servers));
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [mcpEntry?.key, config.handleChange]
  );

  return (
    <ProvidersTab
      llmProvider={llmProvider}
      onLlmProviderChange={onLlmProviderChange}
      isLlmProviderPending={isLlmProviderPending}
      llmModel={llmModel}
      onLlmModelChange={onLlmModelChange}
      isLlmModelPending={isLlmModelPending}
      mcpServers={mcpServers}
      onMcpServersChange={onMcpServersChange}
      isMcpPending={isMcpPending}
    />
  );
}

export function SettingsGitPage() {
  const { workspaceId } = useOutletContext<SettingsOutletContext>();
  return <GitTab workspaceId={workspaceId} />;
}

export function SettingsWorkspacePage() {
  const { workspaceId } = useOutletContext<SettingsOutletContext>();
  return <WorkspaceTab workspaceId={workspaceId} />;
}

export function SettingsOverviewPage() {
  return <SettingsOverview />;
}



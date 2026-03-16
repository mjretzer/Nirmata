import {
  useDiagnostics,
} from "../hooks/useAosData";
import { useState } from "react";
import { Link, useParams } from "react-router";
import {
  ArrowLeft,
  HardDrive,
  AlertCircle,
  CheckCircle,
  FileText,
  Code,
  Download,
  Clock,
  RefreshCw,
  Trash2,
  Unlock,
  Lock,
  FolderOpen,
  ChevronRight,
  Database,
} from "lucide-react";
import { Badge } from "../components/ui/badge";
import { Button } from "../components/ui/button";
import { ScrollArea } from "../components/ui/scroll-area";
import { cn } from "../components/ui/utils";
import { toast } from "sonner";

// ── Sub-components ────────────────────────────────────────────────────

function SectionHeader({
  icon: Icon,
  title,
  description,
  action,
}: {
  icon: React.ElementType;
  title: string;
  description?: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex items-start justify-between gap-4 pb-3 border-b border-border mb-4">
      <div className="flex items-start gap-2">
        <Icon className="h-4 w-4 text-primary mt-0.5 shrink-0" />
        <div>
          <h2 className="text-sm">{title}</h2>
          {description && (
            <p className="text-xs text-muted-foreground mt-0.5">{description}</p>
          )}
        </div>
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

// ── DiagnosticsPage ───────────────────────────────────────────────────

export function DiagnosticsPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { logs: diagLogs, artifacts: diagArtifacts, locks: seedLocks, cacheEntries: seedCache } = useDiagnostics();

  const [expandedLog, setExpandedLog] = useState<string | null>(null);
  const [cacheEntries, setCacheEntries] = useState(seedCache);
  const [locks, setLocks] = useState(seedLocks);

  const totalCacheMB = cacheEntries.reduce((sum, e) => {
    const n = parseFloat(e.size);
    return sum + (isNaN(n) ? 0 : n);
  }, 0);

  const hasStaleCache = cacheEntries.some((e) => e.stale);
  const totalErrors = diagLogs.reduce((s, l) => s + l.errors, 0);
  const totalWarnings = diagLogs.reduce((s, l) => s + l.warnings, 0);

  return (
    <div className="flex flex-col h-full bg-background">
      {/* ── Top bar ───────────────────────────────────────────────── */}
      <div className="flex items-center gap-3 px-6 py-3 border-b border-border bg-muted/10 shrink-0">
        <Link
          to={`/ws/${workspaceId}/settings`}
          className="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          Settings
        </Link>
        <span className="text-muted-foreground/30">/</span>
        <div className="flex items-center gap-2">
          <HardDrive className="h-4 w-4 text-primary" />
          <span className="text-sm">Diagnostics</span>
        </div>

        {/* Summary badges */}
        <div className="ml-auto flex items-center gap-2">
          {totalErrors > 0 && (
            <Badge variant="outline" className="text-[10px] text-red-400 border-red-500/20 bg-red-500/5 gap-1">
              <AlertCircle className="h-3 w-3" />
              {totalErrors} error{totalErrors !== 1 ? "s" : ""}
            </Badge>
          )}
          {totalWarnings > 0 && (
            <Badge variant="outline" className="text-[10px] text-yellow-400 border-yellow-500/20 bg-yellow-500/5 gap-1">
              {totalWarnings} warning{totalWarnings !== 1 ? "s" : ""}
            </Badge>
          )}
          {totalErrors === 0 && totalWarnings === 0 && (
            <Badge variant="outline" className="text-[10px] text-green-400 border-green-500/20 bg-green-500/5 gap-1">
              <CheckCircle className="h-3 w-3" />
              Clean
            </Badge>
          )}
        </div>
      </div>

      <ScrollArea className="flex-1">
        <div className="max-w-4xl mx-auto p-6 space-y-8">

          {/* ── Evidence path ──────────────────────────────────── */}
          <div>
            <SectionHeader
              icon={FolderOpen}
              title="Evidence Path"
              description="Where AOS stores artifacts, logs, and run evidence for the current run."
            />
            <div className="bg-muted/20 border border-border rounded-lg divide-y divide-border/50">
              {[
                { label: "Run evidence", value: ".aos/evidence/run-20260306/" },
                { label: "State files",  value: ".aos/state/" },
                { label: "Spec files",   value: ".aos/spec/" },
                { label: "Cache",        value: ".aos/cache/" },
              ].map((row) => (
                <div key={row.label} className="flex items-center justify-between px-4 py-2.5">
                  <span className="text-xs text-muted-foreground">{row.label}</span>
                  <code className="text-[11px] font-mono text-foreground/60">{row.value}</code>
                </div>
              ))}
            </div>
          </div>

          {/* ── Logs ───────────────────────────────────────────── */}
          <div>
            <SectionHeader
              icon={FileText}
              title="Run Logs"
              description="Logs produced by the most recent run. Click a row to expand."
            />
            <div className="space-y-2">
              {diagLogs.map((log) => {
                const isOpen = expandedLog === log.label;
                return (
                  <div key={log.label} className="border border-border rounded-lg overflow-hidden">
                    <button
                      type="button"
                      onClick={() => setExpandedLog(isOpen ? null : log.label)}
                      className="w-full flex items-center justify-between px-4 py-3 bg-muted/10 hover:bg-muted/20 transition-colors text-left"
                    >
                      <div className="flex items-center gap-3">
                        <FileText className="h-3.5 w-3.5 text-muted-foreground/60" />
                        <span className="text-sm">{log.label}</span>
                        <span className="text-[10px] text-muted-foreground/40 font-mono">
                          {log.lines} lines
                        </span>
                        {log.errors > 0 && (
                          <Badge variant="outline" className="text-[9px] h-4 px-1 text-red-400 border-red-500/20 bg-red-500/5">
                            {log.errors} error{log.errors !== 1 ? "s" : ""}
                          </Badge>
                        )}
                        {log.warnings > 0 && (
                          <Badge variant="outline" className="text-[9px] h-4 px-1 text-yellow-400 border-yellow-500/20 bg-yellow-500/5">
                            {log.warnings} warning{log.warnings !== 1 ? "s" : ""}
                          </Badge>
                        )}
                        {log.errors === 0 && log.warnings === 0 && (
                          <Badge variant="outline" className="text-[9px] h-4 px-1 text-green-400 border-green-500/20 bg-green-500/5">
                            clean
                          </Badge>
                        )}
                      </div>
                      <ChevronRight
                        className={cn(
                          "h-3.5 w-3.5 text-muted-foreground/40 transition-transform duration-150",
                          isOpen && "rotate-90"
                        )}
                      />
                    </button>
                    {isOpen && (
                      <div className="px-4 py-3 border-t border-border bg-muted/5 space-y-2">
                        <code className="text-[10px] font-mono text-muted-foreground/50">
                          {log.path}
                        </code>
                        {/* Simulated log preview */}
                        <div className="font-mono text-[10px] text-foreground/50 space-y-0.5 max-h-32 overflow-hidden">
                          <p>[INFO]  Starting {log.label.toLowerCase().replace(" log", "")} phase…</p>
                          {log.warnings > 0 && <p className="text-yellow-400/70">[WARN]  {log.warnings} warning{log.warnings !== 1 ? "s" : ""} found</p>}
                          {log.errors > 0 && <p className="text-red-400/70">[ERROR] {log.errors} error{log.errors !== 1 ? "s" : ""} found</p>}
                          <p>[INFO]  Completed in 4.2s</p>
                        </div>
                        <Button
                          variant="outline"
                          size="sm"
                          className="h-7 text-xs gap-1.5"
                          onClick={() => toast.success(`Opening ${log.path}`)}
                        >
                          <Code className="h-3 w-3" />
                          View full log
                        </Button>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          {/* ── Artifacts ──────────────────────────────────────── */}
          <div>
            <SectionHeader
              icon={Database}
              title="Attached Artifacts"
              description={`${diagArtifacts.length} files attached to the current run.`}
            />
            <div className="bg-card border border-border rounded-lg divide-y divide-border/50">
              {diagArtifacts.map((a) => (
                <div key={a.name} className="flex items-center justify-between px-4 py-2.5">
                  <div className="flex items-center gap-3 min-w-0">
                    <FileText className="h-3.5 w-3.5 text-muted-foreground/50 shrink-0" />
                    <div className="min-w-0">
                      <p className="text-xs truncate">{a.name}</p>
                      <code className="text-[10px] text-muted-foreground/40 font-mono truncate block">
                        {a.path}
                      </code>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 shrink-0">
                    <span className="text-[10px] text-muted-foreground/50 font-mono">{a.size}</span>
                    <Badge variant="outline" className="text-[9px] h-4 px-1.5 font-mono uppercase text-muted-foreground/40 border-border/30">
                      {a.type}
                    </Badge>
                    <button
                      type="button"
                      aria-label={`Download ${a.name}`}
                      onClick={() => toast.success(`Downloading ${a.name}`)}
                      className="text-muted-foreground/40 hover:text-muted-foreground transition-colors"
                    >
                      <Download className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* ── Last error ──────────────────────────────────────── */}
          <div>
            <SectionHeader
              icon={AlertCircle}
              title="Last Error"
              description="Most recent unrecovered error in this workspace."
            />
            <div className="flex items-center gap-3 p-4 border border-border rounded-lg bg-red-500/5 border-red-500/20">
              <AlertCircle className="h-4 w-4 text-red-400 shrink-0" />
              <div className="flex-1 min-w-0">
                <p className="text-xs text-red-300">Lint check failed: 2 errors in src/auth.ts</p>
                <div className="flex items-center gap-2 mt-1">
                  <Clock className="h-3 w-3 text-muted-foreground/40" />
                  <span className="text-[10px] text-muted-foreground/50">Today 10:18:44</span>
                  <span className="text-[10px] text-muted-foreground/40">·</span>
                  <span className="text-[10px] text-muted-foreground/50 font-mono">run-044</span>
                </div>
              </div>
              <Button
                variant="outline"
                size="sm"
                className="h-7 text-xs gap-1.5 shrink-0"
                onClick={() => toast.info("Opening run-044 detail")}
              >
                View run
              </Button>
            </div>
          </div>

          {/* ── Cache ───────────────────────────────────────────── */}
          <div>
            <SectionHeader
              icon={HardDrive}
              title="Cache"
              description="Temporary files used to speed up runs. Clearing is safe — AOS will regenerate what it needs."
              action={
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-7 text-xs gap-1.5"
                    onClick={() => {
                      setCacheEntries((prev) => prev.map((e) => ({ ...e, stale: false })));
                      toast.success("Expired cache entries pruned");
                    }}
                    disabled={!hasStaleCache}
                  >
                    <RefreshCw className="h-3 w-3" />
                    Prune expired
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-7 text-xs gap-1.5 text-destructive border-destructive/20 hover:bg-destructive/10"
                    onClick={() => {
                      setCacheEntries((prev) =>
                        prev.map((e) => ({ ...e, size: "0 MB", stale: false }))
                      );
                      toast.success("Cache cleared");
                    }}
                  >
                    <Trash2 className="h-3 w-3" />
                    Clear all
                  </Button>
                </div>
              }
            />
            <div className="bg-card border border-border rounded-lg divide-y divide-border/50">
              {cacheEntries.map((c) => (
                <div key={c.label} className="flex items-center justify-between px-4 py-2.5">
                  <div className="flex items-center gap-3">
                    <HardDrive className="h-3.5 w-3.5 text-muted-foreground/50" />
                    <div>
                      <p className="text-xs">{c.label}</p>
                      <code className="text-[10px] text-muted-foreground/40 font-mono">{c.path}</code>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-[10px] text-muted-foreground/50 font-mono">{c.size}</span>
                    {c.stale ? (
                      <Badge variant="outline" className="text-[9px] h-4 px-1 text-yellow-400 border-yellow-500/20 bg-yellow-500/5">
                        stale
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="text-[9px] h-4 px-1 text-muted-foreground/30 border-border/20">
                        ok
                      </Badge>
                    )}
                  </div>
                </div>
              ))}
              <div className="flex items-center justify-between px-4 py-2 bg-muted/5">
                <span className="text-[10px] text-muted-foreground">Total cache size</span>
                <span className="text-[10px] font-mono text-foreground/60">
                  {totalCacheMB.toFixed(0)} MB
                </span>
              </div>
            </div>
          </div>

          {/* ── Locks ───────────────────────────────────────────── */}
          <div>
            <SectionHeader
              icon={Lock}
              title="Workspace Locks"
              description="Active locks prevent concurrent runs from corrupting shared state."
              action={
                locks.length > 0 ? (
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-7 text-xs gap-1.5 text-destructive border-destructive/20 hover:bg-destructive/10"
                    onClick={() => {
                      setLocks([]);
                      toast.success("All locks released");
                    }}
                  >
                    <Unlock className="h-3 w-3" />
                    Release all
                  </Button>
                ) : undefined
              }
            />
            {locks.length === 0 ? (
              <div className="flex items-center gap-3 px-4 py-4 border border-green-500/20 bg-green-500/5 rounded-lg">
                <CheckCircle className="h-4 w-4 text-green-400 shrink-0" />
                <div>
                  <p className="text-xs text-green-300">No active locks</p>
                  <p className="text-[10px] text-muted-foreground mt-0.5">
                    Safe to run tasks. All resources are available.
                  </p>
                </div>
              </div>
            ) : (
              <div className="bg-card border border-border rounded-lg divide-y divide-border/50">
                {locks.map((lock) => (
                  <div key={lock.id} className="flex items-center justify-between px-4 py-3">
                    <div className="flex items-center gap-3">
                      <Lock className="h-3.5 w-3.5 text-yellow-400 shrink-0" />
                      <div>
                        <p className="text-xs">{lock.scope}</p>
                        <p className="text-[10px] text-muted-foreground/50">
                          Held by <span className="font-mono">{lock.owner}</span> since {lock.acquired}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {lock.stale && (
                        <Badge variant="outline" className="text-[9px] h-4 px-1 text-yellow-400 border-yellow-500/20">
                          stale
                        </Badge>
                      )}
                      <Button
                        variant="outline"
                        size="sm"
                        className="h-7 text-xs gap-1"
                        onClick={() => {
                          setLocks((prev) => prev.filter((l) => l.id !== lock.id));
                          toast.success(`Lock ${lock.id} released`);
                        }}
                      >
                        <Unlock className="h-3 w-3" />
                        Release
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* ── Export bundle ───────────────────────────────────── */}
          <div className="flex items-start gap-4 p-4 border border-border rounded-lg bg-muted/10">
            <div className="h-9 w-9 rounded-lg bg-muted/30 flex items-center justify-center shrink-0">
              <Download className="h-4 w-4 text-muted-foreground" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm">Export Diagnostics Bundle</p>
              <p className="text-xs text-muted-foreground mt-0.5">
                Packages all logs, artifacts, cache stats, lock state, and engine version info
                into a zip for bug reports or support escalation.
              </p>
            </div>
            <Button
              variant="outline"
              size="sm"
              className="gap-1.5 shrink-0"
              onClick={() => toast.success("Diagnostics bundle downloaded")}
            >
              <Download className="h-3 w-3" />
              Download
            </Button>
          </div>

        </div>
      </ScrollArea>
    </div>
  );
}
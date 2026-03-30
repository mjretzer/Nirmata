import { useState, useRef, useEffect, useCallback, useMemo } from "react";
import {
  Send,
  Terminal,
  Bot,
  User,
  Cpu,
  ChevronDown,
  ChevronRight,
  FileText,
  CheckCircle2,
  XCircle,
  X,
  Loader2,
  Copy,
  RotateCcw,
  Zap,
  Clock,
  AlertCircle,
  Command,
  ArrowDown,
  MessageSquare,
  Wand2,
} from "lucide-react";
import { toast } from "sonner";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { ScrollArea } from "../components/ui/scroll-area";
import {
  Drawer,
  DrawerClose,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
  DrawerDescription,
  DrawerTrigger,
} from "../components/ui/drawer";
import { cn } from "../components/ui/utils";
import { CommandChip } from "../components/CommandChip";
import { copyToClipboard } from "../utils/clipboard";
import { relativeTime } from "../utils/format";
import { useWorkspaceContext } from "../context/WorkspaceContext";
import {
  useChatMessages,
  useWorkspace,
  type ChatMessage,
  type AgentId,
  type GateState,
  type CommandSuggestion,
  type QuickAction,
} from "../hooks/useAosData";

// ── Agent display config ──────────────────────────────────────

const AGENT_CONFIG: Record<AgentId, { label: string; color: string; bgColor: string }> = {
  orchestrator: { label: "Orchestrator", color: "text-primary", bgColor: "bg-primary/10" },
  interviewer:  { label: "Interviewer",  color: "text-blue-400", bgColor: "bg-blue-500/10" },
  roadmapper:   { label: "Roadmapper",   color: "text-purple-400", bgColor: "bg-purple-500/10" },
  planner:      { label: "Planner",      color: "text-cyan-400", bgColor: "bg-cyan-500/10" },
  executor:     { label: "Executor",     color: "text-amber-400", bgColor: "bg-amber-500/10" },
  verifier:     { label: "Verifier",     color: "text-emerald-400", bgColor: "bg-emerald-500/10" },
};

// ── Pipeline step sequence ────────────────────────────────────
// Ordered list of the real orchestrator agent steps (matches GateState values).
const PIPELINE_STEPS = [
  "interviewer",
  "roadmapper",
  "planner",
  "executor",
  "verifier",
] as const;

type PipelineStep = (typeof PIPELINE_STEPS)[number];

/**
 * Maps every non-idle GateState to its position in PIPELINE_STEPS.
 * fix-loop is a sub-state of verifier (re-runs verification) so it
 * lights up the verifier dot rather than an off-the-end position.
 */
const GATE_TO_PIPELINE_STEP: Partial<Record<GateState, PipelineStep>> = {
  interviewer: "interviewer",
  roadmapper:  "roadmapper",
  planner:     "planner",
  executor:    "executor",
  verifier:    "verifier",
  "fix-loop":  "verifier",
};

// ── Simple markdown renderer ──────────────────────────────────

function renderMarkdown(text: string): React.ReactNode[] {
  const lines = text.split("\n");
  const result: React.ReactNode[] = [];

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // Bold text replacement
    const renderInline = (str: string): React.ReactNode => {
      const parts: React.ReactNode[] = [];
      let remaining = str;
      let key = 0;

      // Handle **bold** and `code`
      while (remaining.length > 0) {
        // Bold
        const boldMatch = remaining.match(/\*\*(.+?)\*\*/);
        // Code
        const codeMatch = remaining.match(/`([^`]+)`/);

        // Find earliest match
        const boldIdx = boldMatch ? remaining.indexOf(boldMatch[0]) : Infinity;
        const codeIdx = codeMatch ? remaining.indexOf(codeMatch[0]) : Infinity;

        if (boldIdx === Infinity && codeIdx === Infinity) {
          parts.push(remaining);
          break;
        }

        if (boldIdx <= codeIdx && boldMatch) {
          if (boldIdx > 0) parts.push(remaining.slice(0, boldIdx));
          parts.push(
            <span key={`b-${key}`} className="font-semibold text-foreground">
              {boldMatch[1]}
            </span>
          );
          remaining = remaining.slice(boldIdx + boldMatch[0].length);
        } else if (codeMatch) {
          if (codeIdx > 0) parts.push(remaining.slice(0, codeIdx));
          parts.push(
            <code key={`c-${key}`} className="px-1 py-0.5 rounded bg-muted border border-border/50 text-[11px] font-mono text-primary">
              {codeMatch[1]}
            </code>
          );
          remaining = remaining.slice(codeIdx + codeMatch[0].length);
        }
        key++;
      }

      return <>{parts}</>;
    };

    // List items
    if (line.startsWith("- ")) {
      result.push(
        <div key={i} className="flex gap-2 pl-2 py-0.5">
          <span className="text-muted-foreground shrink-0 mt-0.5">-</span>
          <span>{renderInline(line.slice(2))}</span>
        </div>
      );
    }
    // Numbered list
    else if (/^\d+\.\s/.test(line)) {
      const match = line.match(/^(\d+)\.\s(.*)/);
      if (match) {
        result.push(
          <div key={i} className="flex gap-2 pl-2 py-0.5">
            <span className="text-muted-foreground shrink-0 font-mono text-[11px] mt-0.5 w-4 text-right">{match[1]}.</span>
            <span>{renderInline(match[2])}</span>
          </div>
        );
      }
    }
    // Empty line → spacer
    else if (line.trim() === "") {
      result.push(<div key={i} className="h-2" />);
    }
    // Regular line
    else {
      result.push(
        <div key={i} className="py-0.5">
          {renderInline(line)}
        </div>
      );
    }
  }

  return result;
}

// ── Timeline Step Row ─────────────────────────────────────────

function TimelineStepRow({ step }: { step: { id: string; label: string; status: string } }) {
  const statusIcon = {
    completed: <CheckCircle2 className="h-3 w-3 text-emerald-500" />,
    running:   <Loader2 className="h-3 w-3 text-blue-400 animate-spin" />,
    failed:    <XCircle className="h-3 w-3 text-red-500" />,
    pending:   <div className="h-3 w-3 rounded-full border border-border/60" />,
  }[step.status] ?? <div className="h-3 w-3 rounded-full border border-border/60" />;

  return (
    <div className="flex items-center gap-2 text-[11px] font-mono">
      {statusIcon}
      <span className={cn(
        step.status === "completed" ? "text-emerald-400" :
        step.status === "running" ? "text-blue-400" :
        step.status === "failed" ? "text-red-400" :
        "text-muted-foreground"
      )}>
        {step.label}
      </span>
    </div>
  );
}

// ── Artifact Reference ────────────────────────────────────────

function ArtifactRefChip({ artifact }: { artifact: { path: string; label: string; action: string } }) {
  const actionColors = {
    created:    "text-emerald-400 bg-emerald-500/10 border-emerald-500/20",
    updated:    "text-blue-400 bg-blue-500/10 border-blue-500/20",
    deleted:    "text-red-400 bg-red-500/10 border-red-500/20",
    referenced: "text-muted-foreground bg-muted/50 border-border/50",
  };

  return (
    <button
      className={cn(
        "inline-flex items-center gap-1.5 px-2 py-1 rounded border text-[10px] font-mono transition-colors hover:brightness-110",
        actionColors[artifact.action as keyof typeof actionColors] ?? actionColors.referenced
      )}
      onClick={() => toast.info(`Opening ${artifact.path}`)}
      title={artifact.path}
    >
      <FileText className="h-3 w-3 shrink-0" />
      <span className="truncate max-w-[180px]">{artifact.label}</span>
      <Badge variant="outline" className="text-[8px] h-4 px-1 uppercase font-mono shrink-0 text-muted-foreground">
        {artifact.action}
      </Badge>
    </button>
  );
}

// ── Log Expander ──────────────────────────────────────────────

function LogBlock({ logs }: { logs: string[] }) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="mt-2">
      <button
        className="flex items-center gap-1.5 text-[10px] font-mono text-muted-foreground hover:text-foreground transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        {expanded ? <ChevronDown className="h-3 w-3" /> : <ChevronRight className="h-3 w-3" />}
        {logs.length} log line{logs.length !== 1 ? "s" : ""}
      </button>
      {expanded && (
        <div className="mt-1.5 bg-background/50 border border-border/40 rounded p-2 space-y-0.5 max-h-48 overflow-y-auto">
          {logs.map((line, i) => (
            <div key={i} className="text-[10px] font-mono text-muted-foreground leading-relaxed">
              <span className="text-muted-foreground/40 select-none mr-2">{String(i + 1).padStart(2, " ")}</span>
              {line}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Agent Badge ───────────────────────────────────────────────

function AgentBadge({ agent }: { agent: AgentId }) {
  const cfg = AGENT_CONFIG[agent];
  return (
    <Badge variant="outline" className={cn("text-[9px] h-4 px-1.5 gap-1 uppercase font-mono border-current/20", cfg.color, cfg.bgColor)}>
      <Cpu className="h-2.5 w-2.5" />
      {cfg.label}
    </Badge>
  );
}

// ── Message Bubble ────────────────────────────────────────────

function MessageBubble({
  message,
  onCopy,
  onSubmitCommand,
}: {
  message: ChatMessage;
  onCopy: (text: string) => void;
  onSubmitCommand: (cmd: string) => void;
}) {
  const isUser = message.role === "user";
  const isSystem = message.role === "system";
  const isResult = message.role === "result";

  return (
    <div className={cn(
      "group flex gap-3 max-w-full",
      isUser ? "flex-row-reverse" : "flex-row"
    )}>
      {/* Avatar */}
      <div className={cn(
        "shrink-0 h-7 w-7 rounded-full flex items-center justify-center mt-0.5",
        isUser ? "bg-primary/15 text-primary" :
        isSystem ? "bg-muted text-muted-foreground" :
        isResult ? "bg-emerald-500/15 text-emerald-400" :
        "bg-muted text-primary"
      )}>
        {isUser ? <User className="h-3.5 w-3.5" /> :
         isSystem ? <AlertCircle className="h-3.5 w-3.5" /> :
         isResult ? <Zap className="h-3.5 w-3.5" /> :
         <Bot className="h-3.5 w-3.5" />}
      </div>

      {/* Content */}
      <div className={cn(
        "flex flex-col gap-1 min-w-0",
        isUser ? "items-end max-w-[75%]" : "items-start max-w-[85%]"
      )}>
        {/* Header: agent badge + timestamp */}
        <div className={cn(
          "flex items-center gap-2 text-[10px] text-muted-foreground/60",
          isUser ? "flex-row-reverse" : "flex-row"
        )}>
          {message.agent && <AgentBadge agent={message.agent} />}
          <span className="font-mono">
            {relativeTime(message.timestamp.toISOString())}
          </span>
          {message.runId && (
            <Badge variant="outline" className="text-[9px] h-4 px-1.5 font-mono text-amber-400 bg-amber-500/5 border-amber-500/20">
              {message.runId}
            </Badge>
          )}
        </div>

        {/* Command chip (for user messages with commands) */}
        {message.command && (
          <div className="w-full max-w-sm">
            <CommandChip
              command={message.command}
              onExecute={() => toast.info(`Would execute: ${message.command}`)}
            />
          </div>
        )}

        {/* Message body */}
        {(!message.command || message.role !== "user") && (
          <div className={cn(
            "rounded-lg px-3.5 py-2.5 text-[13px] leading-relaxed relative",
            isUser
              ? "bg-primary/10 border border-primary/20 text-foreground"
              : isSystem
                ? "bg-muted/30 border border-border/40 text-muted-foreground italic"
                : isResult
                  ? "bg-emerald-500/5 border border-emerald-500/20 text-foreground"
                  : "bg-card border border-border/60 text-foreground"
          )}>
            {/* Copy button */}
            <button
              className="absolute top-1.5 right-1.5 opacity-0 group-hover:opacity-100 transition-opacity p-1 rounded hover:bg-muted/50"
              onClick={() => onCopy(message.content)}
              title="Copy message"
            >
              <Copy className="h-3 w-3 text-muted-foreground" />
            </button>

            <div className="pr-6 space-y-0">
              {renderMarkdown(message.content)}
            </div>

            {/* Streaming indicator */}
            {message.streaming && (
              <div className="flex items-center gap-1.5 mt-2 text-[10px] text-muted-foreground">
                <Loader2 className="h-3 w-3 animate-spin" />
                <span>Thinking...</span>
              </div>
            )}
          </div>
        )}

        {/* Timeline (for result messages) */}
        {message.timeline && message.timeline.length > 0 && (
          <div className="flex items-center gap-3 px-2 py-1.5 bg-muted/20 border border-border/30 rounded-md">
            {message.timeline.map((step, i) => (
              <div key={step.id} className="flex items-center gap-1">
                <TimelineStepRow step={step} />
                {i < (message.timeline?.length ?? 0) - 1 && (
                  <ChevronRight className="h-3 w-3 text-border ml-1" />
                )}
              </div>
            ))}
          </div>
        )}

        {/* Artifacts */}
        {message.artifacts && message.artifacts.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mt-0.5">
            {message.artifacts.map((a, i) => (
              <ArtifactRefChip key={i} artifact={a} />
            ))}
          </div>
        )}

        {/* Logs */}
        {message.logs && message.logs.length > 0 && (
          <div className="w-full">
            <LogBlock logs={message.logs} />
          </div>
        )}

        {/* Next recommended command */}
        {message.nextCommand && !isUser && (
          <div className="w-full max-w-sm mt-1">
            <CommandChip
              command={message.nextCommand}
              onExecute={() => onSubmitCommand(message.nextCommand!)}
            />
          </div>
        )}
      </div>
    </div>
  );
}

// ── Command Suggestions Dropdown ──────────────────────────────

function CommandSuggestionsPanel({
  suggestions,
  filter,
  onSelect,
  visible,
}: {
  suggestions: CommandSuggestion[];
  filter: string;
  onSelect: (cmd: string) => void;
  visible: boolean;
}) {
  const filtered = useMemo(() => {
    if (!filter) return suggestions.slice(0, 8);
    const q = filter.toLowerCase();
    return suggestions.filter(
      (s) =>
        s.command.toLowerCase().includes(q) ||
        s.description.toLowerCase().includes(q)
    ).slice(0, 8);
  }, [filter, suggestions]);

  if (!visible || filtered.length === 0) return null;

  return (
    <div className="absolute bottom-full left-0 right-0 mb-1 bg-card border border-border/60 rounded-lg shadow-xl overflow-hidden z-50">
      <div className="px-3 py-1.5 border-b border-border/40 text-[10px] text-muted-foreground uppercase tracking-wider font-medium flex items-center gap-1.5">
        <Command className="h-3 w-3" />
        Commands
      </div>
      <div className="max-h-64 overflow-y-auto">
        {filtered.map((s) => (
          <button
            key={s.command}
            className="w-full flex items-center gap-3 px-3 py-2 hover:bg-accent/50 transition-colors text-left"
            onClick={() => onSelect(s.command)}
          >
            <Terminal className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
            <div className="flex-1 min-w-0">
              <div className="font-mono text-xs text-foreground truncate">{s.command}</div>
              <div className="text-[10px] text-muted-foreground truncate">{s.description}</div>
            </div>
            <Badge variant="outline" className="text-[8px] h-4 px-1 uppercase font-mono shrink-0 text-muted-foreground">
              {s.group}
            </Badge>
          </button>
        ))}
      </div>
    </div>
  );
}

// ── Quick Action Bar ──────────────────────────────────────────

function QuickActionBar({
  actions,
  onAction,
}: {
  actions: QuickAction[];
  onAction: (cmd: string) => void;
}) {
  return (
    <div className="flex items-center gap-1.5">
      {actions.map((a) => (
        <Button
          key={a.command}
          variant={a.variant === "primary" ? "default" : "outline"}
          size="sm"
          className={cn(
            "h-6 px-2 text-[10px] font-mono gap-1",
            a.variant === "primary" && "bg-primary/15 text-primary border border-primary/30 hover:bg-primary/25"
          )}
          onClick={() => onAction(a.command)}
        >
          {a.variant === "primary" && <Zap className="h-3 w-3" />}
          {a.label}
        </Button>
      ))}
    </div>
  );
}

// ── History Turn Row ──────────────────────────────────────────

function HistoryTurnRow({ message }: { message: ChatMessage }) {
  const isUser = message.role === "user";
  const isResult = message.role === "result";
  const isSystem = message.role === "system";

  const borderColor = isUser
    ? "border-l-primary/50"
    : isResult
    ? "border-l-emerald-500/50"
    : isSystem
    ? "border-l-border/40"
    : "border-l-border/60";

  const roleColor = isUser
    ? "text-primary"
    : isResult
    ? "text-emerald-400"
    : isSystem
    ? "text-muted-foreground"
    : "text-foreground";

  return (
    <div className={cn("border-l-2 pl-2.5 py-1.5 space-y-1", borderColor)}>
      {/* timestamp + role + agent */}
      <div className="flex items-center gap-1.5 flex-wrap">
        <span className={cn("text-[10px] font-mono font-medium capitalize", roleColor)}>
          {message.role}
        </span>
        {message.agent && (
          <Badge
            variant="outline"
            className={cn(
              "text-[8px] h-3.5 px-1 gap-0.5 uppercase font-mono border-current/20",
              AGENT_CONFIG[message.agent].color,
              AGENT_CONFIG[message.agent].bgColor
            )}
          >
            <Cpu className="h-2 w-2" />
            {AGENT_CONFIG[message.agent].label}
          </Badge>
        )}
        <span className="text-[9px] font-mono text-muted-foreground/50 ml-auto">
          {relativeTime(message.timestamp.toISOString())}
        </span>
      </div>
      {/* gate + runId */}
      {(message.gate && message.gate !== "idle" || message.runId) && (
        <div className="flex items-center gap-1 flex-wrap">
          {message.gate && message.gate !== "idle" && (
            <Badge
              variant="outline"
              className="text-[8px] h-3.5 px-1 font-mono uppercase text-muted-foreground border-border/40"
            >
              {message.gate}
            </Badge>
          )}
          {message.runId && (
            <Badge
              variant="outline"
              className="text-[8px] h-3.5 px-1 font-mono text-amber-400 bg-amber-500/5 border-amber-500/20"
            >
              {message.runId}
            </Badge>
          )}
        </div>
      )}
      {/* next command */}
      {message.nextCommand && (
        <div className="font-mono text-[9px] text-primary/70 bg-primary/5 border border-primary/15 rounded px-1.5 py-0.5 truncate">
          → {message.nextCommand}
        </div>
      )}
    </div>
  );
}

// ── Main ChatPage ─────────────────────────────────────────────

export function ChatPage() {
  const { activeWorkspaceId, engineStatus } = useWorkspaceContext();
  const { workspace } = useWorkspace(activeWorkspaceId);
  const {
    messages,
    commandSuggestions,
    quickActions,
    isLoading: isChatLoading,
    isSubmitting,
    submitTurn,
    refreshSnapshot,
  } = useChatMessages(activeWorkspaceId);

  const [inputValue, setInputValue] = useState("");
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [showScrollButton, setShowScrollButton] = useState(false);
  const [chatMode, setChatMode] = useState<"chat" | "command" | "auto">("chat");

  // Derive the active pipeline step from the most recent message that carries
  // a non-idle gate field.  This is the single source of truth — no separate
  // orchestratorStep state needed.
  const activeGateStep = useMemo((): PipelineStep | null => {
    for (let i = messages.length - 1; i >= 0; i--) {
      const gate = messages[i].gate;
      if (gate && gate !== "idle") {
        return GATE_TO_PIPELINE_STEP[gate] ?? null;
      }
    }
    return null;
  }, [messages]);

  const scrollRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const savedScrollPos = useRef<number>(0);

  // Scroll to bottom on new messages
  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, scrollToBottom]);

  // Handle scroll position for "scroll to bottom" button
  const handleScroll = useCallback((e: React.UIEvent<HTMLDivElement>) => {
    const target = e.currentTarget;
    const isNearBottom = target.scrollHeight - target.scrollTop - target.clientHeight < 100;
    setShowScrollButton(!isNearBottom);
  }, []);

  // Detect if input is a command (explicit prefix or command mode active)
  const isCommand = inputValue.trim().startsWith("aos ") || chatMode === "command";

  // Send message
  const handleSend = useCallback(() => {
    const content = inputValue.trim();
    if (!content || isSubmitting) return;

    setInputValue("");
    setShowSuggestions(false);
    void submitTurn(content);
  }, [inputValue, isSubmitting, submitTurn]);

  // Handle keyboard events
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
      if (e.key === "Escape") {
        setShowSuggestions(false);
      }
    },
    [handleSend]
  );

  // Handle input change
  const handleInputChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const val = e.target.value;
    setInputValue(val);
    // In command mode, always show suggestions; otherwise trigger on "aos" / "/"
    setShowSuggestions(chatMode === "command" || val.startsWith("aos") || val.startsWith("/"));
  }, [chatMode]);

  // Select a command suggestion
  const handleSelectCommand = useCallback((cmd: string) => {
    setInputValue(cmd);
    setShowSuggestions(false);
    inputRef.current?.focus();
  }, []);

  // Quick action handler
  const handleQuickAction = useCallback((cmd: string) => {
    setInputValue("");
    setShowSuggestions(false);
    void submitTurn(cmd);
  }, [submitTurn]);

  // Copy message handler
  const handleCopyMessage = useCallback(async (text: string) => {
    const success = await copyToClipboard(text);
    if (success) toast.success("Copied to clipboard");
    else toast.error("Failed to copy");
  }, []);

  return (
    <div className="flex-1 flex flex-col h-full bg-background overflow-hidden">
      {/* ── Sub-toolbar ── */}
      <div className="shrink-0 border-b border-border/50 px-6 py-2.5 flex items-center gap-4">
        {/* Page title + engine cursor */}
        <div className="flex items-center gap-3">
          <span className="text-sm font-semibold text-foreground">Chat</span>
          <span className="text-[10px] font-mono text-muted-foreground/60">
            {workspace.cursor.phase}/{workspace.cursor.task || "—"}
          </span>
        </div>

        <div className="h-4 w-px bg-border/40" />

        {/* Orchestrator Pipeline Stepper */}
        <div className="flex items-center gap-0">
          {PIPELINE_STEPS.map((step, i) => {
            const activeIndex = activeGateStep ? PIPELINE_STEPS.indexOf(activeGateStep) : -1;
            const isActive    = step === activeGateStep;
            const isCompleted = activeIndex >= 0 && i < activeIndex;
            const isPending   = !isActive && !isCompleted;

            return (
              <div key={step} className="flex items-center">
                <div className="flex flex-col items-center gap-0.5">
                  <div
                    className={cn(
                      "h-2 w-2 rounded-full transition-all duration-300",
                      isActive
                        ? "bg-primary shadow-[0_0_6px_rgba(var(--primary),0.6)] scale-125"
                        : isCompleted
                          ? "bg-primary/70"
                          : "bg-muted-foreground/20 border border-border/40"
                    )}
                  />
                  <span
                    className={cn(
                      "text-[8px] font-mono tracking-wide leading-none select-none",
                      isActive
                        ? "text-primary"
                        : isCompleted
                          ? "text-primary/50"
                          : "text-muted-foreground/30"
                    )}
                  >
                    {step}
                  </span>
                </div>
                {i < PIPELINE_STEPS.length - 1 && (
                  <div
                    className={cn(
                      "h-px w-4 mx-0.5 mt-[-8px] transition-colors duration-300",
                      isCompleted ? "bg-primary/50" : "bg-border/30"
                    )}
                  />
                )}
              </div>
            );
          })}
        </div>

        <Badge variant="outline" className={cn(
          "text-[9px] h-4 px-1.5 uppercase font-mono",
          engineStatus === "idle" ? "text-muted-foreground" :
          engineStatus === "running" ? "text-green-400 bg-green-500/10 border-green-500/20" :
          engineStatus === "waiting" ? "text-blue-400 bg-blue-500/10 border-blue-500/20" :
          "text-yellow-400 bg-yellow-500/10 border-yellow-500/20"
        )}>
          {engineStatus}
        </Badge>

        <div className="flex-1" />

        <div className="flex items-center gap-2">
          <Drawer
            direction="right"
            onOpenChange={(open) => {
              const viewport = scrollRef.current?.closest('[data-slot="scroll-area-viewport"]') as HTMLElement | null;
              if (open) {
                savedScrollPos.current = viewport?.scrollTop ?? 0;
              } else {
                if (viewport) viewport.scrollTop = savedScrollPos.current;
                setTimeout(() => inputRef.current?.focus(), 0);
              }
            }}
          >
            <DrawerTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                className="h-7 text-[10px] font-mono gap-1.5"
              >
                <Clock className="h-3 w-3" />
                History
              </Button>
            </DrawerTrigger>
            <DrawerContent className="w-[380px] sm:max-w-[380px] flex flex-col">
              <DrawerHeader className="border-b border-border/50 pb-3 flex-row flex items-start justify-between">
                <div>
                  <DrawerTitle className="text-sm font-mono">Thread History</DrawerTitle>
                  <DrawerDescription className="text-[11px] font-mono text-muted-foreground/70">
                    Workspace chat thread — read-only
                  </DrawerDescription>
                </div>
                <DrawerClose asChild>
                  <Button variant="ghost" size="icon" className="h-6 w-6 shrink-0 -mt-1 -mr-1">
                    <X className="h-3.5 w-3.5" />
                    <span className="sr-only">Close history</span>
                  </Button>
                </DrawerClose>
              </DrawerHeader>
              <ScrollArea className="flex-1">
                {isChatLoading ? (
                  <div className="flex items-center justify-center h-24">
                    <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                  </div>
                ) : messages.length === 0 ? (
                  <div className="flex flex-col items-center justify-center h-24 gap-1.5 text-muted-foreground">
                    <MessageSquare className="h-4 w-4" />
                    <span className="text-[11px] font-mono">No messages yet</span>
                  </div>
                ) : (
                  <div className="p-3 space-y-2">
                    {messages.map((msg) => (
                      <HistoryTurnRow key={msg.id} message={msg} />
                    ))}
                  </div>
                )}
              </ScrollArea>
              <div className="shrink-0 border-t border-border/50 p-3 flex justify-end">
                <DrawerClose asChild>
                  <Button variant="outline" size="sm" className="h-7 text-[10px] font-mono">
                    Back to chat
                  </Button>
                </DrawerClose>
              </div>
            </DrawerContent>
          </Drawer>
          <Button
            variant="outline"
            size="sm"
            className="h-7 text-[10px] font-mono gap-1.5"
            onClick={() => {
              refreshSnapshot();
              toast.success("Chat reloaded");
            }}
          >
            <RotateCcw className="h-3 w-3" />
            Reset
          </Button>
        </div>
      </div>

      {/* Messages Area */}
      <div className="flex-1 overflow-hidden relative">
        <ScrollArea className="h-full">
          <div
            ref={scrollRef}
            className="p-4 space-y-4"
            onScroll={handleScroll}
          >
            {/* Welcome message */}
            {messages.length === 0 && (
              <div className="flex flex-col items-center justify-center py-16 text-center">
                <div className="h-16 w-16 rounded-2xl bg-primary/10 border border-primary/20 flex items-center justify-center mb-4">
                  <Bot className="h-8 w-8 text-primary" />
                </div>
                <h2 className="text-lg font-semibold mb-1">AOS Orchestrator</h2>
                <p className="text-sm text-muted-foreground max-w-md font-mono">
                  Ask questions about your workspace, run commands, or start a task execution.
                  Type <code className="px-1 py-0.5 rounded bg-muted border border-border/50 text-[11px] text-primary">aos</code> to see available commands.
                </p>
              </div>
            )}

            {/* Message list */}
            {messages.map((msg) => (
              <MessageBubble
                key={msg.id}
                message={msg}
                onCopy={handleCopyMessage}
                onSubmitCommand={handleQuickAction}
              />
            ))}

            <div ref={messagesEndRef} />
          </div>
        </ScrollArea>

        {/* Scroll to bottom button */}
        {showScrollButton && (
          <button
            className="absolute bottom-4 right-4 h-8 w-8 rounded-full bg-card border border-border/60 shadow-lg flex items-center justify-center hover:bg-accent transition-colors z-10"
            onClick={scrollToBottom}
          >
            <ArrowDown className="h-4 w-4 text-muted-foreground" />
          </button>
        )}
      </div>

      {/* Input Area */}
      <div className="shrink-0 border-t border-border/60 bg-card/30">
        {/* Mode toggle + message count */}
        <div className="px-4 pt-2 pb-1 flex items-center justify-between">
          <div className="relative flex items-center w-[280px] h-8 bg-muted/50 rounded-full p-1 border border-border">
            <div
              className={cn(
                "absolute top-1 bottom-1 rounded-full bg-foreground transition-all duration-200 ease-out",
                chatMode === "chat"    ? "left-1 w-[calc(33.333%-3px)]" :
                chatMode === "command" ? "left-[calc(33.333%+1px)] w-[calc(33.333%-3px)]" :
                                         "left-[calc(66.666%+1px)] w-[calc(33.333%-4px)]"
              )}
            />
            <button
              onClick={() => {
                setChatMode("chat");
                if (!inputValue.startsWith("aos") && !inputValue.startsWith("/")) {
                  setShowSuggestions(false);
                }
              }}
              className={cn(
                "relative z-10 flex-1 flex items-center justify-center gap-1.5 h-full rounded-full text-[11px] font-mono font-medium tracking-wide transition-colors cursor-pointer select-none",
                chatMode === "chat" ? "text-background" : "text-muted-foreground hover:text-foreground"
              )}
            >
              <MessageSquare className="h-3 w-3" />
              Chat
            </button>
            <button
              onClick={() => {
                setChatMode("command");
                setShowSuggestions(true);
                // Small delay so the state update and re-render complete before focus
                setTimeout(() => inputRef.current?.focus(), 0);
              }}
              className={cn(
                "relative z-10 flex-1 flex items-center justify-center gap-1.5 h-full rounded-full text-[11px] font-mono font-medium tracking-wide transition-colors cursor-pointer select-none",
                chatMode === "command" ? "text-background" : "text-muted-foreground hover:text-foreground"
              )}
            >
              <Terminal className="h-3 w-3" />
              Cmds
            </button>
            <button
              onClick={() => {
                setChatMode("auto");
                if (!inputValue.startsWith("aos") && !inputValue.startsWith("/")) {
                  setShowSuggestions(false);
                }
              }}
              className={cn(
                "relative z-10 flex-1 flex items-center justify-center gap-1.5 h-full rounded-full text-[11px] font-mono font-medium tracking-wide transition-colors cursor-pointer select-none",
                chatMode === "auto" ? "text-background" : "text-muted-foreground hover:text-foreground"
              )}
            >
              <Wand2 className="h-3 w-3" />
              Auto
            </button>
          </div>
          <span className="text-[10px] text-muted-foreground font-mono">
            {messages.length} message{messages.length !== 1 ? "s" : ""}
          </span>
        </div>

        {/* Quick actions bar */}
        {quickActions.length > 0 && (
          <div className="px-4 pb-1">
            <QuickActionBar actions={quickActions} onAction={handleQuickAction} />
          </div>
        )}

        {/* Input bar */}
        <div className="px-4 pb-3 relative">
          {/* Command suggestions dropdown */}
          <CommandSuggestionsPanel
            suggestions={commandSuggestions}
            filter={inputValue}
            onSelect={handleSelectCommand}
            visible={showSuggestions}
          />

          <div className={cn(
            "flex items-end gap-2 rounded-lg border bg-background px-3 py-2 transition-colors",
            isCommand
              ? "border-primary/40 shadow-[0_0_8px_rgba(var(--primary),0.1)]"
              : "border-border/60"
          )}>
            {/* Command mode indicator */}
            {isCommand && (
              <div className="shrink-0 flex items-center gap-1 text-primary pb-1">
                <Terminal className="h-3.5 w-3.5" />
              </div>
            )}

            <textarea
              ref={inputRef}
              value={inputValue}
              onChange={handleInputChange}
              onKeyDown={handleKeyDown}
              onFocus={() => {
                if (chatMode === "command" || inputValue.startsWith("aos") || inputValue.startsWith("/")) {
                  setShowSuggestions(true);
                }
              }}
              onBlur={() => {
                // Delay to allow click on suggestion
                setTimeout(() => setShowSuggestions(false), 200);
              }}
              placeholder={
                isSubmitting
                  ? "Waiting for response..."
                  : chatMode === "command"
                    ? "Type an aos command or select from suggestions above..."
                    : chatMode === "auto"
                      ? "Ask anything — auto-classifies as chat or command"
                      : "Ask a question or type 'aos' for commands..."
              }
              disabled={isSubmitting}
              className="flex-1 bg-transparent text-sm font-mono text-foreground placeholder:text-muted-foreground/50 resize-none outline-none min-h-[20px] max-h-[120px] py-0.5"
              rows={1}
              style={{
                height: "auto",
                overflowY: inputValue.split("\n").length > 4 ? "auto" : "hidden",
              }}
              onInput={(e) => {
                const target = e.target as HTMLTextAreaElement;
                target.style.height = "auto";
                target.style.height = `${Math.min(target.scrollHeight, 120)}px`;
              }}
            />

            <Button
              size="sm"
              className={cn(
                "shrink-0 h-7 w-7 p-0 rounded-md transition-all",
                inputValue.trim()
                  ? "bg-primary text-primary-foreground hover:bg-primary/90 shadow-[0_0_8px_rgba(var(--primary),0.2)]"
                  : "bg-muted text-muted-foreground"
              )}
              onClick={handleSend}
              disabled={!inputValue.trim() || isSubmitting}
            >
              {isSubmitting ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Send className="h-3.5 w-3.5" />
              )}
            </Button>
          </div>

          <div className="flex items-center justify-between mt-1.5 px-1">
            <span className="text-[10px] text-muted-foreground/40 font-mono">
              Enter to send · Shift+Enter for new line · Type <kbd className="px-1 py-0.5 rounded bg-muted/50 border border-border/30 text-[9px]">aos</kbd> for commands
            </span>
            {isCommand && (
              <Badge variant="outline" className="text-[9px] h-4 px-1.5 text-primary bg-primary/5 border-primary/20 font-mono">
                Command Mode
              </Badge>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
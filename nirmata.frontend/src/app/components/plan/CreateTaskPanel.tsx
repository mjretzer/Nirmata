/**
 * CreateTaskPanel — Multi-step task creation wizard
 *
 * UX principles applied:
 *   • Progressive disclosure: step-gated form, "More options" collapse
 *   • Inline validation: per-field errors shown in context with ARIA links
 *   • Gestalt proximity: related fields grouped with label + hint together
 *   • Distinct Save Draft vs Publish affordance
 *   • Undo for accidental list-item deletion via toast action
 *   • Keyboard accessibility: logical tab order, focus management on step change
 *   • Plain-language microcopy: error text says what's wrong + how to fix
 *   • Disabled submit until required fields are valid
 */

import { useState, useId, useRef, useEffect, useCallback } from "react";
import {
  X,
  ChevronRight,
  ChevronDown,
  ChevronUp,
  Plus,
  Trash2,
  CheckCircle,
  AlertCircle,
  Info,
  GripVertical,
  Loader2,
  FileCode,
  Shield,
  ListTodo,
  ClipboardList,
  Eye,
  Lock,
  Tag,
  User,
  GitBranch,
} from "lucide-react";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Label } from "../ui/label";
import { Textarea } from "../ui/textarea";
import { Badge } from "../ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../ui/select";
import { Separator } from "../ui/separator";
import { cn } from "../ui/utils";
import { toast } from "sonner";
import { usePhases, useWorkspace, type Phase, type Task } from "../../hooks/useAosData";

// ── Types ────────────────────────────────────────────────────────────

export interface CreateTaskFormData {
  name: string;
  phaseId: string;
  assignee: string;
  description: string;
  steps: string[];
  fileScope: string[];
  tags: string[];
  verifications: string[];
  definitionOfDone: string[];
}

interface CreateTaskPanelProps {
  /** Which phase to pre-fill (from "Add Task" on a phase row) */
  defaultPhaseId?: string;
  onClose: () => void;
  onSaveDraft: (data: CreateTaskFormData) => void;
  onPublish: (data: CreateTaskFormData) => void;
}

// ── Constants ────────────────────────────────────────────────────────

const STEPS = [
  { id: "basics",  label: "Basics",       icon: ListTodo },
  { id: "plan",    label: "Work Plan",    icon: ClipboardList },
  { id: "verify",  label: "Verification", icon: Shield },
  { id: "review",  label: "Review",       icon: Eye },
] as const;
type StepId = typeof STEPS[number]["id"];

const ASSIGNEES = ["Executor-Alpha", "Executor-Beta", "Unassigned"] as const;

const EXAMPLE_STEPS = [
  "Review requirements and spec files",
  "Implement feature logic",
  "Write unit tests",
  "Run lint and build checks",
];

const EXAMPLE_VERIFICATIONS = [
  "npm run test",
  "npm run build",
  "Manual test: verify feature works end-to-end",
];

// ── Validation ───────────────────────────────────────────────────────

interface FieldErrors {
  name?: string;
  phaseId?: string;
  steps?: string;
  verifications?: string;
  definitionOfDone?: string;
}

function validateStep(
  step: StepId,
  form: CreateTaskFormData
): FieldErrors {
  const errors: FieldErrors = {};
  if (step === "basics" || step === "review") {
    if (!form.name.trim())
      errors.name = "Task name is required. Enter a short description of the work.";
    else if (form.name.trim().length < 3)
      errors.name = "Task name is too short — use at least 3 characters.";
    else if (form.name.trim().length > 120)
      errors.name = "Task name is too long — keep it under 120 characters.";
    if (!form.phaseId)
      errors.phaseId = "Select a phase so the engine knows where this task belongs.";
  }
  if (step === "plan" || step === "review") {
    const filled = form.steps.filter((s) => s.trim().length > 0);
    if (filled.length === 0)
      errors.steps = "Add at least one step so the engine knows what to do.";
  }
  if (step === "verify" || step === "review") {
    const filledDod = form.definitionOfDone.filter((d) => d.trim().length > 0);
    if (filledDod.length === 0)
      errors.definitionOfDone =
        "Add at least one \u201cDone when\u2026\u201d criterion so verification can succeed.";
  }
  return errors;
}

function stepIsValid(step: StepId, form: CreateTaskFormData): boolean {
  return Object.keys(validateStep(step, form)).length === 0;
}

// ── Sub-components ───────────────────────────────────────────────────

/** Accessible form field with label + hint + error */
function Field({
  id,
  label,
  hint,
  error,
  required,
  children,
}: {
  id: string;
  label: string;
  hint?: string;
  error?: string;
  required?: boolean;
  children: React.ReactNode;
}) {
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  return (
    <div className="space-y-1.5">
      <div className="flex items-baseline justify-between gap-2">
        <Label
          htmlFor={id}
          className={cn("text-sm", error && "text-destructive")}
        >
          {label}
          {required && (
            <span className="text-destructive ml-0.5" aria-hidden="true">
              *
            </span>
          )}
        </Label>
        {hint && (
          <span
            id={hintId}
            className="text-[10px] text-muted-foreground/50 shrink-0"
          >
            {hint}
          </span>
        )}
      </div>

      {/* Clone child to inject aria attributes */}
      <div
        aria-describedby={
          [error ? errorId : null, hint ? hintId : null]
            .filter(Boolean)
            .join(" ") || undefined
        }
      >
        {children}
      </div>

      {error && (
        <p
          id={errorId}
          role="alert"
          className="flex items-start gap-1.5 text-xs text-destructive"
        >
          <AlertCircle
            className="h-3.5 w-3.5 shrink-0 mt-px"
            aria-hidden="true"
          />
          {error}
        </p>
      )}
    </div>
  );
}

/** Dynamic ordered list with add / remove + undo-on-delete */
function DynamicList({
  label,
  items,
  onChange,
  placeholder,
  addLabel,
  emptyHint,
  error,
  examples,
  maxItems = 20,
}: {
  label: string;
  items: string[];
  onChange: (items: string[]) => void;
  placeholder: string;
  addLabel: string;
  emptyHint?: string;
  error?: string;
  examples?: string[];
  maxItems?: number;
}) {
  const listId = useId();

  const handleChange = (idx: number, val: string) => {
    const next = [...items];
    next[idx] = val;
    onChange(next);
  };

  const handleAdd = () => {
    if (items.length >= maxItems) return;
    onChange([...items, ""]);
  };

  const handleRemove = (idx: number) => {
    const removed = items[idx];
    const next = items.filter((_, i) => i !== idx);
    onChange(next);
    // Show undo toast
    if (removed.trim()) {
      toast(`Removed: "${removed.slice(0, 40)}${removed.length > 40 ? "…" : ""}"`, {
        action: {
          label: "Undo",
          onClick: () => {
            const restored = [...next];
            restored.splice(idx, 0, removed);
            onChange(restored);
          },
        },
        duration: 4000,
      });
    }
  };

  const handleUseExample = (ex: string) => {
    if (!items.includes(ex)) onChange([...items, ex]);
  };

  return (
    <div className="space-y-2" id={listId} role="group" aria-label={label}>
      {items.map((item, idx) => (
        <div key={idx} className="flex items-start gap-2 group">
          <span
            className="mt-2.5 text-[10px] text-muted-foreground/40 w-5 text-right shrink-0 font-mono"
            aria-hidden="true"
          >
            {idx + 1}.
          </span>
          <Input
            value={item}
            onChange={(e) => handleChange(idx, e.target.value)}
            placeholder={placeholder}
            className={cn(
              "h-8 text-xs flex-1",
              error && idx === 0 && !item.trim() && "border-destructive/50 focus-visible:ring-destructive/30"
            )}
            aria-label={`${label} item ${idx + 1}`}
          />
          <button
            type="button"
            onClick={() => handleRemove(idx)}
            className="mt-1.5 p-1 rounded text-muted-foreground/30 hover:text-destructive hover:bg-destructive/10 transition-colors opacity-0 group-hover:opacity-100 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-destructive/40"
            aria-label={`Remove ${label} item ${idx + 1}: "${item}"`}
          >
            <Trash2 className="h-3.5 w-3.5" />
          </button>
        </div>
      ))}

      {/* Empty hint */}
      {items.length === 0 && emptyHint && (
        <p className="text-xs text-muted-foreground/40 italic pl-7">{emptyHint}</p>
      )}

      {/* Error */}
      {error && (
        <p role="alert" className="flex items-start gap-1.5 text-xs text-destructive pl-7">
          <AlertCircle className="h-3.5 w-3.5 shrink-0 mt-px" aria-hidden="true" />
          {error}
        </p>
      )}

      <div className="flex items-center gap-2 pl-7 pt-1 flex-wrap">
        <button
          type="button"
          onClick={handleAdd}
          disabled={items.length >= maxItems}
          className={cn(
            "flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors focus-visible:outline-none focus-visible:underline",
            items.length >= maxItems && "opacity-30 pointer-events-none"
          )}
          aria-label={`${addLabel} (${items.length}/${maxItems})`}
        >
          <Plus className="h-3.5 w-3.5" aria-hidden="true" />
          {addLabel}
        </button>
        {examples && items.length === 0 && (
          <span className="text-[10px] text-muted-foreground/40">
            Try:{" "}
            {examples.slice(0, 2).map((ex, i) => (
              <button
                key={i}
                type="button"
                onClick={() => handleUseExample(ex)}
                className="underline hover:no-underline focus-visible:outline-none"
                aria-label={`Use example: ${ex}`}
              >
                {ex.length > 28 ? ex.slice(0, 28) + "…" : ex}
              </button>
            ))}
          </span>
        )}
      </div>
    </div>
  );
}

/** Tag input for file scope */
function TagInput({
  label,
  tags,
  onChange,
  placeholder,
}: {
  label: string;
  tags: string[];
  onChange: (tags: string[]) => void;
  placeholder: string;
}) {
  const [input, setInput] = useState("");

  const addTag = (val: string) => {
    const v = val.trim();
    if (v && !tags.includes(v)) onChange([...tags, v]);
    setInput("");
  };

  const removeTag = (tag: string) => {
    const next = tags.filter((t) => t !== tag);
    onChange(next);
    toast(`Removed: "${tag}"`, {
      action: { label: "Undo", onClick: () => onChange([...next, tag]) },
      duration: 3000,
    });
  };

  return (
    <div className="space-y-2">
      <div className="flex gap-2">
        <Input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder={placeholder}
          className="h-8 text-xs flex-1 font-mono"
          aria-label={label}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === ",") {
              e.preventDefault();
              addTag(input);
            }
            if (e.key === "Backspace" && !input && tags.length > 0) {
              removeTag(tags[tags.length - 1]);
            }
          }}
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="h-8 text-xs px-2"
          onClick={() => addTag(input)}
          disabled={!input.trim()}
          aria-label="Add file to scope"
        >
          <Plus className="h-3.5 w-3.5" />
        </Button>
      </div>
      {tags.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {tags.map((tag) => (
            <span
              key={tag}
              className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-muted/50 border border-border/50 text-[10px] font-mono text-muted-foreground group"
            >
              {tag}
              <button
                type="button"
                onClick={() => removeTag(tag)}
                className="text-muted-foreground/40 hover:text-destructive focus-visible:outline-none focus-visible:text-destructive transition-colors"
                aria-label={`Remove ${tag} from file scope`}
              >
                <X className="h-2.5 w-2.5" />
              </button>
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Step Components ──────────────────────────────────────────────────

function StepBasics({
  form,
  onChange,
  errors,
  touched,
  onTouch,
}: {
  form: CreateTaskFormData;
  onChange: (partial: Partial<CreateTaskFormData>) => void;
  errors: FieldErrors;
  touched: Set<string>;
  onTouch: (field: string) => void;
}) {
  return (
    <div className="space-y-5">
      <div>
        <h3 className="text-sm mb-0.5">Task basics</h3>
        <p className="text-xs text-muted-foreground">
          Give this task a clear name and assign it to a phase and executor.
        </p>
      </div>

      {/* Task name */}
      <Field
        id="task-name"
        label="Task name"
        required
        hint={`${form.name.length}/120`}
        error={touched.has("name") ? errors.name : undefined}
      >
        <Input
          id="task-name"
          value={form.name}
          maxLength={120}
          onChange={(e) => onChange({ name: e.target.value })}
          onBlur={() => onTouch("name")}
          placeholder="e.g. Implement OAuth login flow"
          className={cn(
            "h-9",
            touched.has("name") && errors.name && "border-destructive/50 focus-visible:ring-destructive/30"
          )}
          autoFocus
          aria-required="true"
          aria-invalid={!!(touched.has("name") && errors.name)}
        />
      </Field>

      {/* Phase */}
      <Field
        id="task-phase"
        label="Phase"
        required
        error={touched.has("phaseId") ? errors.phaseId : undefined}
      >
        <Select
          value={form.phaseId}
          onValueChange={(v) => { onChange({ phaseId: v }); onTouch("phaseId"); }}
        >
          <SelectTrigger
            id="task-phase"
            className={cn(
              "h-9 text-xs",
              touched.has("phaseId") && errors.phaseId && "border-destructive/50"
            )}
            aria-required="true"
            aria-invalid={!!(touched.has("phaseId") && errors.phaseId)}
          >
            <SelectValue placeholder="Select a phase…" />
          </SelectTrigger>
          <SelectContent>
            {allPhases.map((ph) => (
              <SelectItem key={ph.id} value={ph.id} className="text-xs">
                <span className="font-mono text-muted-foreground mr-2">{ph.id}</span>
                {ph.title}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      {/* Assignee */}
      <Field id="task-assignee" label="Assign to" hint="Optional">
        <Select
          value={form.assignee}
          onValueChange={(v) => onChange({ assignee: v })}
        >
          <SelectTrigger id="task-assignee" className="h-9 text-xs">
            <SelectValue placeholder="Unassigned" />
          </SelectTrigger>
          <SelectContent>
            {ASSIGNEES.map((a) => (
              <SelectItem key={a} value={a} className="text-xs">
                <User className="h-3 w-3 mr-1.5 inline" aria-hidden="true" />
                {a}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      {/* Description */}
      <Field
        id="task-desc"
        label="Description"
        hint={`${form.description.length}/280 — optional`}
      >
        <Textarea
          id="task-desc"
          value={form.description}
          maxLength={280}
          onChange={(e) => onChange({ description: e.target.value })}
          placeholder="Briefly describe the goal of this task in plain language…"
          className="text-xs min-h-[72px]"
          rows={3}
        />
      </Field>
    </div>
  );
}

function StepPlan({
  form,
  onChange,
  errors,
  touched,
  onTouch,
}: {
  form: CreateTaskFormData;
  onChange: (partial: Partial<CreateTaskFormData>) => void;
  errors: FieldErrors;
  touched: Set<string>;
  onTouch: (field: string) => void;
}) {
  const [showAdvanced, setShowAdvanced] = useState(false);

  return (
    <div className="space-y-5">
      <div>
        <h3 className="text-sm mb-0.5">Work plan</h3>
        <p className="text-xs text-muted-foreground">
          Break the task into ordered steps. The engine follows these exactly.
        </p>
      </div>

      {/* Steps */}
      <Field
        id="task-steps"
        label="Steps"
        required
        error={touched.has("steps") ? errors.steps : undefined}
      >
        <div onClick={() => onTouch("steps")}>
          <DynamicList
            label="Step"
            items={form.steps}
            onChange={(steps) => { onChange({ steps }); onTouch("steps"); }}
            placeholder="Describe this step in plain language…"
            addLabel="Add step"
            emptyHint="No steps yet — add at least one so the engine knows what to do."
            error={touched.has("steps") ? errors.steps : undefined}
            examples={EXAMPLE_STEPS}
          />
        </div>
      </Field>

      <Separator className="my-1" />

      {/* File scope */}
      <Field
        id="task-filescope"
        label="Files in scope"
        hint="Optional — press Enter or comma to add"
      >
        <div className="space-y-1.5">
          <div className="flex items-center gap-1.5 text-[10px] text-muted-foreground/50 mb-2">
            <FileCode className="h-3 w-3" aria-hidden="true" />
            <span>Add file paths the engine is allowed to edit for this task.</span>
          </div>
          <TagInput
            label="File scope"
            tags={form.fileScope}
            onChange={(fileScope) => onChange({ fileScope })}
            placeholder="e.g. src/auth/OAuthProvider.ts"
          />
        </div>
      </Field>

      {/* More options — progressive disclosure */}
      <button
        type="button"
        className="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors focus-visible:outline-none focus-visible:underline"
        onClick={() => setShowAdvanced((v) => !v)}
        aria-expanded={showAdvanced}
        aria-controls="plan-advanced"
      >
        {showAdvanced ? (
          <ChevronUp className="h-3.5 w-3.5" aria-hidden="true" />
        ) : (
          <ChevronDown className="h-3.5 w-3.5" aria-hidden="true" />
        )}
        {showAdvanced ? "Hide" : "More"} options
      </button>

      {showAdvanced && (
        <div id="plan-advanced" className="space-y-5 pt-1">
          {/* Tags */}
          <Field id="task-tags" label="Tags" hint="Optional">
            <TagInput
              label="Tags"
              tags={form.tags}
              onChange={(tags) => onChange({ tags })}
              placeholder="e.g. auth, security, backend…"
            />
          </Field>
        </div>
      )}
    </div>
  );
}

function StepVerify({
  form,
  onChange,
  errors,
  touched,
  onTouch,
  phase,
}: {
  form: CreateTaskFormData;
  onChange: (partial: Partial<CreateTaskFormData>) => void;
  errors: FieldErrors;
  touched: Set<string>;
  onTouch: (field: string) => void;
  phase: Phase | undefined;
}) {
  return (
    <div className="space-y-5">
      <div>
        <h3 className="text-sm mb-0.5">Verification criteria</h3>
        <p className="text-xs text-muted-foreground">
          Define how the engine confirms this task is complete and correct.
        </p>
      </div>

      {/* Suggested from phase acceptance criteria */}
      {phase?.acceptance.criteria && phase.acceptance.criteria.length > 0 && (
        <div className="bg-muted/20 border border-border/40 rounded-lg p-3 space-y-2">
          <p className="text-[10px] uppercase tracking-wider text-muted-foreground/50 flex items-center gap-1.5">
            <Info className="h-3 w-3" aria-hidden="true" />
            From phase {phase.id} acceptance criteria
          </p>
          <ul className="space-y-1">
            {phase.acceptance.criteria.map((c, i) => (
              <li key={i} className="flex items-start gap-2">
                <span className="text-[10px] text-muted-foreground/60 flex-1 leading-relaxed">
                  {c}
                </span>
                <button
                  type="button"
                  className="text-[10px] text-muted-foreground/40 hover:text-primary transition-colors underline focus-visible:outline-none shrink-0"
                  onClick={() => {
                    if (!form.definitionOfDone.includes(c))
                      onChange({ definitionOfDone: [...form.definitionOfDone, c] });
                  }}
                  aria-label={`Use as done criterion: ${c}`}
                >
                  Use
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Verification commands */}
      <Field
        id="task-verifications"
        label="Verification commands"
        hint="Optional"
      >
        <DynamicList
          label="Verification"
          items={form.verifications}
          onChange={(verifications) => onChange({ verifications })}
          placeholder="e.g. npm run test"
          addLabel="Add command"
          emptyHint="Add automated test commands to run after execution."
          examples={EXAMPLE_VERIFICATIONS}
        />
      </Field>

      <Separator className="my-1" />

      {/* Definition of done */}
      <Field
        id="task-dod"
        label="Done when…"
        required
        error={touched.has("definitionOfDone") ? errors.definitionOfDone : undefined}
      >
        <div onClick={() => onTouch("definitionOfDone")}>
          <DynamicList
            label="Done criterion"
            items={form.definitionOfDone}
            onChange={(definitionOfDone) => { onChange({ definitionOfDone }); onTouch("definitionOfDone"); }}
            placeholder='e.g. "User can log in via Google OAuth"'
            addLabel="Add criterion"
            emptyHint="At least one criterion is required so verification can succeed."
            error={touched.has("definitionOfDone") ? errors.definitionOfDone : undefined}
          />
        </div>
      </Field>
    </div>
  );
}

function StepReview({
  form,
  errors,
  phase,
}: {
  form: CreateTaskFormData;
  errors: FieldErrors;
  phase: Phase | undefined;
}) {
  const hasErrors = Object.keys(errors).length > 0;
  return (
    <div className="space-y-5">
      <div>
        <h3 className="text-sm mb-0.5">Review & publish</h3>
        <p className="text-xs text-muted-foreground">
          Check the task details before saving. You can save as a draft to continue later.
        </p>
      </div>

      {hasErrors && (
        <div
          role="alert"
          className="flex items-start gap-2 p-3 rounded-lg bg-destructive/5 border border-destructive/20 text-xs text-destructive"
        >
          <AlertCircle className="h-4 w-4 shrink-0 mt-px" aria-hidden="true" />
          <div>
            <p className="font-medium mb-1">Some required fields need attention:</p>
            <ul className="space-y-0.5">
              {Object.values(errors).map((e, i) => (
                <li key={i}>• {e}</li>
              ))}
            </ul>
          </div>
        </div>
      )}

      {/* Summary */}
      <div className="bg-muted/20 rounded-lg border border-border/40 divide-y divide-border/30">
        <div className="px-3 py-2.5 flex items-center justify-between">
          <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">Task name</span>
          <span className={cn("text-sm font-medium", !form.name.trim() && "text-muted-foreground/30 italic")}>
            {form.name.trim() || "—"}
          </span>
        </div>
        <div className="px-3 py-2.5 flex items-center justify-between">
          <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">Phase</span>
          <span className="text-xs font-mono">{phase?.id ?? <span className="text-muted-foreground/30 italic">—</span>}</span>
        </div>
        <div className="px-3 py-2.5 flex items-center justify-between">
          <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">Assignee</span>
          <span className="text-xs">{form.assignee || "Unassigned"}</span>
        </div>
        {form.description.trim() && (
          <div className="px-3 py-2.5">
            <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider block mb-1">Description</span>
            <p className="text-xs text-muted-foreground leading-relaxed">{form.description}</p>
          </div>
        )}
        <div className="px-3 py-2.5 flex items-center justify-between">
          <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">Steps</span>
          <span className={cn("text-xs", form.steps.filter(s => s.trim()).length === 0 && "text-destructive/70")}>
            {form.steps.filter(s => s.trim()).length} step{form.steps.filter(s => s.trim()).length !== 1 ? "s" : ""}
          </span>
        </div>
        <div className="px-3 py-2.5 flex items-center justify-between">
          <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">File scope</span>
          <span className="text-xs text-muted-foreground">
            {form.fileScope.length > 0 ? `${form.fileScope.length} file${form.fileScope.length !== 1 ? "s" : ""}` : "Not specified"}
          </span>
        </div>
        <div className="px-3 py-2.5 flex items-center justify-between">
          <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">Verifications</span>
          <span className="text-xs text-muted-foreground">{form.verifications.filter(v => v.trim()).length}</span>
        </div>
        <div className="px-3 py-2.5 flex items-center justify-between">
          <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">Done criteria</span>
          <span className={cn("text-xs", form.definitionOfDone.filter(d => d.trim()).length === 0 && "text-destructive/70")}>
            {form.definitionOfDone.filter(d => d.trim()).length}
          </span>
        </div>
        {form.tags.length > 0 && (
          <div className="px-3 py-2.5 flex items-start gap-2">
            <span className="text-[10px] text-muted-foreground/50 uppercase tracking-wider shrink-0 pt-0.5">Tags</span>
            <div className="flex flex-wrap gap-1 justify-end flex-1">
              {form.tags.map(t => (
                <Badge key={t} variant="secondary" className="text-[10px] h-5">{t}</Badge>
              ))}
            </div>
          </div>
        )}
      </div>

      {!hasErrors && (
        <p className="flex items-center gap-1.5 text-xs text-green-400">
          <CheckCircle className="h-3.5 w-3.5" aria-hidden="true" />
          Task is ready to publish.
        </p>
      )}
    </div>
  );
}

// ── Main Panel ───────────────────────────────────────────────────────

export function CreateTaskPanel({
  defaultPhaseId,
  onClose,
  onSaveDraft,
  onPublish,
}: CreateTaskPanelProps) {
  const { phases: allPhases } = usePhases();
  const { workspace } = useWorkspace();
  const [currentStep, setCurrentStep] = useState<number>(0);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [touched, setTouched] = useState<Set<string>>(new Set());
  const stepPanelRef = useRef<HTMLDivElement>(null);

  const [form, setForm] = useState<CreateTaskFormData>({
    name: "",
    phaseId: defaultPhaseId ?? "",
    assignee: "Unassigned",
    description: "",
    steps: [],
    fileScope: [],
    tags: [],
    verifications: [],
    definitionOfDone: [],
  });

  const updateForm = useCallback((partial: Partial<CreateTaskFormData>) => {
    setForm((prev) => ({ ...prev, ...partial }));
  }, []);

  const touch = useCallback((field: string) => {
    setTouched((prev) => new Set(prev).add(field));
  }, []);

  const currentStepId = STEPS[currentStep].id;
  const selectedPhase = allPhases.find((p) => p.id === form.phaseId);
  const currentErrors = validateStep(currentStepId, form);
  const allErrors = validateStep("review", form);
  const isCurrentStepValid = Object.keys(currentErrors).length === 0;
  const isFormComplete = Object.keys(allErrors).length === 0;

  // Focus the step container on step change for screen readers
  useEffect(() => {
    stepPanelRef.current?.focus({ preventScroll: false });
  }, [currentStep]);

  const goNext = () => {
    // Touch all fields of current step before advancing
    const stepFields: Record<StepId, string[]> = {
      basics: ["name", "phaseId"],
      plan: ["steps"],
      verify: ["definitionOfDone"],
      review: [],
    };
    stepFields[currentStepId].forEach(touch);
    if (!isCurrentStepValid) return;
    setCurrentStep((s) => Math.min(s + 1, STEPS.length - 1));
    setTouched(new Set());
  };

  const goBack = () => {
    setCurrentStep((s) => Math.max(s - 1, 0));
    setTouched(new Set());
  };

  const handleSaveDraft = async () => {
    setIsSubmitting(true);
    await new Promise((r) => setTimeout(r, 600));
    setIsSubmitting(false);
    onSaveDraft(form);
    toast.success("Draft saved — you can continue editing later.");
    onClose();
  };

  const handlePublish = async () => {
    if (!isFormComplete) {
      setCurrentStep(0);
      setTouched(new Set(["name", "phaseId", "steps", "definitionOfDone"]));
      toast.error("Fix the highlighted errors before publishing.");
      return;
    }
    setIsSubmitting(true);
    await new Promise((r) => setTimeout(r, 800));
    setIsSubmitting(false);
    onPublish(form);
    toast.success(`Task created: ${form.name}`);
    onClose();
  };

  const handleClose = () => {
    if (form.name.trim() || form.steps.some(s => s.trim())) {
      const hasWork = true; // would normally confirm
      toast("Your progress was discarded.", {
        action: { label: "Keep editing", onClick: () => {} },
        duration: 3000,
      });
    }
    onClose();
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-stretch justify-end"
      role="dialog"
      aria-modal="true"
      aria-labelledby="create-task-title"
    >
      {/* Backdrop */}
      <button
        className="absolute inset-0 bg-black/50 cursor-default"
        onClick={handleClose}
        tabIndex={-1}
        aria-label="Close panel"
      />

      {/* Panel */}
      <div className="relative w-full max-w-md bg-background border-l border-border flex flex-col shadow-2xl">

        {/* ── Header ──────────────────────────────────── */}
        <div className="flex items-start justify-between p-5 border-b border-border shrink-0">
          <div>
            <h2 id="create-task-title" className="text-base">
              Create Task
            </h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Define a new unit of work for the engine to execute.
            </p>
          </div>
          <button
            onClick={handleClose}
            className="p-1.5 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
            aria-label="Close create task panel"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>

        {/* ── Step indicator ───────────────────────────── */}
        <div
          className="flex items-center px-5 py-3 gap-0 border-b border-border shrink-0"
          role="list"
          aria-label="Task creation steps"
        >
          {STEPS.map((step, i) => {
            const StepIcon = step.icon;
            const isDone = i < currentStep;
            const isActive = i === currentStep;
            const isUpcoming = i > currentStep;
            return (
              <div
                key={step.id}
                role="listitem"
                aria-current={isActive ? "step" : undefined}
                className="flex items-center flex-1"
              >
                <button
                  type="button"
                  disabled={i > currentStep}
                  onClick={() => { if (i < currentStep) { setCurrentStep(i); setTouched(new Set()); }}}
                  className={cn(
                    "flex items-center gap-1.5 text-xs transition-colors focus-visible:outline-none focus-visible:underline shrink-0",
                    isDone && "text-primary cursor-pointer",
                    isActive && "text-foreground font-medium cursor-default",
                    isUpcoming && "text-muted-foreground/30 cursor-default"
                  )}
                  aria-label={`Step ${i + 1}: ${step.label}${isDone ? " (completed)" : isActive ? " (current)" : ""}`}
                >
                  <span
                    className={cn(
                      "h-5 w-5 rounded-full flex items-center justify-center text-[10px] border transition-colors",
                      isDone && "bg-primary/20 border-primary/40 text-primary",
                      isActive && "bg-background border-primary text-primary ring-2 ring-primary/20",
                      isUpcoming && "bg-muted/30 border-border/40 text-muted-foreground/30"
                    )}
                  >
                    {isDone ? <CheckCircle className="h-3 w-3" /> : i + 1}
                  </span>
                  <span className="hidden sm:inline">{step.label}</span>
                </button>
                {i < STEPS.length - 1 && (
                  <div
                    className={cn(
                      "h-px flex-1 mx-2 transition-colors",
                      i < currentStep ? "bg-primary/30" : "bg-border/40"
                    )}
                    aria-hidden="true"
                  />
                )}
              </div>
            );
          })}
        </div>

        {/* ── Step content ────────────────────────────── */}
        <div
          className="flex-1 overflow-y-auto p-5"
          ref={stepPanelRef}
          tabIndex={-1}
          aria-live="polite"
          aria-atomic="true"
          aria-label={`Step ${currentStep + 1} of ${STEPS.length}: ${STEPS[currentStep].label}`}
        >
          {currentStepId === "basics" && (
            <StepBasics
              form={form}
              onChange={updateForm}
              errors={currentErrors}
              touched={touched}
              onTouch={touch}
            />
          )}
          {currentStepId === "plan" && (
            <StepPlan
              form={form}
              onChange={updateForm}
              errors={currentErrors}
              touched={touched}
              onTouch={touch}
            />
          )}
          {currentStepId === "verify" && (
            <StepVerify
              form={form}
              onChange={updateForm}
              errors={currentErrors}
              touched={touched}
              onTouch={touch}
              phase={selectedPhase}
            />
          )}
          {currentStepId === "review" && (
            <StepReview
              form={form}
              errors={allErrors}
              phase={selectedPhase}
            />
          )}
        </div>

        {/* ── Footer ──────────────────────────────────── */}
        <div className="shrink-0 border-t border-border p-4 space-y-3">
          {/* Progress bar */}
          <div
            className="h-1 w-full bg-muted/40 rounded-full overflow-hidden"
            role="progressbar"
            aria-valuenow={currentStep + 1}
            aria-valuemin={1}
            aria-valuemax={STEPS.length}
            aria-label={`Step ${currentStep + 1} of ${STEPS.length}`}
          >
            <div
              className="h-full bg-primary/60 rounded-full transition-all duration-300"
              style={{ width: `${((currentStep + 1) / STEPS.length) * 100}%` }}
            />
          </div>

          <div className="flex items-center gap-2">
            {/* Back */}
            {currentStep > 0 && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="gap-1.5 text-xs"
                onClick={goBack}
                aria-label="Go to previous step"
              >
                Back
              </Button>
            )}

            {/* Cancel / Save Draft */}
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="gap-1.5 text-xs ml-auto"
              onClick={handleSaveDraft}
              disabled={isSubmitting}
              aria-label="Save this task as a draft to continue later"
            >
              {isSubmitting ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : null}
              Save Draft
            </Button>

            {/* Continue / Publish */}
            {currentStep < STEPS.length - 1 ? (
              <Button
                type="button"
                size="sm"
                className="gap-1.5 text-xs"
                onClick={goNext}
                aria-label={`Continue to ${STEPS[currentStep + 1].label} step`}
              >
                Continue
                <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
              </Button>
            ) : (
              <Button
                type="button"
                size="sm"
                className="gap-1.5 text-xs"
                disabled={!isFormComplete || isSubmitting}
                onClick={handlePublish}
                aria-label={
                  !isFormComplete
                    ? "Fix errors before publishing"
                    : "Publish this task to the engine"
                }
                title={
                  !isFormComplete
                    ? "Fix all required fields before publishing"
                    : undefined
                }
              >
                {isSubmitting ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  <CheckCircle className="h-3.5 w-3.5" />
                )}
                Publish Task
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
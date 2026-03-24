import { NoWorkspaceDetected } from "../components/NoWorkspaceDetected";

export function NoChatPage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to chat about its cursor, gate state, and artifacts." />
  );
}

export function NoPlanPage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to view .aos/spec and the roadmap/task lenses." />
  );
}

export function NoVerificationPage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to view UAT checks and issues." />
  );
}

export function NoRunsPage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to view .aos/evidence/runs." />
  );
}

export function NoContinuityPage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to view .aos/state and handoff/checkpoints." />
  );
}

export function NoCodebasePage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to view .aos/codebase intelligence." />
  );
}

export function NoSettingsPage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to configure engine/workspace/provider/git settings." />
  );
}

export function NoHostPage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to access engine host controls and diagnostics." />
  );
}

export function NoDiagnosticsPage() {
  return (
    <NoWorkspaceDetected description="Select a workspace to access engine host controls and diagnostics." />
  );
}

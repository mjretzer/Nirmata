import { createBrowserRouter, Navigate } from "react-router";
import { Layout } from "./components/Layout";
import { WorkspacePathPage } from "./pages/WorkspacePathPage";
import {
  SettingsPage,
  SettingsOverviewPage,
  SettingsConfigPage,
  SettingsEnginePage,
  SettingsProvidersPage,
  SettingsGitPage,
  SettingsWorkspacePage,
} from "./pages/SettingsPage";
import { ContinuityPage } from "./pages/ContinuityPage";
import { PlanPage } from "./pages/PlanPage";
import { RunsPage } from "./pages/RunsPage";
import { VerificationPage } from "./pages/VerificationPage";
import { FixPage } from "./pages/FixPage";
import { ChatPage } from "./pages/ChatPage";
import { CodebasePage } from "./pages/CodebasePage";
import { WorkspaceDashboard } from "./pages/WorkspaceDashboard";
import { WorkspaceLauncherPage } from "./pages/WorkspaceLauncherPage";
import { HostConsolePage } from "./pages/HostConsolePage";
import { DiagnosticsPage } from "./pages/DiagnosticsPage";
import { ErrorBoundary } from "./components/ErrorBoundary";
import {
  NoChatPage,
  NoPlanPage,
  NoVerificationPage,
  NoRunsPage,
  NoContinuityPage,
  NoCodebasePage,
  NoSettingsPage,
  NoHostPage,
  NoDiagnosticsPage,
} from "./pages/NoWorkspacePages";

import { LegacyPathRedirect } from "./components/LegacyPathRedirect";

export const router = createBrowserRouter([
  {
    path: "/",
    Component: Layout,
    ErrorBoundary: ErrorBoundary,
    children: [
      {
        index: true,
        Component: WorkspaceLauncherPage,
      },

      /* --- No-workspace routes (unscoped, no :workspaceId) --- */
      { path: "chat", Component: NoChatPage },
      { path: "plan", Component: NoPlanPage },
      { path: "verification", Component: NoVerificationPage },
      { path: "runs", Component: NoRunsPage },
      { path: "continuity", Component: NoContinuityPage },
      { path: "codebase", Component: NoCodebasePage },
      { path: "settings", Component: NoSettingsPage },
      { path: "host", Component: NoHostPage },
      { path: "diagnostics", Component: NoDiagnosticsPage },

      {
        path: "ws/:workspaceId/path/*",
        Component: LegacyPathRedirect,
      },
      {
        path: "ws/:workspaceId",
        Component: WorkspaceDashboard,
      },
      {
        path: "ws/:workspaceId/chat",
        Component: ChatPage,
      },
      {
        path: "ws/:workspaceId/settings",
        Component: SettingsPage,
        children: [
          { index: true, Component: SettingsOverviewPage },
          { path: "overview", Component: SettingsOverviewPage },
          { path: "workspace", Component: SettingsWorkspacePage },
          { path: "config", Component: SettingsConfigPage },
          { path: "engine", Component: SettingsEnginePage },
          { path: "providers", Component: SettingsProvidersPage },
          { path: "git", Component: SettingsGitPage },
          { path: "*", element: <Navigate to="overview" replace /> },
        ],
      },
      {
        path: "ws/:workspaceId/host",
        Component: HostConsolePage,
      },
      {
        path: "ws/:workspaceId/diagnostics",
        Component: DiagnosticsPage,
      },

      /* --- Folder-Backed Consoles (1:1 sidebar mapping) --- */

      // Verification → .aos/spec/uat/
      { path: "ws/:workspaceId/files/.aos/spec/uat", Component: VerificationPage },
      { path: "ws/:workspaceId/files/.aos/spec/uat/*", Component: VerificationPage },

      // Issues → redirects to Verification (consolidated)
      { path: "ws/:workspaceId/files/.aos/spec/issues", Component: VerificationPage },
      { path: "ws/:workspaceId/files/.aos/spec/issues/*", Component: VerificationPage },

      // Fix loop → .aos/spec/fix/
      { path: "ws/:workspaceId/files/.aos/spec/fix", Component: FixPage },
      { path: "ws/:workspaceId/files/.aos/spec/fix/*", Component: FixPage },

      // Runs → .aos/evidence/runs/
      { path: "ws/:workspaceId/files/.aos/evidence/runs", Component: RunsPage },
      { path: "ws/:workspaceId/files/.aos/evidence/runs/*", Component: RunsPage },

      // Continuity → .aos/state/
      { path: "ws/:workspaceId/files/.aos/state", Component: ContinuityPage },
      { path: "ws/:workspaceId/files/.aos/state/*", Component: ContinuityPage },

      // Codebase → .aos/codebase/
      { path: "ws/:workspaceId/files/.aos/codebase", Component: CodebasePage },
      { path: "ws/:workspaceId/files/.aos/codebase/*", Component: CodebasePage },

      // Plan → .aos/spec/ (must be LAST among spec/* routes to avoid catching uat/issues)
      { path: "ws/:workspaceId/files/.aos/spec", Component: PlanPage },
      { path: "ws/:workspaceId/files/.aos/spec/*", Component: PlanPage },

      /* --- Generic File Viewer Fallback --- */
      {
        path: "ws/:workspaceId/files/*",
        Component: WorkspacePathPage,
      },
    ],
  },
]);
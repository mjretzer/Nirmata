// Mock data representing AOS workspace state

export interface Workspace {
  repoRoot: string;
  projectName: string;
  hasAosDir: boolean;
  hasProjectSpec: boolean;
  hasRoadmap: boolean;
  hasTaskPlans: boolean;
  hasHandoff: boolean;
  cursor: {
    milestone: string;
    phase: string;
    task: string | null;
  };
  lastRun: {
    id: string;
    status: "success" | "failed" | "running";
    timestamp: string;
  };
  validation: {
    schemas: "valid" | "invalid";
    spec: "valid" | "invalid";
    state: "valid" | "invalid" | "warning";
    evidence: "valid" | "invalid";
    codebase: "valid" | "invalid";
  };
  lastValidationAt: string;
  hasStarterRoadmap?: boolean;
  openIssuesCount: number;
  openTodosCount: number;
}

export interface Phase {
  id: string;
  milestoneId: string;
  title: string;
  summary: string;
  order: number;
  status: "planned" | "in-progress" | "completed";

  brief: {
    goal: string;
    priorities: string[];
    nonGoals: string[];
    constraints: string[];
    dependencies: { type: string; ref: string; reason: string }[];
  };

  deliverables: {
    id: string;
    title: string;
    description: string;
  }[];

  acceptance: {
    criteria: string[];
    uatChecklist: string[];
  };

  links: {
    roadmapPhaseRef: string;
    tasks: string[];
    artifacts: { kind: "phase-file" | "phase-assumptions" | "phase-research" | "documentation" | "pipeline", path: string }[];
  };

  metadata: {
    tags: string[];
    owner: string;
    createdAt: string;
    updatedAt: string;
  };
}

export interface Task {
  id: string;
  phaseId: string;
  milestone: string;
  name: string;
  status: "planned" | "in-progress" | "completed" | "failed";
  assignedTo: string;
  plan?: {
    fileScope: string[];
    steps: string[];
    verification: string[];
  };
  commitHash?: string;
}

export interface Run {
  id: string;
  taskId: string | null;
  command: string;
  status: "success" | "failed" | "running";
  startTime: string;
  endTime?: string;
  artifacts: string[];
  logs: string[];
  changedFiles: string[];
}

export interface Issue {
  id: string;
  severity: "critical" | "high" | "medium" | "low";
  scope: string;
  description: string;
  repro: string[];
  linkedTasks: string[];
  status: "open" | "in-progress" | "resolved";
  impactedArea?: string;
  impactedFiles?: string[];
  expectedVsActual?: {
    expected: string;
    actual: string;
  };
  links?: {
    runId?: string;
    checklistItemId?: string;
    taskId?: string;
    phaseId?: string;
  };
  tags?: string[];
  createdFromUAT?: boolean;
  history?: {
      timestamp: string;
      event: string;
      details?: string;
  }[];
}

export interface Checkpoint {
  id: string;
  timestamp: string;
  cursor: Workspace["cursor"];
  description: string;
  source?: "manual" | "auto";
}

export interface Milestone {
  id: string;
  name: string;
  description: string;
  phases: string[];
  status: "planned" | "in-progress" | "completed";
  targetDate: string;
  definitionOfDone: string[];
}

export interface ProjectSpec {
  name: string;
  description: string;
  version: string;
  owner: string;
  repo: string;
  milestones: string[];
  createdAt: string;
  updatedAt: string;
  tags: string[];
  constraints: string[];
}

export interface TaskPlan {
  taskId: string;
  fileScope: string[];
  steps: { order: number; description: string; done: boolean }[];
  verification: { command: string; type: "automated" | "manual"; passed?: boolean }[];
  definitionOfDone: string[];
}

// Mock workspace
export const mockWorkspace: Workspace = {
  repoRoot: "/Users/dev/projects/my-app",
  projectName: "my-app",
  hasAosDir: true,
  hasProjectSpec: true,
  hasRoadmap: true,
  hasTaskPlans: true,
  hasHandoff: false,
  cursor: {
    milestone: "MS-0001",
    phase: "PH-0002",
    task: "TSK-000003",
  },
  lastRun: {
    id: "RUN-2026-02-22T143022Z",
    status: "success",
    timestamp: "2026-02-22T14:30:22Z",
  },
  validation: {
    schemas: "valid",
    spec: "valid",
    state: "valid",
    evidence: "valid",
    codebase: "valid",
  },
  lastValidationAt: "2026-02-22T14:30:22Z",
  hasStarterRoadmap: false,
  openIssuesCount: 2,
  openTodosCount: 3,
};

export interface WorkspaceSummary extends Workspace {
  id: string;
  alias?: string;
  pinned: boolean;
  isGitRepo: boolean;
  gitRemote?: string;
  gitBranch?: string;
  aheadCount?: number;
  behindCount?: number;
  gitLastSync?: string;
  status: "healthy" | "needs-init" | "invalid" | "repair-needed" | "missing-path";
  lastScanned: string;
  lastOpened: string;
}

export const mockWorkspaces: WorkspaceSummary[] = [
  {
    ...mockWorkspace,
    id: "WS-001",
    alias: "Main App",
    pinned: true,
    isGitRepo: true,
    gitRemote: "github.com/dev/my-app",
    gitBranch: "main",
    aheadCount: 0,
    behindCount: 2,
    gitLastSync: "2026-02-23T08:15:00Z",
    status: "healthy",
    lastScanned: "2026-02-23T10:00:00Z",
    lastOpened: "2026-02-23T10:15:00Z",
  },
  {
    id: "WS-002",
    repoRoot: "/Users/dev/projects/client-site",
    projectName: "client-site",
    hasAosDir: false,
    hasProjectSpec: false,
    hasRoadmap: false,
    hasTaskPlans: false,
    hasHandoff: false,
    cursor: { milestone: "", phase: "", task: null },
    lastRun: { id: "", status: "running", timestamp: "" },
    validation: { schemas: "valid", spec: "valid", state: "valid", evidence: "valid", codebase: "valid" },
    lastValidationAt: "",
    openIssuesCount: 0,
    openTodosCount: 0,
    pinned: false,
    isGitRepo: true,
    gitRemote: "github.com/client/client-site",
    gitBranch: "develop",
    aheadCount: 3,
    behindCount: 0,
    gitLastSync: "2026-02-22T12:00:00Z",
    status: "needs-init",
    lastScanned: "2026-02-22T14:30:00Z",
    lastOpened: "2026-02-22T14:30:00Z",
  },
  {
    id: "WS-003",
    repoRoot: "/Users/dev/experiments/test-lab",
    projectName: "test-lab",
    hasAosDir: true,
    hasProjectSpec: true,
    hasRoadmap: true,
    hasTaskPlans: false,
    hasHandoff: false,
    cursor: { milestone: "MS-0001", phase: "PH-0001", task: "TSK-000003" },
    lastRun: { id: "RUN-9921", status: "failed", timestamp: "2026-02-21T09:15:00Z" },
    validation: { schemas: "invalid", spec: "valid", state: "warning", evidence: "valid", codebase: "valid" },
    lastValidationAt: "2026-02-21T09:15:00Z",
    openIssuesCount: 3,
    openTodosCount: 5,
    pinned: true,
    isGitRepo: true,
    gitBranch: "feature/phase-2",
    aheadCount: 5,
    behindCount: 0,
    status: "invalid",
    lastScanned: "2026-02-23T08:45:00Z",
    lastOpened: "2026-02-23T08:45:00Z",
  },
  {
     id: "WS-004",
     repoRoot: "/Users/dev/old/legacy-app",
     projectName: "legacy-app",
     hasAosDir: true,
     hasProjectSpec: true,
     hasRoadmap: false,
     hasTaskPlans: false,
     hasHandoff: false,
     cursor: { milestone: "", phase: "", task: null },
     lastRun: { id: "", status: "success", timestamp: "" },
     validation: { schemas: "valid", spec: "valid", state: "valid", evidence: "valid", codebase: "valid" },
     lastValidationAt: "2025-12-01T08:00:00Z",
     openIssuesCount: 0,
     openTodosCount: 0,
     pinned: false,
     isGitRepo: false,
     status: "missing-path",
     lastScanned: "2025-12-01T08:00:00Z",
     lastOpened: "2025-12-01T08:00:00Z",
  }
];

// Mock phases
export const mockPhases: Phase[] = [
  {
    id: "PH-0001",
    milestoneId: "MS-0001",
    title: "Plan Page UI + Spec Folder Integration",
    summary: "Define and implement the Plan page UX that opens the spec folder in the file explorer and shows plan-specific details in the right panel.",
    order: 1,
    status: "completed",

    brief: {
      goal: "Make the Plan page feel like an IDE-native planning surface: file-first navigation + structured plan view.",
      priorities: [
        "File explorer remains visible and functional",
        "Plan button auto-focuses spec folder",
        "Right panel shows plan artifacts + editing affordances",
      ],
      nonGoals: [
        "No run/verification UX changes in this phase",
        "No new orchestration logic beyond UI wiring",
      ],
      constraints: [
        "All edits must map to deterministic .aos/ spec artifacts",
        "UI must be usable without referring to run history",
      ],
      dependencies: [
        { type: "artifact", ref: ".aos/spec/roadmap.json", reason: "Phase must remain aligned to roadmap ordering" },
      ],
    },

    deliverables: [
      {
        id: "DEL-1",
        title: "Plan page detail panel",
        description: "Right-side panel renders phase overview + linked task plans + quick actions.",
      },
      {
        id: "DEL-2",
        title: "File explorer auto-focus",
        description: "Selecting Plan highlights/opens the spec folder path in the explorer.",
      },
    ],

    acceptance: {
      criteria: [
        "Clicking Plan opens the spec folder in the file explorer",
        "Plan page shows phase brief + deliverables + linked tasks",
        "User can open/edit plan-related JSON from the UI without losing orientation",
      ],
      uatChecklist: [
        "Open workspace → click Plan → explorer focuses spec/",
        "Select PH-0001 phase file → details render correctly",
        "Open a linked task plan from the phase view → correct file opens",
      ],
    },

    links: {
      roadmapPhaseRef: ".aos/spec/roadmap.json#/milestones/MS-0001/phases/0",
      tasks: ["TSK-000001", "TSK-000002"],
      artifacts: [
        { kind: "phase-file", path: ".aos/spec/phases/PH-0001/phase.json" },
        { kind: "phase-assumptions", path: ".aos/spec/phases/PH-0001/assumptions.json" },
        { kind: "phase-research", path: ".aos/spec/phases/PH-0001/research.json" },
      ],
    },

    metadata: {
      tags: ["ui", "ide", "plan", "file-first"],
      owner: "product",
      createdAt: "2026-02-25T00:00:00Z",
      updatedAt: "2026-02-25T00:00:00Z",
    },
  },
  {
    id: "PH-0002",
    milestoneId: "MS-0001",
    title: "User Authentication",
    summary: "Implement secure user authentication, support OAuth providers, and manage sessions.",
    order: 2,
    status: "in-progress",

    brief: {
      goal: "Enable secure user authentication for the application.",
      priorities: [
        "OAuth provider integration",
        "Session management",
        "Login UI components",
      ],
      nonGoals: [
        "Detailed UI design",
        "Backend implementation",
      ],
      constraints: [
        "OAuth credentials will be provided",
        "Password hashing library approved",
      ],
      dependencies: [
        { type: "project", ref: "MS-0001", reason: "Project milestone" },
      ],
    },

    deliverables: [
      {
        id: "DEL-0004",
        title: "Users can sign up/login",
        description: "Implement user registration and login functionality.",
      },
      {
        id: "DEL-0005",
        title: "OAuth integration with Google/GitHub",
        description: "Integrate OAuth providers for user authentication.",
      },
      {
        id: "DEL-0006",
        title: "Secure session handling",
        description: "Implement secure session management for user authentication.",
      },
    ],

    acceptance: {
      criteria: [
        "Users can sign up and login successfully",
        "OAuth integration is functional with Google and GitHub",
        "Session management is secure and reliable",
      ],
      uatChecklist: [
        "User registration and login are tested",
        "OAuth integration is verified",
        "Session management is tested",
      ],
    },

    links: {
      roadmapPhaseRef: "RM-PH-0002",
      tasks: ["TSK-000003", "TSK-000004"],
      artifacts: [
        { kind: "documentation", path: "docs/auth.md" },
        { kind: "pipeline", path: ".github/workflows/ci.yml" },
      ],
    },

    metadata: {
      tags: ["auth", "security"],
      owner: "Executor-Alpha",
      createdAt: "2026-02-15T09:00:00Z",
      updatedAt: "2026-02-22T14:35:18Z",
    },
  },
  {
    id: "PH-0003",
    milestoneId: "MS-0001",
    title: "Dashboard UI",
    summary: "Build the main dashboard interface, implement data visualization, and ensure responsive design.",
    order: 3,
    status: "planned",

    brief: {
      goal: "Create a user-friendly dashboard for the application.",
      priorities: [
        "Dashboard layout",
        "Data visualization",
        "Responsive design",
      ],
      nonGoals: [
        "Detailed UI design",
        "Backend implementation",
      ],
      constraints: [
        "Design mockups finalized",
        "API endpoints ready",
      ],
      dependencies: [
        { type: "project", ref: "MS-0001", reason: "Project milestone" },
      ],
    },

    deliverables: [
      {
        id: "DEL-0007",
        title: "Dashboard displays user metrics",
        description: "Implement the dashboard to display relevant user metrics.",
      },
      {
        id: "DEL-0008",
        title: "Charts render correctly",
        description: "Integrate chart library for data visualization.",
      },
      {
        id: "DEL-0009",
        title: "Mobile-responsive layout",
        description: "Ensure the dashboard is responsive across different devices.",
      },
    ],

    acceptance: {
      criteria: [
        "Dashboard displays user metrics correctly",
        "Charts render accurately and are interactive",
        "Dashboard is responsive on mobile, tablet, and desktop",
      ],
      uatChecklist: [
        "Dashboard metrics are tested",
        "Charts are tested",
        "Responsive design is verified",
      ],
    },

    links: {
      roadmapPhaseRef: "RM-PH-0003",
      tasks: ["TSK-000005"],
      artifacts: [
        { kind: "documentation", path: "docs/dashboard.md" },
        { kind: "pipeline", path: ".github/workflows/ci.yml" },
      ],
    },

    metadata: {
      tags: ["ui", "dashboard"],
      owner: "Executor-Alpha",
      createdAt: "2026-02-15T09:00:00Z",
      updatedAt: "2026-02-22T14:35:18Z",
    },
  },
  {
    id: "PH-0004",
    milestoneId: "MS-0002",
    title: "Performance Optimization",
    summary: "Audit and optimize bundle size, rendering performance, and core web vitals across all pages.",
    order: 4,
    status: "planned",

    brief: {
      goal: "Achieve sub-2s LCP and sub-100ms FID on all critical paths.",
      priorities: [
        "Bundle splitting and lazy loading",
        "Image optimization pipeline",
        "Render performance profiling",
      ],
      nonGoals: [
        "No new feature work",
        "No backend performance changes",
      ],
      constraints: [
        "Bundle size must stay under 500KB gzipped",
        "No regressions in existing functionality",
      ],
      dependencies: [
        { type: "milestone", ref: "MS-0001", reason: "MVP must be complete before optimization" },
      ],
    },

    deliverables: [
      {
        id: "DEL-0010",
        title: "Bundle analysis report",
        description: "Generate and review bundle size analysis with actionable recommendations.",
      },
      {
        id: "DEL-0011",
        title: "Optimized build configuration",
        description: "Implement code splitting, tree shaking, and lazy loading for all routes.",
      },
    ],

    acceptance: {
      criteria: [
        "LCP < 2s on 3G throttled connection",
        "FID < 100ms on mid-range devices",
        "Bundle size reduced by at least 20%",
      ],
      uatChecklist: [
        "Lighthouse audit scores ≥ 90",
        "No layout shifts visible during load",
        "All routes load within budget",
      ],
    },

    links: {
      roadmapPhaseRef: "RM-PH-0004",
      tasks: ["TSK-000006", "TSK-000007"],
      artifacts: [
        { kind: "documentation", path: "docs/performance.md" },
      ],
    },

    metadata: {
      tags: ["performance", "optimization"],
      owner: "Executor-Alpha",
      createdAt: "2026-02-25T09:00:00Z",
      updatedAt: "2026-02-25T09:00:00Z",
    },
  },
  {
    id: "PH-0005",
    milestoneId: "MS-0002",
    title: "Analytics Integration",
    summary: "Integrate event tracking, user analytics, and build a real-time analytics dashboard for product insights.",
    order: 5,
    status: "planned",

    brief: {
      goal: "Provide actionable product analytics for data-driven decisions.",
      priorities: [
        "Event tracking infrastructure",
        "User behavior analytics",
        "Analytics dashboard",
      ],
      nonGoals: [
        "Marketing analytics",
        "A/B testing framework",
      ],
      constraints: [
        "GDPR-compliant data collection",
        "No third-party cookies",
      ],
      dependencies: [
        { type: "phase", ref: "PH-0004", reason: "Performance baseline needed before adding tracking" },
      ],
    },

    deliverables: [
      {
        id: "DEL-0012",
        title: "Event tracking SDK",
        description: "Lightweight SDK for tracking user events without performance impact.",
      },
      {
        id: "DEL-0013",
        title: "Analytics dashboard page",
        description: "Internal dashboard showing key product metrics and user behavior.",
      },
    ],

    acceptance: {
      criteria: [
        "Events tracked with < 5ms overhead per event",
        "Analytics dashboard shows real-time data",
        "Data collection is GDPR-compliant",
      ],
      uatChecklist: [
        "Event tracking verified on all critical flows",
        "Dashboard loads within 2s",
        "Consent banner works correctly",
      ],
    },

    links: {
      roadmapPhaseRef: "RM-PH-0005",
      tasks: ["TSK-000008"],
      artifacts: [
        { kind: "documentation", path: "docs/analytics.md" },
      ],
    },

    metadata: {
      tags: ["analytics", "tracking"],
      owner: "Executor-Beta",
      createdAt: "2026-02-25T09:00:00Z",
      updatedAt: "2026-02-25T09:00:00Z",
    },
  },
  {
    id: "PH-0006",
    milestoneId: "MS-0003",
    title: "Horizontal Scaling",
    summary: "Implement horizontal scaling, multi-region deployment, and automated failover for high availability.",
    order: 6,
    status: "planned",

    brief: {
      goal: "Ensure the application can handle increased load and maintain high availability.",
      priorities: [
        "Horizontal scaling architecture",
        "Multi-region deployment",
        "Automated failover",
      ],
      nonGoals: [
        "Detailed UI design",
        "Backend implementation",
      ],
      constraints: [
        "Current infrastructure supports scaling",
        "No regressions in existing functionality",
      ],
      dependencies: [
        { type: "milestone", ref: "MS-0002", reason: "Post-MVP enhancements must be complete before scaling" },
      ],
    },

    deliverables: [
      {
        id: "DEL-0014",
        title: "Scaling architecture design",
        description: "Design and document the scaling architecture.",
      },
      {
        id: "DEL-0015",
        title: "Multi-region deployment setup",
        description: "Set up multi-region deployment for high availability.",
      },
      {
        id: "DEL-0016",
        title: "Automated failover implementation",
        description: "Implement automated failover for critical services.",
      },
    ],

    acceptance: {
      criteria: [
        "Auto-scaling validated under 10x load",
        "Multi-region failover tested",
        "99.9% uptime SLA documented and monitored",
        "Disaster recovery runbook complete",
      ],
      uatChecklist: [
        "Load testing under 10x load",
        "Failover testing in multi-region setup",
        "Uptime SLA monitoring in place",
        "Disaster recovery runbook reviewed",
      ],
    },

    links: {
      roadmapPhaseRef: "RM-PH-0006",
      tasks: ["TSK-000009", "TSK-000010"],
      artifacts: [
        { kind: "documentation", path: "docs/scaling.md" },
      ],
    },

    metadata: {
      tags: ["scaling", "availability"],
      owner: "Executor-Alpha",
      createdAt: "2026-02-25T09:00:00Z",
      updatedAt: "2026-02-25T09:00:00Z",
    },
  },
];

// Mock tasks
export const mockTasks: Task[] = [
  {
    id: "TSK-000001",
    phaseId: "PH-0001",
    milestone: "MS-0001",
    name: "Implement right-side plan detail panel",
    status: "completed",
    assignedTo: "Executor-Alpha",
    plan: {
      fileScope: ["src/app/pages/PlanPage.tsx", "src/app/components/PlanDetailPanel.tsx"],
      steps: [
        "Create PlanDetailPanel component",
        "Wire up phase details to right panel",
      ],
      verification: ["npm run build", "npm run lint"],
    },
    commitHash: "a3f5e7c",
  },
  {
    id: "TSK-000002",
    phaseId: "PH-0001",
    milestone: "MS-0001",
    name: "Wire up file explorer auto-focus",
    status: "completed",
    assignedTo: "Executor-Beta",
    plan: {
      fileScope: ["src/app/components/FileExplorer.tsx"],
      steps: [
        "Add auto-focus logic to FileExplorer",
        "Trigger focus when Plan page is mounted",
      ],
      verification: ["Test auto-focus behavior in browser"],
    },
    commitHash: "b8d2f1a",
  },
  {
    id: "TSK-000003",
    phaseId: "PH-0002",
    milestone: "MS-0001",
    name: "Implement OAuth login flow",
    status: "in-progress",
    assignedTo: "Executor-Alpha",
    plan: {
      fileScope: ["src/auth/", "src/components/LoginButton.tsx"],
      steps: [
        "Create OAuth provider integration",
        "Build login UI components",
        "Handle callback and token storage",
      ],
      verification: [
        "npm run test:auth",
        "Manual test: login with Google",
        "Manual test: login with GitHub",
      ],
    },
  },
  {
    id: "TSK-000004",
    phaseId: "PH-0002",
    milestone: "MS-0001",
    name: "Session management middleware",
    status: "planned",
    assignedTo: "Executor-Beta",
  },
  {
    id: "TSK-000005",
    phaseId: "PH-0003",
    milestone: "MS-0001",
    name: "Build dashboard layout",
    status: "planned",
    assignedTo: "Executor-Alpha",
  },
  {
    id: "TSK-000006",
    phaseId: "PH-0004",
    milestone: "MS-0002",
    name: "Analyze bundle size",
    status: "planned",
    assignedTo: "Executor-Alpha",
  },
  {
    id: "TSK-000007",
    phaseId: "PH-0004",
    milestone: "MS-0002",
    name: "Implement lazy loading",
    status: "planned",
    assignedTo: "Executor-Alpha",
  },
  {
    id: "TSK-000008",
    phaseId: "PH-0005",
    milestone: "MS-0002",
    name: "Set up event tracking",
    status: "planned",
    assignedTo: "Executor-Beta",
  },
  {
    id: "TSK-000009",
    phaseId: "PH-0006",
    milestone: "MS-0003",
    name: "Design scaling architecture",
    status: "planned",
    assignedTo: "Executor-Alpha",
  },
  {
    id: "TSK-000010",
    phaseId: "PH-0006",
    milestone: "MS-0003",
    name: "Implement multi-region deployment",
    status: "planned",
    assignedTo: "Executor-Alpha",
  },
];

// Mock runs
export const mockRuns: Run[] = [
  {
    id: "RUN-2026-02-22T143022Z",
    taskId: "TSK-000012",
    command: "aos execute-plan TSK-000012",
    status: "success",
    startTime: "2026-02-22T14:30:22Z",
    endTime: "2026-02-22T14:35:18Z",
    artifacts: [
      ".aos/evidence/runs/RUN-2026-02-22T143022Z/artifacts.json",
      ".aos/evidence/tasks/TSK-000012/latest.json",
    ],
    logs: [
      ".aos/evidence/runs/RUN-2026-02-22T143022Z/logs/stdout.log",
      ".aos/evidence/runs/RUN-2026-02-22T143022Z/logs/stderr.log",
    ],
    changedFiles: [
      "src/auth/OAuthProvider.ts",
      "src/components/LoginButton.tsx",
      "src/utils/tokenStorage.ts",
    ],
  },
  {
    id: "RUN-2026-02-22T112033Z",
    taskId: "TSK-000009",
    command: "aos execute-plan TSK-000009",
    status: "success",
    startTime: "2026-02-22T11:20:33Z",
    endTime: "2026-02-22T11:24:11Z",
    artifacts: [
      ".aos/evidence/runs/RUN-2026-02-22T112033Z/artifacts.json",
      ".aos/evidence/tasks/TSK-000009/latest.json",
    ],
    logs: [".aos/evidence/runs/RUN-2026-02-22T112033Z/logs/stdout.log"],
    changedFiles: [
      ".github/workflows/ci.yml",
      ".github/workflows/deploy.yml",
      "deploy.sh",
    ],
  },
  {
    id: "RUN-2026-02-22T094511Z",
    taskId: null,
    command: "aos validate --all",
    status: "success",
    startTime: "2026-02-22T09:45:11Z",
    endTime: "2026-02-22T09:45:14Z",
    artifacts: [".aos/evidence/runs/RUN-2026-02-22T094511Z/validation-report.json"],
    logs: [".aos/evidence/runs/RUN-2026-02-22T094511Z/logs/stdout.log"],
    changedFiles: [],
  },
];

// Mock issues
export const mockIssues: Issue[] = [
  {
    id: "ISS-0001",
    severity: "high",
    scope: "PH-0002",
    description: "OAuth callback fails on mobile Safari",
    repro: [
      "Open app on iPhone Safari",
      "Click 'Login with Google'",
      "Observe redirect loop",
    ],
    linkedTasks: ["TSK-000003"],
    status: "open",
    impactedArea: "Auth Module",
    impactedFiles: ["src/auth/OAuthProvider.ts"],
    expectedVsActual: {
        expected: "Redirect to dashboard",
        actual: "Infinite redirect loop"
    },
    links: {
        taskId: "TSK-000003",
        phaseId: "PH-0002"
    },
    tags: ["bug", "mobile"],
    createdFromUAT: false,
    history: [
        { timestamp: "2026-02-22T10:00:00Z", event: "Issue Created" }
    ]
  },
  {
    id: "ISS-0002",
    severity: "medium",
    scope: "TSK-000008",
    description: "Build warnings for unused imports",
    repro: ["Run 'npm run build'", "Observe warning messages"],
    linkedTasks: ["TSK-000008"],
    status: "resolved",
    impactedArea: "Build Config",
    createdFromUAT: true,
    history: [
        { timestamp: "2026-02-21T15:00:00Z", event: "Issue Created" },
        { timestamp: "2026-02-21T16:00:00Z", event: "Resolved", details: "Fixed in commit 8f2d1a" }
    ]
  },
];

// Mock checkpoints
export const mockCheckpoints: Checkpoint[] = [
  {
    id: "CHK-001",
    timestamp: "2026-02-22T10:00:00Z",
    cursor: {
      milestone: "MS-0001",
      phase: "PH-0002",
      task: "TSK-000011",
    },
    description: "Before starting OAuth integration",
    source: "manual"
  },
  {
    id: "CHK-002",
    timestamp: "2026-02-21T16:30:00Z",
    cursor: {
      milestone: "MS-0001",
      phase: "PH-0001",
      task: "TSK-000009",
    },
    description: "Completed project setup phase",
    source: "auto"
  },
];

// Mock milestones
export const mockMilestones: Milestone[] = [
  {
    id: "MS-0001",
    name: "MVP Release",
    description: "Deliver a working MVP with authentication, basic dashboard, and CI/CD pipeline. This is the first externally visible release.",
    phases: ["PH-0001", "PH-0002", "PH-0003"],
    status: "in-progress",
    targetDate: "2026-03-15",
    definitionOfDone: [
      "All phases completed and verified",
      "Zero open critical/high issues",
      "UAT pass rate ≥ 95%",
      "CI/CD pipeline green on main branch",
      "Deployed to staging environment",
    ],
  },
  {
    id: "MS-0002",
    name: "Post-MVP Enhancements",
    description: "Performance optimization, analytics integration, and advanced user management features for production readiness.",
    phases: ["PH-0004", "PH-0005"],
    status: "planned",
    targetDate: "2026-06-01",
    definitionOfDone: [
      "All enhancement phases completed and verified",
      "Performance benchmarks met (LCP < 2s, FID < 100ms)",
      "Analytics pipeline operational",
      "Zero open critical issues",
    ],
  },
  {
    id: "MS-0003",
    name: "Scale & Reliability",
    description: "Horizontal scaling, multi-region deployment, automated failover, and 99.9% uptime SLA compliance.",
    phases: ["PH-0006"],
    status: "planned",
    targetDate: "2026-09-01",
    definitionOfDone: [
      "Auto-scaling validated under 10x load",
      "Multi-region failover tested",
      "99.9% uptime SLA documented and monitored",
      "Disaster recovery runbook complete",
    ],
  },
];

// Mock project spec
export const mockProjectSpec: ProjectSpec = {
  name: "my-app",
  description: "A modern web application with secure authentication, real-time dashboard, and automated deployment pipeline. Built with React, TypeScript, and Vite.",
  version: "0.1.0",
  owner: "Executor-Alpha",
  repo: "github.com/team/my-app",
  milestones: ["MS-0001"],
  createdAt: "2026-02-15T09:00:00Z",
  updatedAt: "2026-02-22T14:35:18Z",
  tags: ["web-app", "react", "typescript", "authentication", "dashboard"],
  constraints: [
    "No external CSS frameworks besides Tailwind",
    "All API calls must go through typed service layer",
    "Authentication must support SSO via OAuth 2.0",
    "Bundle size must stay under 500KB gzipped",
    "All components must have UAT coverage",
  ],
};

// Mock task plans (expanded)
export const mockTaskPlans: TaskPlan[] = [
  {
    taskId: "TSK-000003",
    fileScope: ["src/auth/OAuthProvider.ts", "src/auth/types.ts", "src/components/LoginButton.tsx", "src/utils/tokenStorage.ts"],
    steps: [
      { order: 1, description: "Create OAuth provider integration module", done: true },
      { order: 2, description: "Build login UI components with loading states", done: true },
      { order: 3, description: "Handle callback URL and token storage", done: false },
      { order: 4, description: "Add error boundary for auth failures", done: false },
    ],
    verification: [
      { command: "npm run test:auth", type: "automated", passed: false },
      { command: "Manual test: login with Google", type: "manual", passed: false },
      { command: "Manual test: login with GitHub", type: "manual" },
      { command: "Manual test: token refresh flow", type: "manual" },
    ],
    definitionOfDone: [
      "User can sign in with Google OAuth",
      "User can sign in with GitHub OAuth",
      "Tokens are securely stored and refreshed",
      "Auth errors show user-friendly messages",
    ],
  },
  {
    taskId: "TSK-000008",
    fileScope: ["package.json", "vite.config.ts", "src/", "tsconfig.json"],
    steps: [
      { order: 1, description: "Initialize Vite project with React template", done: true },
      { order: 2, description: "Configure TypeScript strict mode and path aliases", done: true },
      { order: 3, description: "Set up ESLint + Prettier with team config", done: true },
    ],
    verification: [
      { command: "npm run build", type: "automated", passed: true },
      { command: "npm run lint", type: "automated", passed: true },
      { command: "npm run typecheck", type: "automated", passed: true },
    ],
    definitionOfDone: [
      "Project builds without errors",
      "Linting passes with zero warnings",
      "TypeScript strict mode enabled",
    ],
  },
  {
    taskId: "TSK-000009",
    fileScope: [".github/workflows/ci.yml", ".github/workflows/deploy.yml", "deploy.sh"],
    steps: [
      { order: 1, description: "Create GitHub Actions CI workflow", done: true },
      { order: 2, description: "Create deploy workflow with staging/prod targets", done: true },
      { order: 3, description: "Write deploy.sh helper script", done: true },
    ],
    verification: [
      { command: "Test workflow triggers on push", type: "manual", passed: true },
      { command: "Verify deploy to staging succeeds", type: "manual", passed: true },
    ],
    definitionOfDone: [
      "CI runs on every push to main",
      "Deploy workflow works for staging",
      "deploy.sh has usage documentation",
    ],
  },
  {
    taskId: "TSK-000012",
    fileScope: ["src/auth/OAuthProvider.ts", "src/auth/types.ts", "src/components/LoginButton.tsx", "src/utils/tokenStorage.ts"],
    steps: [
      { order: 1, description: "Create OAuth provider integration module", done: true },
      { order: 2, description: "Build login UI components with loading states", done: false },
      { order: 3, description: "Handle callback URL and token storage", done: false },
      { order: 4, description: "Add error boundary for auth failures", done: false },
    ],
    verification: [
      { command: "npm run test:auth", type: "automated", passed: false },
      { command: "Manual test: login with Google", type: "manual" },
      { command: "Manual test: login with GitHub", type: "manual" },
      { command: "Manual test: token refresh flow", type: "manual" },
    ],
    definitionOfDone: [
      "User can sign in with Google OAuth",
      "User can sign in with GitHub OAuth",
      "Tokens are securely stored and refreshed",
      "Auth errors show user-friendly messages",
    ],
  },
  {
    taskId: "TSK-000013",
    fileScope: ["src/middleware/session.ts", "src/auth/sessionStore.ts"],
    steps: [
      { order: 1, description: "Design session storage interface", done: false },
      { order: 2, description: "Implement session middleware", done: false },
      { order: 3, description: "Add session expiry and refresh logic", done: false },
    ],
    verification: [
      { command: "npm run test:session", type: "automated" },
      { command: "Manual test: session persists on refresh", type: "manual" },
    ],
    definitionOfDone: [
      "Sessions persist across page reloads",
      "Expired sessions redirect to login",
      "Session data is encrypted at rest",
    ],
  },
  {
    taskId: "TSK-000014",
    fileScope: ["src/pages/Dashboard.tsx", "src/components/MetricsCard.tsx", "src/components/ChartWidget.tsx"],
    steps: [
      { order: 1, description: "Create responsive dashboard grid layout", done: false },
      { order: 2, description: "Build MetricsCard component with variants", done: false },
      { order: 3, description: "Integrate chart library for data visualization", done: false },
    ],
    verification: [
      { command: "npm run test:ui", type: "automated" },
      { command: "Visual regression test on 3 viewports", type: "manual" },
    ],
    definitionOfDone: [
      "Dashboard renders on mobile, tablet, desktop",
      "Metrics cards show real-time data",
      "Charts are interactive and accessible",
    ],
  },
  {
    taskId: "TSK-000015",
    fileScope: ["src/index.tsx", "src/utils/bundleAnalyzer.ts"],
    steps: [
      { order: 1, description: "Install and configure Webpack Bundle Analyzer", done: false },
      { order: 2, description: "Run analysis on production build", done: false },
      { order: 3, description: "Review and document findings", done: false },
    ],
    verification: [
      { command: "npm run analyze", type: "automated" },
      { command: "Manual review of report", type: "manual" },
    ],
    definitionOfDone: [
      "Bundle size analysis report generated",
      "Actionable recommendations documented",
      "Report included in project documentation",
    ],
  },
  {
    taskId: "TSK-000016",
    fileScope: ["src/index.tsx", "src/utils/lazyLoad.ts"],
    steps: [
      { order: 1, description: "Implement lazy loading for non-critical routes", done: false },
      { order: 2, description: "Test lazy loading in development and production", done: false },
      { order: 3, description: "Measure and document performance improvements", done: false },
    ],
    verification: [
      { command: "npm run test:performance", type: "automated" },
      { command: "Manual test: lazy loading behavior", type: "manual" },
    ],
    definitionOfDone: [
      "Lazy loading implemented for non-critical routes",
      "Performance improvements documented",
      "Lazy loading verified in both dev and prod",
    ],
  },
  {
    taskId: "TSK-000017",
    fileScope: ["src/index.tsx", "src/utils/eventTracker.ts"],
    steps: [
      { order: 1, description: "Create lightweight event tracking SDK", done: false },
      { order: 2, description: "Integrate SDK with critical user flows", done: false },
      { order: 3, description: "Test event tracking in development and production", done: false },
    ],
    verification: [
      { command: "npm run test:tracking", type: "automated" },
      { command: "Manual test: event tracking behavior", type: "manual" },
    ],
    definitionOfDone: [
      "Event tracking SDK implemented",
      "SDK integrated with critical flows",
      "Event tracking verified in both dev and prod",
    ],
  },
  {
    taskId: "TSK-000018",
    fileScope: ["src/index.tsx", "src/utils/scaling.ts"],
    steps: [
      { order: 1, description: "Design horizontal scaling architecture", done: false },
      { order: 2, description: "Implement scaling logic in application", done: false },
      { order: 3, description: "Test scaling under 10x load", done: false },
    ],
    verification: [
      { command: "npm run test:scaling", type: "automated" },
      { command: "Manual test: scaling behavior", type: "manual" },
    ],
    definitionOfDone: [
      "Scaling architecture designed and documented",
      "Scaling logic implemented in application",
      "Scaling validated under 10x load",
    ],
  },
  {
    taskId: "TSK-000019",
    fileScope: ["src/index.tsx", "src/utils/deployment.ts"],
    steps: [
      { order: 1, description: "Set up multi-region deployment", done: false },
      { order: 2, description: "Implement automated failover", done: false },
      { order: 3, description: "Test failover in multi-region setup", done: false },
    ],
    verification: [
      { command: "npm run test:deployment", type: "automated" },
      { command: "Manual test: failover behavior", type: "manual" },
    ],
    definitionOfDone: [
      "Multi-region deployment set up",
      "Automated failover implemented",
      "Failover tested in multi-region setup",
    ],
  },
];

// Pruned: PhaseAssumptions, PhaseResearch, mockCodebaseData, UATLibrary, mockUATLibraries,
// mockPhaseAssumptions, mockPhaseResearch — zero consumers remain after hook migration.
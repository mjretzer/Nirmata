import { mockRuns, mockTasks, mockPhases, mockIssues, mockWorkspace } from "./mockData";

export interface FileSystemNode {
    id: string;
    name: string;
    type: "directory" | "file";
    children?: FileSystemNode[];
    status?: "valid" | "warning" | "error";
    meta?: any;
}

// Convert mock data arrays into the file system structure
const runsChildren: FileSystemNode[] = mockRuns.map(run => ({
    id: `run-${run.id}`,
    name: run.id,
    type: "directory",
    status: run.status === "success" ? "valid" : "error",
    children: [
        { id: `run-${run.id}-run-json`, name: "run.json", type: "file" },
        { id: `run-${run.id}-commands`, name: "commands.ndjson", type: "file" },
        {
            id: `run-${run.id}-logs`,
            name: "logs",
            type: "directory",
            children: [
                { id: `run-${run.id}-build-log`, name: "build.log", type: "file" },
                { id: `run-${run.id}-test-log`, name: "test.log", type: "file" }
            ]
        },
        {
            id: `run-${run.id}-artifacts`,
            name: "artifacts",
            type: "directory",
            children: run.artifacts.map((art, i) => ({
                id: `run-${run.id}-art-${i}`,
                name: art.split('/').pop() || art,
                type: "file"
            }))
        }
    ]
}));

const tasksChildren: FileSystemNode[] = mockTasks.map(task => ({
    id: `task-${task.id}`,
    name: task.id,
    type: "directory",
    status: task.status === "completed" ? "valid" : "warning",
    children: [
        { id: `task-${task.id}-task`, name: "task.json", type: "file" },
        { id: `task-${task.id}-plan`, name: "plan.json", type: "file" },
        { id: `task-${task.id}-uat`, name: "uat.json", type: "file" },
        { id: `task-${task.id}-links`, name: "links.json", type: "file" }
    ]
}));

const taskEvidenceChildren: FileSystemNode[] = mockTasks.map(task => ({
    id: `task-ev-${task.id}`,
    name: task.id,
    type: "directory",
    children: [
        { id: `task-ev-${task.id}-index`, name: "index.json", type: "file" },
        {
            id: `task-ev-${task.id}-runs`,
            name: "runs",
            type: "directory",
            children: [
                { id: `task-ev-${task.id}-run-ref`, name: "RUN-2026-01-13T021500Z.json", type: "file" }
            ]
        },
        { id: `task-ev-${task.id}-logs`, name: "logs", type: "directory", children: [] },
        { id: `task-ev-${task.id}-artifacts`, name: "artifacts", type: "directory", children: [] }
    ]
}));

const phasesChildren: FileSystemNode[] = mockPhases.map(phase => ({
    id: `phase-${phase.id}`,
    name: phase.id,
    type: "directory",
    status: phase.status === "completed" ? "valid" : "warning",
    children: [
        { id: `phase-${phase.id}-phase`, name: "phase.json", type: "file" },
        { id: `phase-${phase.id}-assumptions`, name: "assumptions.json", type: "file" },
        { id: `phase-${phase.id}-research`, name: "research.json", type: "file" }
    ]
}));

// Issues are flat files in the new structure under .aos/spec/issues/
const issuesChildren: FileSystemNode[] = mockIssues.map(issue => ({
    id: `issue-${issue.id}`,
    name: `${issue.id}.json`,
    type: "file",
    status: issue.severity === "high" ? "error" : "valid"
}));

// Milestones
const milestonesChildren: FileSystemNode[] = [
    {
        id: "ms-0001",
        name: "MS-0001",
        type: "directory",
        children: [
            { id: "ms-0001-file", name: "milestone.json", type: "file" }
        ]
    }
];

// UAT
const uatChildren: FileSystemNode[] = [
    { id: "uat-index", name: "index.json", type: "file" },
    // Generate one UAT file per task, matching the UAT-XXXXXX.json naming shown in the table
    ...mockTasks.map(task => ({
        id: `uat-${task.id.toLowerCase()}`,
        name: `${task.id.replace("TSK", "UAT")}.json`,
        type: "file" as const,
        status: task.status === "completed" ? "valid" as const : task.status === "failed" ? "error" as const : "warning" as const
    }))
];

// Context Packs
const contextPacksChildren: FileSystemNode[] = [
    { id: "pack-tsk-001", name: "TSK-000001.json", type: "file" },
    { id: "pack-ph-001", name: "PH-0001.json", type: "file" }
];

export const mockFileSystem: FileSystemNode[] = [
    {
        id: "root-aos",
        name: ".aos",
        type: "directory",
        children: [
            { id: "config", name: "config.json", type: "file" },
            { id: "last-run", name: "last-run.json", type: "file" },
            
            {
                id: "schemas",
                name: "schemas",
                type: "directory",
                children: [
                    { id: "schema-project", name: "project.schema.json", type: "file" },
                    { id: "schema-roadmap", name: "roadmap.schema.json", type: "file" },
                    { id: "schema-milestone", name: "milestone.schema.json", type: "file" },
                    { id: "schema-phase", name: "phase.schema.json", type: "file" },
                    { id: "schema-task", name: "task.schema.json", type: "file" },
                    { id: "schema-issue", name: "issue.schema.json", type: "file" },
                    { id: "schema-uat", name: "uat.schema.json", type: "file" },
                    { id: "schema-event", name: "event.schema.json", type: "file" },
                    { id: "schema-cpack", name: "context-pack.schema.json", type: "file" },
                    { id: "schema-evidence", name: "evidence.schema.json", type: "file" }
                ]
            },
            
            {
                id: "spec",
                name: "spec",
                type: "directory",
                children: [
                    { id: "spec-project", name: "project.json", type: "file" },
                    { id: "spec-roadmap", name: "roadmap.json", type: "file" },
                    
                    {
                        id: "spec-milestones",
                        name: "milestones",
                        type: "directory",
                        children: [
                            { id: "milestones-index", name: "index.json", type: "file" },
                            ...milestonesChildren
                        ]
                    },
                    
                    {
                        id: "spec-phases",
                        name: "phases",
                        type: "directory",
                        children: [
                            { id: "phases-index", name: "index.json", type: "file" },
                            ...phasesChildren
                        ]
                    },
                    
                    {
                        id: "spec-tasks",
                        name: "tasks",
                        type: "directory",
                        children: [
                            { id: "tasks-index", name: "index.json", type: "file" },
                            ...tasksChildren
                        ]
                    },
                    
                    {
                        id: "spec-uat",
                        name: "uat",
                        type: "directory",
                        children: uatChildren
                    },

                    {
                        id: "spec-issues",
                        name: "issues",
                        type: "directory",
                        children: [
                            { id: "issues-index", name: "index.json", type: "file" },
                            ...issuesChildren
                        ]
                    }
                ]
            },
            
            {
                id: "state",
                name: "state",
                type: "directory",
                children: [
                    { id: "state-json", name: "state.json", type: "file" },
                    { id: "state-events", name: "events.ndjson", type: "file" },
                    { 
                        id: "state-checkpoints", 
                        name: "checkpoints", 
                        type: "directory",
                        children: [
                            { id: "chk-001", name: "2026-01-13T021500Z.json", type: "file" }
                        ]
                    }
                ]
            },
            
            {
                id: "evidence",
                name: "evidence",
                type: "directory",
                children: [
                    { id: "ev-summary", name: "summary.json", type: "file" },
                    { id: "ev-latest", name: "latest.json", type: "file" },
                    { id: "ev-commands", name: "commands.json", type: "file" },
                    
                    {
                        id: "ev-runs",
                        name: "runs",
                        type: "directory",
                        children: [
                            { id: "runs-index", name: "index.json", type: "file" },
                            ...runsChildren
                        ]
                    },
                    
                    {
                        id: "ev-history",
                        name: "history",
                        type: "directory",
                        children: [
                            { id: "hist-run-001", name: "RUN-2026-01-13T021500Z.json", type: "file" }
                        ]
                    },
                    
                    {
                        id: "ev-task-evidence",
                        name: "task-evidence",
                        type: "directory",
                        children: taskEvidenceChildren
                    }
                ]
            },

            {
                id: "codebase",
                name: "codebase",
                type: "directory",
                children: [
                    { id: "cb-map", name: "map.json", type: "file" },
                    { id: "cb-stack", name: "stack.json", type: "file" },
                    { id: "cb-arch", name: "architecture.json", type: "file" },
                    { id: "cb-struct", name: "structure.json", type: "file" },
                    { id: "cb-conv", name: "conventions.json", type: "file" },
                    { id: "cb-test", name: "testing.json", type: "file" },
                    { id: "cb-int", name: "integrations.json", type: "file" },
                    { id: "cb-concerns", name: "concerns.json", type: "file" },
                    { id: "cb-sym", name: "symbols.json", type: "file" },
                    { id: "cb-graph", name: "file-graph.json", type: "file" }
                ]
            },
            
            {
                id: "context",
                name: "context",
                type: "directory",
                children: [
                    {
                        id: "ctx-packs",
                        name: "packs",
                        type: "directory",
                        children: contextPacksChildren
                    },
                    {
                        id: "ctx-templates",
                        name: "templates",
                        type: "directory",
                        children: [
                            { id: "tpl-task", name: "task-pack.template.json", type: "file" }
                        ]
                    }
                ]
            },
            
            {
                id: "cache",
                name: "cache",
                type: "directory",
                children: [] // Placeholder
            },
            
            {
                id: "locks",
                name: "locks",
                type: "directory",
                children: [] // Placeholder
            },
            
            {
                id: "tmp",
                name: "tmp",
                type: "directory",
                children: [] // Placeholder
            }
        ]
    }
];

// Helper to recursively find a node by its path
export function findNodeByPath(root: FileSystemNode[], pathParts: string[]): FileSystemNode | null {
    if (pathParts.length === 0) return null;
    
    const [current, ...rest] = pathParts;
    
    // Find matching child in current level
    const match = root.find(node => node.name === current);
    
    if (!match) return null;
    
    // If we're at the end of the path, we found it
    if (rest.length === 0) return match;
    
    // If it's a directory, search children
    if (match.type === "directory" && match.children) {
        return findNodeByPath(match.children, rest);
    }
    
    return null;
}
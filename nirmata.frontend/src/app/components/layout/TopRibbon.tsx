import { useState, useEffect } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import {
  ChevronRight,
  LayoutDashboard,
  Folder,
  File,
  FileJson,
  Code,
  Shield,
  History,
  Settings,
  Database,
  Map,
  ListChecks,
  Copy,
  ExternalLink,
  RefreshCw,
  Command,
  Zap,
  Play,
  Home,
  Menu
} from "lucide-react";
import { Button } from "../ui/button";
import { useWorkspace } from "../../hooks/useAosData";
import { toast } from "sonner";
import { copyToClipboard } from "../../utils/clipboard";
import { ScrollArea } from "../ui/scroll-area";
import {
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandGroup,
  CommandItem,
  CommandSeparator,
  CommandShortcut,
} from "../ui/command";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import { getAosLink, resolveAosPath } from "../../utils/aosResolver";

type Breadcrumb = {
  icon: any;
  label: string;
  path: string;
  isDir: boolean;
  virtualPath?: string; // The underlying .aos path
};

export function TopRibbon() {
  const location = useLocation();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  
  // Extract workspaceId
  // Expected format: /ws/:workspaceId/...
  const pathParts = location.pathname.split('/');
  const { workspace: defaultWs } = useWorkspace();
  const workspaceId = pathParts[2] || defaultWs.projectName;
  
  // Toggle Command Palette with Ctrl+K or Cmd+K
  useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === "k" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setOpen((open) => !open);
      }
    };
    document.addEventListener("keydown", down);
    return () => document.removeEventListener("keydown", down);
  }, []);

  const runCommand = (command: () => void) => {
    setOpen(false);
    command();
  };

  const getBreadcrumbs = (): Breadcrumb[] => {
    // 1. Root Crumb (Workspace Dashboard)
    const crumbs: Breadcrumb[] = [{
      icon: LayoutDashboard,
      label: workspaceId,
      path: `/ws/${workspaceId}`,
      isDir: true,
      virtualPath: "/"
    }];

    // 2. Identify the section
    const section = pathParts[3]; // 'files', 'orchestrator', 'settings', etc.
    
    if (!section) return crumbs;

    // Helper to generate path segments from a virtual path string
    const addPathSegments = (virtualPath: string, rootIcon?: any) => {
        // Remove .aos prefix if we want to show it explicitly, or keep it.
        // The prompt says "Tasks lens -> .aos/spec/tasks/".
        // So we should show: Workspace > .aos > spec > tasks
        
        // We need to construct the breadcrumbs such that they are clickable.
        // But clicking ".aos" should go where? probably /ws/:id/files/.aos
        // Clicking "tasks" (the folder) should go to /ws/:id/files/.aos/spec/tasks
        
        const segments = virtualPath.split('/').filter(Boolean);
        let currentPath = "";
        
        segments.forEach((segment, idx) => {
            currentPath = currentPath ? `${currentPath}/${segment}` : segment;
            
            let icon: any = Folder;
            if (segment.includes('.')) {
                icon = File;
                if (segment.endsWith('.json')) icon = FileJson;
            }
            // Use specific icon for the last segment if provided and matches functionality
            if (idx === segments.length - 1 && rootIcon) {
                // If it's a file, we might keep FileJson, but if it's a folder representing the lens, we can use the lens icon
                // actually standard folder icon is probably better for consistency in the path view
            }

            // Special icons for known folders
            if (segment === ".aos") icon = Database;
            if (segment === "spec") icon = ListChecks;
            if (segment === "evidence") icon = Shield;
            if (segment === "codebase") icon = Code;

            crumbs.push({
                icon,
                label: segment,
                // Point to the files view for this path
                path: getAosLink(workspaceId, currentPath),
                isDir: !segment.includes('.'),
                virtualPath: currentPath
            });
        });
    };

    // Mappings for Lenses -> underlying .aos path
    if (section === 'settings') {
        crumbs.push({ label: 'Settings', icon: Settings, path: `/ws/${workspaceId}/settings`, isDir: true });
    } else if (section === 'continuity') {
        addPathSegments(".aos/state");
    } else if (section === 'roadmap') {
        // Map to .aos/spec/roadmap.json
        // We want the breadcrumb to show: Workspace > .aos > spec > roadmap.json
        addPathSegments(".aos/spec/roadmap.json");
    } else if (section === 'tasks') {
        // Map to .aos/spec/tasks
        addPathSegments(".aos/spec/tasks");
        // If there's an ID in the query params or deeper path?
        // The router uses /ws/:id/tasks. TasksPage usually handles query params for specific tasks.
        // If the URL was /ws/:id/tasks?id=TSK-123, we might want to show that, but standard breadcrumbs usually follow URL structure.
        // However, prompt says "use it everywhere... breadcrumbs".
        // If we are on the Tasks lens, we are technically viewing .aos/spec/tasks.
    } else if (section === 'runs') {
        addPathSegments(".aos/evidence/runs");
    } else if (section === 'verification') {
        addPathSegments(".aos/spec/uat"); 
    } else if (section === 'codebase') {
        addPathSegments(".aos/codebase");
    } else if (section === 'files') {
        // Existing logic for files
        const relativePathSegments = pathParts.slice(4);
        // We just treat the whole relative path as the virtual path
        const relativePath = relativePathSegments.map(decodeURIComponent).join('/');
        if (relativePath) {
            addPathSegments(relativePath);
        } else {
            // Root files
            crumbs.push({ label: 'Files', icon: Folder, path: `/ws/${workspaceId}/files`, isDir: true, virtualPath: "/" });
        }
    }

    return crumbs;
  };

  const breadcrumbs = getBreadcrumbs();

  return (
    <div className="h-10 border-b border-border bg-card/50 flex items-center justify-between px-4 shrink-0 font-mono text-xs select-none">
      {/* Breadcrumbs */}
      <ScrollArea className="flex-1 min-w-0 h-full whitespace-nowrap mr-4 [&>[data-slot=scroll-area-viewport]]:flex [&>[data-slot=scroll-area-viewport]]:items-center">
        <div className="flex w-max items-center text-muted-foreground/80 pr-4">
            {breadcrumbs.map((crumb, index) => (
            <div key={index} className="flex items-center whitespace-nowrap">
                {index > 0 && <ChevronRight className="h-3 w-3 mx-1 opacity-40" />}
                
                <div className="flex items-center group">
                    <Link 
                        to={crumb.path}
                        className={`flex items-center gap-1.5 hover:text-foreground hover:bg-muted/50 px-2 py-1 rounded transition-colors ${
                            index === breadcrumbs.length - 1 ? "text-foreground font-medium bg-muted/30" : ""
                        }`}
                        onContextMenu={(e) => {
                            e.preventDefault();
                            // Trigger dropdown manually or rely on separate trigger
                        }}
                    >
                        {crumb.icon && <crumb.icon className="h-3.5 w-3.5 opacity-70" />}
                        <span>{crumb.label}</span>
                    </Link>
                    
                    {/* Context Menu Trigger on Hover or Right Click logic simulation */}
                    <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                            <Button variant="ghost" className="h-4 w-4 p-0 opacity-0 group-hover:opacity-100 transition-opacity -ml-1 focus:opacity-100 data-[state=open]:opacity-100">
                                <span className="sr-only">Open menu</span>
                                <Menu className="h-3 w-3" /> 
                            </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="start">
                            <DropdownMenuItem onClick={() => {
                                copyToClipboard(crumb.virtualPath || crumb.label);
                                toast.success("Path segment copied");
                            }}>
                                <Copy className="mr-2 h-3.5 w-3.5" />
                                Copy Path
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={() => {
                                navigate(crumb.path);
                            }}>
                                <ExternalLink className="mr-2 h-3.5 w-3.5" />
                                Open
                            </DropdownMenuItem>
                        </DropdownMenuContent>
                    </DropdownMenu>
                </div>
            </div>
            ))}
        </div>
      </ScrollArea>

      {/* Quick Actions */}
      <div className="flex items-center gap-1 shrink-0">
        <div className="hidden md:flex items-center gap-2 mr-2 px-2 py-0.5 bg-muted/30 rounded border border-border/40 text-[10px]">
             <span className="text-muted-foreground">Workspace:</span>
             <span className="font-medium text-foreground">{workspaceId}</span>
        </div>
        <div className="h-4 w-px bg-border mx-1" />
        <Button variant="ghost" size="icon" className="h-7 w-7 text-muted-foreground" onClick={() => {
            toast.success("Workspace refreshed");
        }}>
           <RefreshCw className="h-3.5 w-3.5" />
        </Button>
        <Button 
          variant="ghost" 
          size="icon" 
          className="h-7 w-7 text-muted-foreground"
          onClick={() => setOpen(true)}
        >
           <Command className="h-3.5 w-3.5" />
        </Button>
      </div>

      {/* Command Palette Dialog */}
      <CommandDialog open={open} onOpenChange={setOpen}>
        <CommandInput placeholder="Type a command or search..." />
        <CommandList>
          <CommandEmpty>No results found.</CommandEmpty>
          
          <CommandGroup heading="Navigation">
            <CommandItem onSelect={() => runCommand(() => navigate(`/ws/${workspaceId}`))}>
              <Home className="mr-2 h-4 w-4" />
              <span>Workspace Dashboard</span>
            </CommandItem>
            <CommandItem onSelect={() => runCommand(() => navigate(getAosLink(workspaceId, ".aos/state")))}>
              <History className="mr-2 h-4 w-4" />
              <span>Continuity</span>
            </CommandItem>
            <CommandItem onSelect={() => runCommand(() => navigate(`/ws/${workspaceId}/chat`))}>
              <Zap className="mr-2 h-4 w-4" />
              <span>Chat</span>
            </CommandItem>
            <CommandItem onSelect={() => runCommand(() => navigate(getAosLink(workspaceId, ".aos/spec/roadmap.json")))}>
              <Map className="mr-2 h-4 w-4" />
              <span>Roadmap</span>
            </CommandItem>
            <CommandItem onSelect={() => runCommand(() => navigate(getAosLink(workspaceId, ".aos/evidence/runs")))}>
              <Play className="mr-2 h-4 w-4" />
              <span>Runs & Evidence</span>
            </CommandItem>
            <CommandItem onSelect={() => runCommand(() => navigate(`/ws/${workspaceId}/settings`))}>
              <Settings className="mr-2 h-4 w-4" />
              <span>Settings</span>
              <CommandShortcut>⌘S</CommandShortcut>
            </CommandItem>
          </CommandGroup>
          
          <CommandSeparator />
          
          <CommandGroup heading="Actions">
            <CommandItem onSelect={() => runCommand(() => {
               toast.success("Validation started...");
            })}>
              <Shield className="mr-2 h-4 w-4" />
              <span>Validate Workspace</span>
            </CommandItem>
            <CommandItem onSelect={() => runCommand(() => {
               toast.success("Rebuilding codebase map...");
            })}>
              <Code className="mr-2 h-4 w-4" />
              <span>Rebuild Codebase Map</span>
            </CommandItem>
            <CommandItem onSelect={() => runCommand(() => {
               toast.success("Cache cleared");
            })}>
              <Database className="mr-2 h-4 w-4" />
              <span>Clear Cache</span>
            </CommandItem>
          </CommandGroup>

          <CommandSeparator />

          <CommandGroup heading="Recent Files">
            <CommandItem onSelect={() => runCommand(() => navigate(getAosLink(workspaceId, ".aos/spec/project.json")))}>
              <FileJson className="mr-2 h-4 w-4" />
              <span>project.json</span>
            </CommandItem>
            <CommandItem onSelect={() => runCommand(() => navigate(getAosLink(workspaceId, "src/app/App.tsx")))}>
              <File className="mr-2 h-4 w-4" />
              <span>App.tsx</span>
            </CommandItem>
          </CommandGroup>

        </CommandList>
      </CommandDialog>
    </div>
  );
}
import * as React from "react"
import {
  LayoutDashboard,
  Map,
  ListChecks,
  History,
  Terminal,
  Play,
  MessageSquare,
  GitBranch,
  Settings,
  Cpu,
} from "lucide-react"

import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from "../ui/command"
import { useNavigate, useParams, useLocation } from "react-router"
import { toast } from "sonner"
import { useWorkspaceContext } from "../../context/WorkspaceContext"

export function GlobalCommandPalette() {
  const [open, setOpen] = React.useState(false)
  const navigate = useNavigate()
  const { pathname } = useLocation()

  // Detect whether the current URL is workspace-scoped (/ws/:workspaceId/...)
  const { workspaceId: paramWsId } = useParams<{ workspaceId: string }>()
  const { activeWorkspaceId } = useWorkspaceContext()
  const pathParts = pathname.split('/')
  const isWorkspaceScoped = pathParts[1] === 'ws' && Boolean(pathParts[2])
  const wsId = isWorkspaceScoped ? (paramWsId ?? activeWorkspaceId) : null

  const ws = (wsPath: string, noWsPath: string) =>
    wsId ? `/ws/${wsId}${wsPath}` : noWsPath

  React.useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === "k" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault()
        setOpen((open) => !open)
        return
      }
      if (
        e.key === "/" &&
        !(e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement)
      ) {
        e.preventDefault()
        setOpen((open) => !open)
      }
    }

    document.addEventListener("keydown", down)
    return () => document.removeEventListener("keydown", down)
  }, [])

  const runCommand = (command: () => void) => {
    setOpen(false)
    command()
  }

  return (
    <CommandDialog open={open} onOpenChange={setOpen}>
      <CommandInput placeholder="Type a command or search..." />
      <CommandList>
        <CommandEmpty>No results found.</CommandEmpty>
        <CommandGroup heading="Pages">
          <CommandItem onSelect={() => runCommand(() => navigate(ws("", "/")))}>
            <LayoutDashboard className="mr-2 h-4 w-4" />
            <span>Workspace Dashboard</span>
          </CommandItem>
          <CommandItem onSelect={() => runCommand(() => navigate(ws("/chat", "/chat")))}>
            <MessageSquare className="mr-2 h-4 w-4" />
            <span>Chat</span>
          </CommandItem>
          <CommandItem onSelect={() => runCommand(() => navigate(ws("/files/.aos/spec", "/plan")))}>
            <Map className="mr-2 h-4 w-4" />
            <span>Plan / Roadmap</span>
          </CommandItem>
          <CommandItem onSelect={() => runCommand(() => navigate(ws("/files/.aos/evidence/runs", "/runs")))}>
            <History className="mr-2 h-4 w-4" />
            <span>Runs</span>
          </CommandItem>
          <CommandItem onSelect={() => runCommand(() => navigate(ws("/files/.aos/state", "/continuity")))}>
            <ListChecks className="mr-2 h-4 w-4" />
            <span>Continuity</span>
          </CommandItem>
          <CommandItem onSelect={() => runCommand(() => navigate(ws("/files/.aos/spec/uat", "/verification")))}>
            <GitBranch className="mr-2 h-4 w-4" />
            <span>Verification / UAT</span>
          </CommandItem>
          <CommandItem onSelect={() => runCommand(() => navigate(ws("/files/.aos/codebase", "/codebase")))}>
            <Cpu className="mr-2 h-4 w-4" />
            <span>Codebase</span>
          </CommandItem>
          <CommandItem onSelect={() => runCommand(() => navigate(ws("/settings", "/settings")))}>
            <Settings className="mr-2 h-4 w-4" />
            <span>Settings</span>
          </CommandItem>
        </CommandGroup>
        <CommandSeparator />
        <CommandGroup heading="Actions">
          <CommandItem
            onSelect={() =>
              runCommand(() => toast.info("Run Diagnostics: not yet connected to daemon"))
            }
          >
            <Terminal className="mr-2 h-4 w-4" />
            <span>Run Diagnostics</span>
          </CommandItem>
          <CommandItem
            onSelect={() =>
              runCommand(() => toast.info("Sync Codebase: not yet connected to daemon"))
            }
          >
            <Play className="mr-2 h-4 w-4" />
            <span>Sync Codebase</span>
          </CommandItem>
        </CommandGroup>
      </CommandList>
    </CommandDialog>
  )
}
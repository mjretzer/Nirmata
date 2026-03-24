import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "../ui/sheet"
import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { Separator } from "../ui/separator"
import { Activity, AlertTriangle, CheckCircle, Wifi, RefreshCw } from "lucide-react"

import { useWorkspaceContext } from "../../context/WorkspaceContext"

interface DiagnosticsDrawerProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function DiagnosticsDrawer({ open, onOpenChange }: DiagnosticsDrawerProps) {
  const { daemonConnectionState, reconnect } = useWorkspaceContext()

  const connectionConfig = {
    connecting: {
      label: "Connecting",
      badgeClassName: "bg-amber-500/10 text-amber-500 border-amber-500/20",
      icon: Wifi,
      iconClassName: "text-amber-500",
      description: "Waiting for the daemon health check to complete.",
      actionLabel: null,
    },
    connected: {
      label: "Connected",
      badgeClassName: "bg-green-500/10 text-green-500 border-green-500/20",
      icon: CheckCircle,
      iconClassName: "text-green-500",
      description: "Connected to the Windows Service API host.",
      actionLabel: null,
    },
    disconnected: {
      label: "Reconnect required",
      badgeClassName: "bg-red-500/10 text-red-500 border-red-500/20",
      icon: AlertTriangle,
      iconClassName: "text-red-500",
      description: "Health polling stopped after a failed startup check. Fix the daemon configuration, then reconnect.",
      actionLabel: "Reconnect",
    },
  } as const

  const connection = connectionConfig[daemonConnectionState]
  const StatusIcon = connection.icon

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent>
        <SheetHeader>
          <SheetTitle>System Diagnostics</SheetTitle>
          <SheetDescription>
            Engine connectivity and health status.
          </SheetDescription>
        </SheetHeader>
        
        <div className="space-y-6 mt-6">
          {/* Connection Status */}
          <div className="bg-card border border-border rounded-lg p-4">
            <div className="flex items-center justify-between mb-2">
              <span className="text-sm font-medium flex items-center gap-2">
                <StatusIcon className={`h-4 w-4 ${connection.iconClassName}`} />
                Connection
              </span>
              <Badge variant="outline" className={connection.badgeClassName}>
                {connection.label}
              </Badge>
            </div>
            <p className="text-xs text-muted-foreground">
              {connection.description}
            </p>
            {connection.actionLabel && (
              <div className="mt-3">
                <Button variant="outline" size="sm" className="gap-2 text-xs" onClick={reconnect}>
                  <RefreshCw className="h-3 w-3" />
                  {connection.actionLabel}
                </Button>
              </div>
            )}
          </div>

          <Separator />

          {/* Engine Info */}
          <div className="space-y-4">
            <h3 className="text-sm font-medium">Engine Status</h3>
            
            <div className="grid grid-cols-2 gap-4">
              <div className="p-3 bg-muted/50 rounded-md">
                <span className="text-xs text-muted-foreground block mb-1">Version</span>
                <span className="text-sm font-mono font-medium">v2.4.1-beta</span>
              </div>
              <div className="p-3 bg-muted/50 rounded-md">
                <span className="text-xs text-muted-foreground block mb-1">Uptime</span>
                <span className="text-sm font-mono font-medium">4d 12h 30m</span>
              </div>
            </div>

            <div className="flex items-center justify-between p-3 bg-muted/50 rounded-md">
              <span className="text-sm">Workspace Lock</span>
              <Badge variant="secondary">Active</Badge>
            </div>

             <div className="flex items-center justify-between p-3 bg-muted/50 rounded-md">
              <span className="text-sm">Heartbeat</span>
              <span className="text-xs font-mono text-muted-foreground">23ms ago</span>
            </div>
          </div>

          <Separator />

          {/* Recent Events */}
          <div className="space-y-3">
             <h3 className="text-sm font-medium flex items-center gap-2">
               <Activity className="h-4 w-4" />
               Recent Activity
             </h3>
             
             <div className="space-y-2">
               <div className="text-xs flex gap-2 items-start">
                 <CheckCircle className="h-3 w-3 text-green-500 mt-0.5" />
                 <div>
                   <span className="block text-foreground">Snapshot verified</span>
                   <span className="text-muted-foreground">2 mins ago</span>
                 </div>
               </div>
               <div className="text-xs flex gap-2 items-start">
                 <AlertTriangle className="h-3 w-3 text-yellow-500 mt-0.5" />
                 <div>
                   <span className="block text-foreground">High latency detected</span>
                   <span className="text-muted-foreground">15 mins ago</span>
                 </div>
               </div>
             </div>
          </div>
        </div>
      </SheetContent>
    </Sheet>
  )
}
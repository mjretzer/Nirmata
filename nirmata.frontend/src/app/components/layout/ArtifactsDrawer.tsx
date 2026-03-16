import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from "../ui/sheet"
import { ScrollArea } from "../ui/scroll-area"
import { Folder, File, ChevronRight, FileJson, FileText } from "lucide-react"
import { useState } from "react"
import { PathChip } from "../PathChip"

interface ArtifactsDrawerProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

const mockArtifacts = [
  {
    name: ".aos",
    type: "folder",
    children: [
      {
        name: "spec",
        type: "folder",
        children: [
          { name: "workflow.yaml", type: "file" },
          { name: "tasks.json", type: "file" }
        ]
      },
      {
        name: "state",
        type: "folder",
        children: [
          { name: "current_run.json", type: "file" },
          { name: "history.db", type: "file" }
        ]
      },
      {
        name: "evidence",
        type: "folder",
        children: [
          { name: "screenshot_001.png", type: "file" },
          { name: "logs_2023-10-27.txt", type: "file" }
        ]
      }
    ]
  }
]

const FileTreeItem = ({ item, depth = 0 }: { item: any; depth?: number }) => {
  const [expanded, setExpanded] = useState(false)
  
  if (item.type === "folder") {
    return (
      <div className="select-none">
        <div 
          className="flex items-center gap-2 py-1 px-2 hover:bg-accent/50 rounded cursor-pointer"
          style={{ paddingLeft: `${depth * 12 + 8}px` }}
          onClick={() => setExpanded(!expanded)}
        >
          <ChevronRight 
            className={`h-3 w-3 transition-transform ${expanded ? "rotate-90" : ""}`} 
          />
          <Folder className="h-4 w-4 text-muted-foreground" />
          <span className="text-sm font-medium">{item.name}</span>
        </div>
        {expanded && (
          <div>
            {item.children?.map((child: any, i: number) => (
              <FileTreeItem key={i} item={child} depth={depth + 1} />
            ))}
          </div>
        )}
      </div>
    )
  }
  
  return (
    <div 
      className="flex items-center gap-2 py-1 px-2 hover:bg-accent/50 rounded cursor-default group"
      style={{ paddingLeft: `${depth * 12 + 24}px` }}
    >
      <FileText className="h-3.5 w-3.5 text-muted-foreground" />
      <span className="text-sm">{item.name}</span>
      <div className="ml-auto opacity-0 group-hover:opacity-100 transition-opacity">
        <PathChip path={item.path || item.name} />
      </div>
    </div>
  )
}

export function ArtifactsDrawer({ open, onOpenChange }: ArtifactsDrawerProps) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-[300px] sm:w-[400px]">
        <SheetHeader>
          <SheetTitle>Artifact Browser</SheetTitle>
          <SheetDescription>Explore generated artifacts and logs.</SheetDescription>
        </SheetHeader>
        <ScrollArea className="h-[calc(100vh-100px)] mt-4">
          <div className="space-y-1">
            {mockArtifacts.map((item, i) => (
              <FileTreeItem key={i} item={item} />
            ))}
          </div>
        </ScrollArea>
      </SheetContent>
    </Sheet>
  )
}
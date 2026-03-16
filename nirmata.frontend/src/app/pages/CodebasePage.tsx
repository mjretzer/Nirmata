import { useState, useMemo } from "react";
import {
  Search,
  RefreshCw,
  Database,
  FileCode,
  Shield,
  AlertCircle,
  CheckCircle2,
  ChevronRight,
  Eye,
  Copy,
  AlertTriangle,
  Play,
  Folder,
  Layers,
  Box,
  Terminal,
} from "lucide-react";
import { toast } from "sonner";
import { Badge } from "../components/ui/badge";
import { Button } from "../components/ui/button";
import { Input } from "../components/ui/input";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "../components/ui/card";
import { cn } from "../components/ui/utils";
import { JsonPreviewCard } from "../components/JsonPreviewCard";
import { PieChart, Pie, Cell, Tooltip } from "recharts";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription, SheetFooter } from "../components/ui/sheet";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "../components/ui/tabs";
import { ScrollArea } from "../components/ui/scroll-area";
import { Alert, AlertTitle, AlertDescription } from "../components/ui/alert";
import {
  useCodebaseIntel,
  type CodebaseArtifact,
} from "../hooks/useAosData";

// ── Components ───────────────────────────────────────────────────────

function StatusPill({ status }: { status: CodebaseArtifact['status'] }) {
  const styles = {
    ready: "bg-emerald-500/10 text-emerald-600 border-emerald-500/20",
    stale: "bg-yellow-500/10 text-yellow-600 border-yellow-500/20",
    missing: "bg-red-500/10 text-red-600 border-red-500/20",
    error: "bg-red-500/10 text-red-600 border-red-500/20",
  };
  
  const icons = {
    ready: CheckCircle2,
    stale: AlertTriangle,
    missing: AlertCircle,
    error: AlertCircle,
  };

  const Icon = icons[status];

  return (
    <Badge variant="outline" className={cn("gap-1.5 font-mono text-[10px] h-5 px-2 uppercase", styles[status])}>
      <Icon className="h-3 w-3" />
      {status}
    </Badge>
  );
}

function ContextHealthHero({ artifacts, onPreview, onRegenerate }: { artifacts: CodebaseArtifact[], onPreview: (id: string) => void, onRegenerate: () => void }) {
  const readyCount = artifacts.filter(a => a.status === 'ready').length;
  const totalCount = artifacts.filter(a => a.type === 'intel').length;
  const staleCount = artifacts.filter(a => a.status === 'stale').length;
  const missingCount = artifacts.filter(a => a.status === 'missing').length;

  return (
    <Card className="border-border/60 bg-gradient-to-br from-card to-muted/20 shadow-sm overflow-hidden">
      <CardHeader className="pb-4 border-b border-border/40">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-lg font-semibold flex items-center gap-2">
              <Shield className="h-5 w-5 text-primary" />
              Context Health
            </CardTitle>
            <CardDescription className="flex items-center gap-2 mt-1">
              <span className="font-medium text-foreground">{totalCount} required</span>
              <span>•</span>
              <span className="text-emerald-600">{readyCount} ready</span>
              <span>•</span>
              {staleCount > 0 && <span className="text-yellow-600">{staleCount} stale</span>}
              {missingCount > 0 && <span className="text-red-600">{missingCount} missing</span>}
            </CardDescription>
          </div>
          <div className="text-right hidden sm:block">
            <div className="text-xs text-muted-foreground uppercase tracking-wider mb-1">System Status</div>
            <Badge variant="outline" className="bg-background text-emerald-600 border-emerald-200">
               Operational
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="p-0">
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-px bg-border/40">
          {artifacts.filter(a => a.type === 'intel').map((artifact) => (
             <div key={artifact.id} className="bg-card p-4 flex flex-col gap-3 hover:bg-muted/30 transition-colors group relative">
                <div className="flex items-start justify-between">
                   <div className="p-1.5 rounded-md bg-muted/50 text-muted-foreground group-hover:text-primary group-hover:bg-primary/5 transition-colors">
                      <FileCode className="h-4 w-4" />
                   </div>
                   <div className={cn(
                     "h-2 w-2 rounded-full",
                     artifact.status === 'ready' ? "bg-emerald-500" :
                     artifact.status === 'stale' ? "bg-yellow-500" :
                     "bg-red-500"
                   )} />
                </div>
                <div>
                  <div className="font-mono text-xs font-medium truncate" title={artifact.name}>{artifact.name}</div>
                  <div className="text-[10px] text-muted-foreground truncate">{artifact.description}</div>
                </div>
                
                {/* Hover Actions */}
                <div className="absolute inset-0 bg-background/90 backdrop-blur-[1px] opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2">
                   <Button size="icon" variant="outline" className="h-7 w-7 rounded-full shadow-sm" title="Preview" onClick={() => onPreview(artifact.id)}>
                       <Eye className="h-3.5 w-3.5" />
                    </Button>
                    <Button size="icon" variant="outline" className="h-7 w-7 rounded-full shadow-sm" title="Regenerate" onClick={onRegenerate}>
                       <RefreshCw className="h-3.5 w-3.5" />
                    </Button>
                </div>
             </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

function LocationCard({ 
  title, 
  path, 
  desc, 
  actions 
}: { 
  title: string, 
  path: string, 
  desc: string, 
  actions: React.ReactNode 
}) {
  return (
    <Card className="border-border/60 shadow-sm flex flex-col h-full">
      <CardContent className="p-5 flex flex-col h-full justify-between gap-4">
        <div>
           <h3 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground mb-3">{title}</h3>
           <div className="flex items-center gap-2 font-mono text-xs bg-muted/50 border border-border/50 px-3 py-2 rounded-md text-foreground select-all">
              <Folder className="h-3.5 w-3.5 text-blue-500/70" />
              {path}
           </div>
           <p className="text-xs text-muted-foreground mt-3 leading-relaxed">
             {desc}
           </p>
        </div>
        <div className="flex gap-2 pt-2 border-t border-border/40">
           {actions}
        </div>
      </CardContent>
    </Card>
  )
}

function ArtifactDrawer({ 
  artifact, 
  isOpen, 
  onClose 
}: { 
  artifact: CodebaseArtifact | null, 
  isOpen: boolean, 
  onClose: () => void 
}) {
  if (!artifact) return null;

  return (
    <Sheet open={isOpen} onOpenChange={onClose}>
      <SheetContent className="w-[400px] sm:w-[540px] flex flex-col gap-0 p-0 border-l border-border/60 shadow-2xl">
        <SheetHeader className="p-6 border-b border-border/60 bg-muted/5">
          <div className="flex items-center gap-2 mb-2">
            <Badge variant="outline" className="uppercase text-[10px] tracking-wider">{artifact.type}</Badge>
            <StatusPill status={artifact.status} />
          </div>
          <SheetTitle className="font-mono text-lg">{artifact.name}</SheetTitle>
          <SheetDescription className="font-mono text-xs text-muted-foreground break-all">
            {artifact.path}
          </SheetDescription>
        </SheetHeader>
        
        <Tabs defaultValue="preview" className="flex-1 flex flex-col overflow-hidden">
          <div className="px-6 border-b border-border/60 bg-background">
             <TabsList className="h-10 -mb-px bg-transparent p-0">
                <TabsTrigger value="summary" className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent px-4 pb-2 pt-3 text-xs">Summary</TabsTrigger>
                <TabsTrigger value="preview" className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent px-4 pb-2 pt-3 text-xs">Preview</TabsTrigger>
                <TabsTrigger value="validate" className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent px-4 pb-2 pt-3 text-xs">Validate</TabsTrigger>
             </TabsList>
          </div>
          
          <TabsContent value="summary" className="flex-1 p-6 overflow-y-auto m-0 bg-muted/5">
             <div className="space-y-6">
                <div className="space-y-2">
                   <h4 className="text-sm font-medium">Metadata</h4>
                   <div className="grid grid-cols-2 gap-4 text-xs">
                      <div className="p-3 rounded-lg border bg-background">
                         <div className="text-muted-foreground mb-1">Last Updated</div>
                         <div className="font-medium">{artifact.lastUpdated}</div>
                      </div>
                      <div className="p-3 rounded-lg border bg-background">
                         <div className="text-muted-foreground mb-1">Size</div>
                         <div className="font-medium">{artifact.size}</div>
                      </div>
                   </div>
                </div>

                <div className="space-y-2">
                   <h4 className="text-sm font-medium">Purpose</h4>
                   <p className="text-xs text-muted-foreground leading-relaxed p-3 rounded-lg border bg-background">
                     {artifact.description}. Agents use this file to understand the codebase context, ensuring accurate planning and code generation.
                   </p>
                </div>
             </div>
          </TabsContent>
          
          <TabsContent value="preview" className="flex-1 overflow-hidden m-0 relative flex flex-col">
             <div className="absolute top-2 right-2 z-10">
                <Button size="sm" variant="outline" className="h-6 text-[10px] bg-background/80 backdrop-blur">
                   <Copy className="h-3 w-3 mr-1" /> Copy
                </Button>
             </div>
             <ScrollArea className="flex-1 p-4 bg-muted/10 font-mono text-xs">
                <pre className="text-muted-foreground">
{`{
  "meta": {
    "generatedAt": "${new Date().toISOString()}",
    "version": "1.4.2",
    "scope": "full"
  },
  "content": {
    "type": "${artifact.type}",
    "name": "${artifact.name}",
    "description": "${artifact.description}",
    "metrics": {
       "complexity": "low",
       "coverage": "high"
    }
  }
  // ... rest of content
}`}
                </pre>
             </ScrollArea>
          </TabsContent>

          <TabsContent value="validate" className="flex-1 p-6 m-0">
             <div className="flex flex-col gap-4">
                <Alert variant={artifact.status === 'ready' ? "default" : "destructive"}>
                   {artifact.status === 'ready' ? <CheckCircle2 className="h-4 w-4" /> : <AlertTriangle className="h-4 w-4" />}
                   <AlertTitle>{artifact.status === 'ready' ? "Validation Passed" : "Validation Issues"}</AlertTitle>
                   <AlertDescription>
                      {artifact.status === 'ready' 
                         ? "Schema validation successful. No errors detected." 
                         : "Artifact is missing required fields or has outdated structure."}
                   </AlertDescription>
                </Alert>
             </div>
          </TabsContent>
        </Tabs>

        <SheetFooter className="p-4 border-t border-border/60 bg-background sm:justify-between">
           <Button variant="outline" size="sm" onClick={onClose}>Close</Button>
           <div className="flex gap-2">
              <Button variant="outline" size="sm">Open in Editor</Button>
              <Button size="sm">Regenerate</Button>
           </div>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

// ── Main Page ────────────────────────────────────────────────────────

export function CodebasePage() {
  const { artifacts: ARTIFACTS, languages: LANGUAGES, stack: STACK } = useCodebaseIntel();
  const [isIndexing, setIsIndexing] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [expandedArtifact, setExpandedArtifact] = useState<string | null>(null);
  const [drawerArtifactId, setDrawerArtifactId] = useState<string | null>(null);

  const drawerArtifact = ARTIFACTS.find(a => a.id === drawerArtifactId) ?? null;

  const handleReindex = () => {
    setIsIndexing(true);
    toast.promise(
      new Promise((resolve) => setTimeout(resolve, 2000)),
      {
        loading: "Scanning codebase...",
        success: () => { setIsIndexing(false); return "Index updated successfully"; },
        error: "Failed to update index",
      }
    );
  };

  const filteredArtifacts = useMemo(() => {
    return ARTIFACTS.filter(a => {
      if (statusFilter && a.status !== statusFilter) return false;
      if (typeFilter && a.type !== typeFilter) return false;
      if (searchQuery) {
        const q = searchQuery.toLowerCase();
        return (
          a.name.toLowerCase().includes(q) ||
          a.description.toLowerCase().includes(q) ||
          a.path.toLowerCase().includes(q)
        );
      }
      return true;
    });
  }, [searchQuery, statusFilter, typeFilter, ARTIFACTS]);

  const readyCount   = ARTIFACTS.filter(a => a.status === "ready").length;
  const staleCount   = ARTIFACTS.filter(a => a.status === "stale").length;
  const missingCount = ARTIFACTS.filter(a => a.status === "missing" || a.status === "error").length;
  const intelCount   = ARTIFACTS.filter(a => a.type === "intel").length;

  return (
    <div className="flex-1 flex flex-col overflow-hidden bg-background">
      {/* ── Content ── */}
      <div className="flex-1 overflow-auto">
        <div className="p-6 space-y-5">

          {/* Header */}
          <div className="flex items-start justify-between">
            <div>
              <div className="flex items-center gap-2 mb-1">
                <Terminal className="h-4 w-4 text-muted-foreground" />
                <span className="font-mono text-xs text-muted-foreground">.aos/intel/codebase</span>
              </div>
              <h1 className="text-2xl font-bold tracking-tight mb-1">Codebase Context</h1>
              <p className="text-muted-foreground text-sm max-w-2xl font-mono">
                {intelCount} intel files tracked — {readyCount} ready, {staleCount} stale, {missingCount} missing
              </p>
            </div>
            <div className="flex gap-2 items-stretch">
              <Button
                variant="outline"
                size="sm"
                className="font-mono text-xs h-8"
                onClick={() => { navigator.clipboard.writeText(".aos/intel/codebase.json"); toast.success("Copied path"); }}
              >
                <Copy className="h-3 w-3 mr-1.5" /> Copy Path
              </Button>
              <Button
                size="sm"
                className="font-mono text-xs h-8 bg-emerald-500 hover:bg-emerald-600 text-emerald-950 shadow-[0_0_10px_rgba(16,185,129,0.2)] border-0"
                onClick={handleReindex}
                disabled={isIndexing}
              >
                <Search className="h-3 w-3 mr-1.5" /> Codebase Scan
              </Button>
            </div>
          </div>

          {/* Stats Row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            <Card className="bg-card/50">
              <CardContent className="p-3">
                <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-1">Intel Files</div>
                <div className="font-mono text-lg font-bold text-foreground flex items-center gap-2">
                  {intelCount}
                  <FileCode className="h-3.5 w-3.5 text-muted-foreground/40" />
                </div>
              </CardContent>
            </Card>
            <Card className="bg-card/50">
              <CardContent className="p-3">
                <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-1">Ready</div>
                <div className="font-mono text-lg font-bold text-green-400 flex items-center gap-2">
                  {readyCount}
                  <CheckCircle2 className="h-3.5 w-3.5 text-green-500/40" />
                </div>
              </CardContent>
            </Card>
            <Card className="bg-card/50">
              <CardContent className="p-3">
                <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-1">Stale</div>
                <div className={cn("font-mono text-lg font-bold flex items-center gap-2", staleCount > 0 ? "text-yellow-400" : "text-foreground")}>
                  {staleCount}
                  <AlertTriangle className={cn("h-3.5 w-3.5", staleCount > 0 ? "text-yellow-500/40" : "text-muted-foreground/40")} />
                </div>
              </CardContent>
            </Card>
            <Card className="bg-card/50">
              <CardContent className="p-3">
                <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-1">Missing</div>
                <div className={cn("font-mono text-lg font-bold flex items-center gap-2", missingCount > 0 ? "text-red-400" : "text-foreground")}>
                  {missingCount}
                  <AlertCircle className={cn("h-3.5 w-3.5", missingCount > 0 ? "text-red-500/40" : "text-muted-foreground/40")} />
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Context Health Hero */}
          <ContextHealthHero artifacts={ARTIFACTS} onPreview={setDrawerArtifactId} onRegenerate={handleReindex} />

          {/* Language + Stack Row */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">

            {/* Language Makeup */}
            <Card className="bg-card/50">
              <CardContent className="p-3">
                <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-3 flex items-center gap-1.5">
                  <Layers className="h-3 w-3" /> Language Makeup
                </div>
                <div className="flex items-center gap-4">
                  {/* Pie */}
                  <div className="shrink-0">
                    <PieChart width={110} height={110}>
                      <Pie
                        data={LANGUAGES}
                        cx={55}
                        cy={55}
                        innerRadius={28}
                        outerRadius={50}
                        paddingAngle={2}
                        dataKey="pct"
                        strokeWidth={0}
                      >
                        {LANGUAGES.map((lang) => (
                          <Cell key={lang.name} fill={lang.color} />
                        ))}
                      </Pie>
                      <Tooltip
                        contentStyle={{ background: "hsl(var(--card))", border: "1px solid hsl(var(--border))", borderRadius: "6px", fontSize: "11px", fontFamily: "monospace" }}
                        formatter={(value: number) => [`${value}%`, ""]}
                        itemStyle={{ color: "hsl(var(--foreground))" }}
                      />
                    </PieChart>
                  </div>
                  {/* Legend */}
                  <div className="flex-1 space-y-1.5 min-w-0">
                    {LANGUAGES.map((lang) => (
                      <div key={lang.name} className="flex items-center gap-2">
                        <div className="h-2 w-2 rounded-full shrink-0" style={{ background: lang.color }} />
                        <span className="font-mono text-[11px] text-foreground/80 flex-1 truncate">{lang.name}</span>
                        <span className="font-mono text-[11px] text-muted-foreground shrink-0">{lang.pct}%</span>
                      </div>
                    ))}
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Project Stack */}
            <Card className="bg-card/50">
              <CardContent className="p-3">
                <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-3 flex items-center gap-1.5">
                  <Box className="h-3 w-3" /> Project Stack
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {STACK.map(tile => (
                    <div key={tile.name} className={cn("flex flex-col rounded border px-2 py-1.5 min-w-0", tile.color)}>
                      <span className="font-mono text-[11px] font-medium leading-tight">{tile.name}</span>
                      <span className="font-mono text-[9px] opacity-50 uppercase tracking-wider leading-tight mt-0.5">{tile.category}</span>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>

          </div>

          {/* Key Locations */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <LocationCard
              title="Intel Directory"
              path=".aos/intel/"
              desc="Generated codebase intelligence files consumed by planning and execution agents."
              actions={
                <>
                  <Button variant="outline" size="sm" className="text-xs font-mono h-7" onClick={() => toast.info("Opening .aos/intel/ in editor")}>
                    Open
                  </Button>
                  <Button variant="outline" size="sm" className="text-xs font-mono h-7" onClick={() => { navigator.clipboard.writeText(".aos/intel/"); toast.success("Copied"); }}>
                    <Copy className="h-3 w-3 mr-1" /> Copy
                  </Button>
                </>
              }
            />
            <LocationCard
              title="Evidence Directory"
              path=".aos/evidence/runs/"
              desc="Verification evidence and run artifacts produced during task execution."
              actions={
                <>
                  <Button variant="outline" size="sm" className="text-xs font-mono h-7" onClick={() => toast.info("Opening .aos/evidence/runs/ in editor")}>
                    Open
                  </Button>
                  <Button variant="outline" size="sm" className="text-xs font-mono h-7" onClick={() => { navigator.clipboard.writeText(".aos/evidence/runs/"); toast.success("Copied"); }}>
                    <Copy className="h-3 w-3 mr-1" /> Copy
                  </Button>
                </>
              }
            />
          </div>

          {/* Filter Row */}
          <div className="flex items-center gap-3">
            <div className="relative flex-1 max-w-sm">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <Input
                placeholder="Search by name, description, path..."
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                className="pl-8 h-8 text-xs font-mono"
              />
            </div>
            <select
              value={statusFilter}
              onChange={e => setStatusFilter(e.target.value)}
              className="h-8 px-2 text-xs font-mono bg-background border border-border rounded-md text-foreground"
            >
              <option value="">All Statuses</option>
              <option value="ready">Ready</option>
              <option value="stale">Stale</option>
              <option value="missing">Missing</option>
              <option value="error">Error</option>
            </select>
            <select
              value={typeFilter}
              onChange={e => setTypeFilter(e.target.value)}
              className="h-8 px-2 text-xs font-mono bg-background border border-border rounded-md text-foreground"
            >
              <option value="">All Types</option>
              <option value="intel">Intel</option>
              <option value="pack">Pack</option>
              <option value="cache">Cache</option>
            </select>
            <span className="text-xs text-muted-foreground font-mono ml-auto">
              {filteredArtifacts.length} artifact{filteredArtifacts.length !== 1 ? "s" : ""}
            </span>
          </div>

          {/* Artifact List */}
          <div className="space-y-2">
            {filteredArtifacts.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-center">
                <FileCode className="h-10 w-10 text-muted-foreground/20 mb-3" />
                <p className="text-sm font-mono text-muted-foreground">
                  {searchQuery || statusFilter || typeFilter
                    ? "No artifacts match your filters."
                    : "No artifacts found."}
                </p>
              </div>
            ) : (
              filteredArtifacts.map(artifact => {
                const isExpanded = expandedArtifact === artifact.id;
                const dotColor =
                  artifact.status === "ready"  ? "bg-green-500" :
                  artifact.status === "stale"  ? "bg-yellow-500" :
                  "bg-red-500";
                const borderColor =
                  artifact.status === "ready"  ? "border-border/60 bg-card/30" :
                  artifact.status === "stale"  ? "border-yellow-500/20 bg-yellow-500/[0.01]" :
                  "border-red-500/20 bg-red-500/[0.01]";

                return (
                  <div
                    key={artifact.id}
                    className={cn("border rounded-lg transition-all", borderColor, isExpanded && "shadow-sm")}
                  >
                    {/* Row Header */}
                    <div
                      className="flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-accent/30 transition-colors rounded-lg"
                      onClick={() => setExpandedArtifact(isExpanded ? null : artifact.id)}
                    >
                      {/* Status dot */}
                      <div className={cn("h-2.5 w-2.5 rounded-full shrink-0", dotColor)} />

                      {/* Expand chevron */}
                      <ChevronRight className={cn(
                        "h-3.5 w-3.5 text-muted-foreground/40 shrink-0 transition-transform",
                        isExpanded && "rotate-90"
                      )} />

                      {/* Name */}
                      <span className="font-mono text-xs text-foreground/80 shrink-0">{artifact.name}</span>

                      {/* Type badge */}
                      <Badge
                        variant="outline"
                        className="text-[9px] h-4 px-1.5 shrink-0 uppercase font-mono"
                      >
                        {artifact.type}
                      </Badge>

                      {/* Description */}
                      {artifact.description && (
                        <span className="text-[10px] font-mono text-muted-foreground/60 truncate">
                          {artifact.description}
                        </span>
                      )}

                      {/* Spacer */}
                      <div className="flex-1" />

                      {/* Size */}
                      {artifact.size !== "-" && (
                        <div className="flex items-center gap-1 text-[10px] font-mono text-muted-foreground/50 shrink-0">
                          <Database className="h-3 w-3" />
                          {artifact.size}
                        </div>
                      )}

                      {/* Status pill */}
                      <StatusPill status={artifact.status} />

                      {/* Last updated */}
                      <span className="text-[10px] font-mono text-muted-foreground/40 shrink-0">
                        {artifact.lastUpdated}
                      </span>
                    </div>

                    {/* Expanded Detail */}
                    {isExpanded && (
                      <div className="px-4 pb-4 pt-2 border-t border-border/30">
                        <div className="bg-muted/10 border border-border/40 rounded-lg overflow-hidden">
                          {artifact.status === "missing" || artifact.status === "error" ? (
                            <div className="flex flex-col items-center justify-center gap-3 py-8 px-4 text-center">
                              <div className="relative">
                                <FileCode className="h-10 w-10 text-muted-foreground/15" />
                                <div className="absolute -bottom-1 -right-1 h-4 w-4 rounded-full bg-red-500/15 border border-red-500/30 flex items-center justify-center">
                                  <AlertCircle className="h-2.5 w-2.5 text-red-400" />
                                </div>
                              </div>
                              <div className="space-y-1">
                                <p className="font-mono text-xs text-foreground/60">{artifact.path}</p>
                                <p className="font-mono text-[11px] text-muted-foreground/50">
                                  {artifact.status === "error" ? "File could not be read — check permissions or encoding." : "File has not been generated yet."}
                                </p>
                              </div>
                              <div className="flex items-center gap-1.5 font-mono text-[10px] text-red-400/70 bg-red-500/5 border border-red-500/15 rounded px-2.5 py-1">
                                <AlertCircle className="h-3 w-3 shrink-0" />
                                {artifact.status === "error" ? "read error" : "not found"}
                              </div>
                            </div>
                          ) : (
                            <JsonPreviewCard
                              label="Artifact Intel"
                              filename={artifact.name}
                              data={{
                                path: artifact.path,
                                type: artifact.type,
                                status: artifact.status,
                                description: artifact.description,
                                size: artifact.size,
                                lastUpdated: artifact.lastUpdated,
                              }}
                              onClick={() => toast.info(`Opening ${artifact.path}`)}
                            />
                          )}
                        </div>

                        {/* Footer Actions */}
                        <div className="flex items-center gap-2 mt-3">
                          <Button
                            variant="outline"
                            size="sm"
                            className="text-xs font-mono gap-1.5 h-7"
                            onClick={() => setDrawerArtifactId(artifact.id)}
                          >
                            <Eye className="h-3 w-3" /> View Details
                          </Button>
                          {artifact.status === "ready" && (
                            <Button
                              variant="outline"
                              size="sm"
                              className="text-xs font-mono gap-1.5 h-7"
                              onClick={handleReindex}
                              disabled={isIndexing}
                            >
                              <RefreshCw className={cn("h-3 w-3", isIndexing && "animate-spin")} /> Regenerate
                            </Button>
                          )}
                          {artifact.status === "stale" && (
                            <Button
                              size="sm"
                              className="text-xs font-mono gap-1.5 h-7 bg-emerald-500 hover:bg-emerald-600 text-emerald-950 border-0"
                              onClick={() => toast.success(`Rebuilding ${artifact.name}...`)}
                            >
                              <Play className="h-3 w-3" /> Rebuild
                            </Button>
                          )}
                          {(artifact.status === "missing" || artifact.status === "error") && (
                            <Button
                              size="sm"
                              className="text-xs font-mono gap-1.5 h-7 bg-emerald-500 hover:bg-emerald-600 text-emerald-950 border-0"
                              onClick={() => toast.success(`Scanning to generate ${artifact.name}...`)}
                            >
                              <Terminal className="h-3 w-3" /> Re-scan
                            </Button>
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>

          {/* Hint */}
          {filteredArtifacts.length > 0 && (
            <p className="text-[10px] text-muted-foreground/60 font-mono text-center">
              Click an artifact to expand details · Regenerate to refresh intel
            </p>
          )}

        </div>
      </div>

      {/* Artifact Drawer */}
      <ArtifactDrawer
        artifact={drawerArtifact}
        isOpen={drawerArtifactId !== null}
        onClose={() => setDrawerArtifactId(null)}
      />
    </div>
  );
}
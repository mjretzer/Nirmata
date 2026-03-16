import { ReactNode, useState } from "react";
import {
  FolderOpen, Copy, Search, Terminal, ChevronRight, ArrowUpDown, Shield, Wrench,
} from "lucide-react";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Badge } from "../ui/badge";
import { Card, CardContent } from "../ui/card";
import { cn } from "../ui/utils";
import { toast } from "sonner";
import { copyToClipboard } from "../../utils/clipboard";
import { useWorkspace } from "../../hooks/useAosData";

export interface StatItem {
  label: string;
  value: string | number;
  color?: string; // tailwind text color class
}

export interface FileRow {
  id: string;
  filename: string;
  status?: string;
  statusColor?: string;
  meta?: string; // secondary info like date, count, etc.
  extraBadge?: string;
  extraBadgeVariant?: "default" | "secondary" | "destructive" | "outline";
  /** Sortable value for the Updated column (ISO string or timestamp) */
  sortDate?: string;
}

export interface FilterOption {
  label: string;
  value: string;
}

export interface FolderDashboardProps {
  /** Folder name shown in header */
  folderName: string;
  /** Full .aos path, e.g. ".aos/spec/uat" */
  folderPath: string;
  /** Breadcrumb segments (e.g. [".aos", "spec", "uat"]) */
  breadcrumb: string[];
  /** Quick stat counters */
  stats: StatItem[];
  /** Action buttons rendered in the header */
  actions?: ReactNode;
  /** Rows to render in the file list */
  rows: FileRow[];
  /** Called when a row is clicked (opens file in editor) */
  onRowClick: (row: FileRow) => void;
  /** Filter dropdown configs */
  filters?: {
    options: FilterOption[];
    value: string;
    onChange: (v: string) => void;
    placeholder?: string;
  }[];
  /** Optional second filter */
  filters2?: {
    options: FilterOption[];
    value: string;
    onChange: (v: string) => void;
    placeholder?: string;
  }[];
  /** Empty state message */
  emptyMessage?: string;
  /** Column label override for the date column (default: "Updated") */
  dateColumnLabel?: string;
  /** Optional pill toggle tabs (e.g. UAT ↔ Fix Loop) */
  toggleTabs?: { label: string; value: string; icon?: "shield" | "wrench" }[];
  /** Currently active toggle tab value */
  activeTab?: string;
  /** Called when a toggle tab is clicked */
  onTabChange?: (value: string) => void;
  /** Override for the search input placeholder (default: "Search files...") */
  searchPlaceholder?: string;
  /** Override for the first column header (default: "Filename") */
  filenameColumnLabel?: string;
  /** Override for the last column header (default: "Info") */
  infoColumnLabel?: string;
  /** Singular noun for the row count label (default: "file") */
  rowNoun?: string;
  /** Hint text shown below the list (default: "Click a row to open in editor · Column headers to sort") */
  hintText?: string;
  /** Tooltip prefix for row hover (default: "Click to open") */
  rowTooltipPrefix?: string;
}

type SortField = "filename" | "status" | "updated";
type SortDir = "asc" | "desc";

export function FolderDashboard({
  folderName,
  folderPath,
  breadcrumb,
  stats,
  actions,
  rows,
  onRowClick,
  filters,
  filters2,
  emptyMessage = "No files in this folder.",
  dateColumnLabel = "Updated",
  toggleTabs,
  activeTab,
  onTabChange,
  searchPlaceholder = "Search files...",
  filenameColumnLabel = "Filename",
  infoColumnLabel = "Info",
  rowNoun = "file",
  hintText = "Click a row to open in editor \u00b7 Column headers to sort",
  rowTooltipPrefix = "Click to open",
}: FolderDashboardProps) {
  const { workspace: currentWs } = useWorkspace();
  const [search, setSearch] = useState("");
  const [sortField, setSortField] = useState<SortField | null>(null);
  const [sortDir, setSortDir] = useState<SortDir>("asc");

  const copy = async () => {
    const ok = await copyToClipboard(folderPath);
    if (ok) toast.success(`Copied: ${folderPath}`);
    else toast.error("Failed to copy");
  };

  const filteredRows = rows.filter(
    (r) =>
      !search ||
      r.filename.toLowerCase().includes(search.toLowerCase()) ||
      r.id.toLowerCase().includes(search.toLowerCase()) ||
      (r.meta && r.meta.toLowerCase().includes(search.toLowerCase()))
  );

  const toggleSort = (field: SortField) => {
    if (sortField === field) {
      if (sortDir === "asc") setSortDir("desc");
      else { setSortField(null); setSortDir("asc"); }
    } else {
      setSortField(field);
      setSortDir("asc");
    }
  };

  const sortedRows = [...filteredRows].sort((a, b) => {
    if (!sortField) return 0;
    if (sortField === "updated") {
      const aVal = a.sortDate || a.meta || "";
      const bVal = b.sortDate || b.meta || "";
      return sortDir === "asc" ? aVal.localeCompare(bVal) : bVal.localeCompare(aVal);
    }
    const aVal = a[sortField] || "";
    const bVal = b[sortField] || "";
    return sortDir === "asc" ? aVal.localeCompare(bVal) : bVal.localeCompare(aVal);
  });

  return (
    <div className="flex-1 flex flex-col overflow-hidden bg-background">
      <div className="flex-1 overflow-auto">
        <div className="max-w-5xl mx-auto p-6 space-y-5">

          {/* Pill Toggle */}
          {toggleTabs && toggleTabs.length > 0 && (
            <div className="flex justify-center">
              <div className="inline-flex items-center bg-muted/60 border border-border rounded-full p-0.5 gap-0">
                {toggleTabs.map((tab) => {
                  const isActive = activeTab === tab.value;
                  const TabIcon = tab.icon === "shield" ? Shield : tab.icon === "wrench" ? Wrench : null;
                  return (
                    <button
                      key={tab.value}
                      onClick={() => onTabChange?.(tab.value)}
                      className={cn(
                        "relative flex items-center gap-1.5 px-6 py-1.5 rounded-full text-xs font-mono font-medium transition-all duration-200 cursor-pointer select-none",
                        isActive
                          ? "bg-foreground text-background shadow-sm"
                          : "text-muted-foreground hover:text-foreground"
                      )}
                    >
                      {TabIcon && <TabIcon className="h-3 w-3" />}
                      {tab.label}
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {/* Breadcrumb */}
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground font-mono">
            <span>{currentWs.projectName}</span>
            {breadcrumb.map((seg, i) => (
              <span key={i} className="flex items-center gap-1.5">
                <ChevronRight className="h-3 w-3" />
                <span className={i === breadcrumb.length - 1 ? "text-foreground flex items-center gap-1" : ""}>
                  {i === breadcrumb.length - 1 && <FolderOpen className="h-3 w-3" />}
                  {seg}
                </span>
              </span>
            ))}
          </div>

          {/* Header */}
          <div className="flex items-start justify-between">
            <div>
              <div className="flex items-center gap-2 mb-1">
                <Terminal className="h-4 w-4 text-muted-foreground" />
                <span className="font-mono text-xs text-muted-foreground">{folderPath}</span>
              </div>
              <h1 className="text-2xl font-bold tracking-tight">{folderName}</h1>
            </div>
            <div className="flex gap-2 items-center">
              <Button variant="outline" size="sm" className="font-mono text-xs gap-1.5" onClick={copy}>
                <Copy className="h-3 w-3" /> Copy Path
              </Button>
              {actions}
            </div>
          </div>

          {/* Stats Row */}
          {stats.length > 0 && (
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              {stats.map((s, i) => (
                <Card key={i} className="bg-card/50">
                  <CardContent className="p-3">
                    <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-1">{s.label}</div>
                    <div className={cn("font-mono text-lg font-bold", s.color || "text-foreground")}>{s.value}</div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}

          {/* Filter Row */}
          <div className="flex items-center gap-3">
            <div className="relative flex-1 max-w-sm">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <Input
                placeholder={searchPlaceholder}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="pl-8 h-8 text-xs font-mono"
              />
            </div>
            {filters && filters.map((f, i) => (
              <select
                key={i}
                value={f.value}
                onChange={(e) => f.onChange(e.target.value)}
                className="h-8 px-2 text-xs font-mono bg-background border border-border rounded-md text-foreground"
              >
                <option value="">{f.placeholder || "All"}</option>
                {f.options.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
            ))}
            {filters2 && filters2.map((f, i) => (
              <select
                key={`f2-${i}`}
                value={f.value}
                onChange={(e) => f.onChange(e.target.value)}
                className="h-8 px-2 text-xs font-mono bg-background border border-border rounded-md text-foreground"
              >
                <option value="">{f.placeholder || "All"}</option>
                {f.options.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
            ))}
            <span className="text-xs text-muted-foreground font-mono ml-auto">
              {sortedRows.length} {rowNoun}{sortedRows.length !== 1 ? "s" : ""}
            </span>
          </div>

          {/* File List */}
          <div className="border border-border rounded-lg overflow-hidden">
            {/* Header */}
            <div className="grid grid-cols-[1fr_100px_140px_80px] gap-3 px-4 py-2 bg-muted/50 border-b border-border text-[10px] text-muted-foreground uppercase tracking-wider font-medium font-mono">
              <span
                className="cursor-pointer"
                onClick={() => toggleSort("filename")}
              >
                {filenameColumnLabel}
                {sortField === "filename" && (
                  <ArrowUpDown className="inline-block ml-1 h-2.5 w-2.5" />
                )}
              </span>
              <span
                className="cursor-pointer"
                onClick={() => toggleSort("status")}
              >
                Status
                {sortField === "status" && (
                  <ArrowUpDown className="inline-block ml-1 h-2.5 w-2.5" />
                )}
              </span>
              <span
                className="cursor-pointer"
                onClick={() => toggleSort("updated")}
              >
                {dateColumnLabel}
                {sortField === "updated" && (
                  <ArrowUpDown className="inline-block ml-1 h-2.5 w-2.5" />
                )}
              </span>
              <span className="text-right">{infoColumnLabel}</span>
            </div>

            {sortedRows.length === 0 ? (
              <div className="px-4 py-8 text-center text-sm text-muted-foreground font-mono">
                {emptyMessage}
              </div>
            ) : (
              <div>
                {sortedRows.map((row) => (
                  <div
                    key={row.id}
                    className="grid grid-cols-[1fr_100px_140px_80px] gap-3 px-4 py-2.5 hover:bg-accent/40 cursor-pointer transition-colors border-b border-border/50 last:border-0 group"
                    onClick={() => onRowClick(row)}
                    title={`${rowTooltipPrefix} ${row.filename} in editor`}
                  >
                    <span className="font-mono text-xs text-foreground truncate">{row.filename}</span>
                    <span>
                      {row.status && (
                        <Badge
                          variant={
                            row.statusColor === "green" ? "secondary" :
                            row.statusColor === "red" ? "destructive" :
                            "outline"
                          }
                          className="text-[10px] h-5"
                        >
                          {row.status}
                        </Badge>
                      )}
                    </span>
                    <span className="font-mono text-[11px] text-muted-foreground truncate">{row.meta || "—"}</span>
                    <span className="text-right">
                      {row.extraBadge && (
                        <Badge variant={row.extraBadgeVariant || "outline"} className="text-[10px] h-5">
                          {row.extraBadge}
                        </Badge>
                      )}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Hint */}
          {sortedRows.length > 0 && (
            <p className="text-[10px] text-muted-foreground/60 font-mono text-center">
              {hintText}
            </p>
          )}

        </div>
      </div>
    </div>
  );
}
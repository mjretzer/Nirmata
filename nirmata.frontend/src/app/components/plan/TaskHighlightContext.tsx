import { createContext, useContext, useState, useCallback, type ReactNode } from "react";

interface TaskHighlightContextValue {
  /** Currently highlighted task ID (e.g. "TSK-000012"), or null */
  highlightedTaskId: string | null;
  /** Source of the highlight: "graph" = git node clicked, "roadmap" = task row clicked */
  highlightSource: "graph" | "roadmap" | null;
  /** Set highlight from the git graph */
  highlightFromGraph: (taskId: string | null) => void;
  /** Set highlight from the roadmap task list */
  highlightFromRoadmap: (taskId: string | null) => void;
  /** Clear highlight */
  clearHighlight: () => void;
}

const TaskHighlightContext = createContext<TaskHighlightContextValue>({
  highlightedTaskId: null,
  highlightSource: null,
  highlightFromGraph: () => {},
  highlightFromRoadmap: () => {},
  clearHighlight: () => {},
});

export function TaskHighlightProvider({ children }: { children: ReactNode }) {
  const [highlightedTaskId, setHighlightedTaskId] = useState<string | null>(null);
  const [highlightSource, setHighlightSource] = useState<"graph" | "roadmap" | null>(null);

  const highlightFromGraph = useCallback((taskId: string | null) => {
    setHighlightedTaskId(prev => prev === taskId ? null : taskId);
    setHighlightSource(taskId ? "graph" : null);
  }, []);

  const highlightFromRoadmap = useCallback((taskId: string | null) => {
    setHighlightedTaskId(prev => prev === taskId ? null : taskId);
    setHighlightSource(taskId ? "roadmap" : null);
  }, []);

  const clearHighlight = useCallback(() => {
    setHighlightedTaskId(null);
    setHighlightSource(null);
  }, []);

  return (
    <TaskHighlightContext.Provider value={{ highlightedTaskId, highlightSource, highlightFromGraph, highlightFromRoadmap, clearHighlight }}>
      {children}
    </TaskHighlightContext.Provider>
  );
}

export function useTaskHighlight() {
  return useContext(TaskHighlightContext);
}

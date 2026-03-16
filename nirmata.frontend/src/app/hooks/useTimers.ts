/**
 * Shared timer hooks.
 *
 * General-purpose timing utilities that can be reused across pages
 * (HostConsolePage, DiagnosticsPage, OrchestratorPage, etc.).
 */

import { useState, useEffect, useRef } from "react";

/**
 * Ticks once per second, returning the current `Date`.
 * Useful for live clocks and "last refreshed" displays.
 */
export function useLiveClock(): Date {
  const [now, setNow] = useState(new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);
  return now;
}

/**
 * Tracks elapsed time since mount (or since `running` became `true`).
 * Returns a formatted "Xh Ym Zs" string.
 *
 * @param running – when false the timer pauses; when true it counts.
 */
export function useUptime(running: boolean): string {
  const startRef = useRef(Date.now());
  const [elapsed, setElapsed] = useState(0);

  useEffect(() => {
    if (!running) return;
    const id = setInterval(() => setElapsed(Date.now() - startRef.current), 1000);
    return () => clearInterval(id);
  }, [running]);

  const s = Math.floor(elapsed / 1000);
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  return `${h}h ${m}m ${s % 60}s`;
}

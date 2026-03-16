import { useParams, Navigate } from "react-router";

export function LegacyPathRedirect() {
  const { workspaceId, "*": relativePath } = useParams();
  
  if (!relativePath || relativePath === "") {
    return <Navigate to={`/ws/${workspaceId}`} replace />;
  }

  if (relativePath === ".aos/state") {
    return <Navigate to={`/ws/${workspaceId}/chat`} replace />;
  }
  
  if (relativePath === ".aos/codebase") {
    return <Navigate to={`/ws/${workspaceId}/files/.aos/codebase`} replace />;
  }

  // Default to files view
  return <Navigate to={`/ws/${workspaceId}/files/${relativePath}`} replace />;
}
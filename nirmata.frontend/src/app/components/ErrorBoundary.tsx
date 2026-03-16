import { useRouteError, isRouteErrorResponse, Link, useNavigate } from "react-router";
import { AlertTriangle, Home, ArrowLeft, Terminal, AlertCircle } from "lucide-react";
import { Button } from "./ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "./ui/card";

export function ErrorBoundary() {
  const error = useRouteError();
  const navigate = useNavigate();

  let errorTitle = "Unexpected Error";
  let errorMessage = "An unknown error occurred while processing your request.";
  let errorStatus: string | number | undefined;
  let errorStack: string | undefined;

  if (isRouteErrorResponse(error)) {
    errorStatus = error.status;
    if (error.status === 404) {
      errorTitle = "Path Not Found";
      errorMessage = "The requested file or directory does not exist in the current workspace context.";
    } else {
      errorTitle = error.statusText || "Route Error";
      errorMessage = error.data?.message || JSON.stringify(error.data) || "A routing error occurred.";
    }
  } else if (error instanceof Error) {
    errorTitle = "Execution Exception";
    errorMessage = error.message;
    errorStack = error.stack;
  } else if (typeof error === 'string') {
    errorMessage = error;
  }

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-background text-foreground p-4">
      <Card className="w-full max-w-lg shadow-lg border-destructive/20 bg-card/50 backdrop-blur-sm">
        <CardHeader className="space-y-1">
          <div className="flex items-center gap-2 text-destructive">
            <AlertCircle className="h-5 w-5" />
            <CardTitle className="text-xl font-mono">
              {errorStatus ? `Error ${errorStatus}: ` : ""}{errorTitle}
            </CardTitle>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="p-3 rounded-md bg-muted/50 border border-border font-mono text-sm overflow-auto max-h-48 whitespace-pre-wrap break-words">
            <span className="text-destructive font-bold">$ error: </span>
            {errorMessage}
            {errorStack && (
              <div className="mt-2 pt-2 border-t border-border/50 text-xs text-muted-foreground opacity-70">
                {errorStack}
              </div>
            )}
          </div>
          
          <p className="text-sm text-muted-foreground">
            This workspace operation could not be completed. Check the path or return to the dashboard.
          </p>
        </CardContent>
        <CardFooter className="flex justify-between border-t border-border pt-4">
          <Button variant="outline" onClick={() => navigate(-1)} className="gap-2">
            <ArrowLeft className="h-4 w-4" />
            Go Back
          </Button>
          <Button asChild className="gap-2">
            <Link to="/">
              <Home className="h-4 w-4" />
              Return to Workspace
            </Link>
          </Button>
        </CardFooter>
      </Card>
      
      <div className="mt-8 flex items-center gap-2 text-xs text-muted-foreground opacity-50 font-mono">
        <Terminal className="h-3 w-3" />
        <span>AOS Command Console v1.0.0</span>
      </div>
    </div>
  );
}
